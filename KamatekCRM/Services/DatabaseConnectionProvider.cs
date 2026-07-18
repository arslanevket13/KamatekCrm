using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Data;

namespace KamatekCrm.Services
{
    public interface IDatabaseConnectionProvider
    {
        string CurrentServerIp { get; }
        string GetConnectionString();
        void SetServerIp(string ipAddress);
        
        bool IsConnected { get; }
        void SetConnectionState(bool isConnected);
        
        Task<bool> TestConnectionAsync(string serverIp);
    }

    public static class NetworkExtensions
    {
        public static string SanitizeServerAddress(this string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            // Strip HTTP/HTTPS
            var uriPrefixes = new[] { "http://", "https://", "ftp://" };
            foreach (var prefix in uriPrefixes)
            {
                if (input.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    input = input.Substring(prefix.Length);
            }

            // Strip UNC slashes
            input = input.Replace("\\", "").Replace("//", "");

            // Strip trailing ports (e.g. 192.168.1.50:5432 -> 192.168.1.50)
            int colonIndex = input.IndexOf(':');
            if (colonIndex > 0)
            {
                input = input.Substring(0, colonIndex);
            }

            return input.Trim();
        }
    }

    /// <summary>
    /// PostgreSQL bağlantı dizesini yöneten thread-safe Singleton servis.
    /// 
    /// Eşzamanlılık stratejisi: <see cref="ReaderWriterLockSlim"/>
    /// - Okumalar (GetConnectionString, CurrentServerIp, IsConnected): Birden fazla
    ///   iş parçacığı eş zamanlı olarak okuyabilir (EnterReadLock).
    /// - Yazmalar (SetServerIp, SetConnectionState): Tek yazıcı erişimi garanti
    ///   edilir, tüm okuyucular yazma bitene kadar beklenir (EnterWriteLock).
    /// 
    /// Bu servis DI konteynerinde Singleton olarak kayıtlıdır.
    /// <see cref="IDisposable"/> ile ReaderWriterLockSlim kaynağı temizlenir.
    /// </summary>
    public class DatabaseConnectionProvider : IDatabaseConnectionProvider, IDisposable
    {
        #region Fields

        private string _currentServerIp = "";
        private bool _isConnected;
        private bool _disposed;

        /// <summary>
        /// Read-heavy / Write-rare senaryo için optimize edilmiş kilit.
        /// Recursion desteği kapatılmıştır (deadlock önleme).
        /// </summary>
        private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.NoRecursion);

        private const string DbUser = "postgres";   // varsayılan
        private const string DbPass = "1313";      // varsayılan
        private const string DbName = "kamatekcrm";  // varsayılan

        #endregion

        #region Properties (Read-Locked)

        /// <summary>
        /// O anki aktif sunucu IP adresini thread-safe şekilde döner.
        /// </summary>
        public string CurrentServerIp
        {
            get
            {
                _rwLock.EnterReadLock();
                try
                {
                    return _currentServerIp;
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Veritabanına bağlı olup olmadığını thread-safe şekilde döner.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                _rwLock.EnterReadLock();
                try
                {
                    return _isConnected;
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }
        }

        #endregion

        #region Write Operations (Write-Locked)

        /// <summary>
        /// Aktif sunucu IP adresini günceller.
        /// Yazma kilidi alır — tüm okuyucular yazma bitene kadar beklenir.
        /// </summary>
        public void SetServerIp(string ipAddress)
        {
            if (ipAddress == "0.0.0.0" || ipAddress == "::" || ipAddress == System.Net.IPAddress.Any.ToString())
            {
                ipAddress = "127.0.0.1";
            }

            _rwLock.EnterWriteLock();
            try
            {
                _currentServerIp = ipAddress ?? "";
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Bağlantı durumunu günceller.
        /// Yazma kilidi alır — tüm okuyucular yazma bitene kadar beklenir.
        /// </summary>
        public void SetConnectionState(bool isConnected)
        {
            _rwLock.EnterWriteLock();
            try
            {
                _isConnected = isConnected;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        #endregion

        #region Read Operations (Read-Locked)

        /// <summary>
        /// Mevcut sunucu IP'sine göre tam bağlantı dizesi oluşturur.
        /// Okuma kilidi alır — birden fazla ViewModel eş zamanlı çağırabilir.
        /// </summary>
        /// <exception cref="InvalidOperationException">Sunucu IP henüz ayarlanmamışsa.</exception>
        public string GetConnectionString()
        {
            _rwLock.EnterReadLock();
            try
            {
                if (string.IsNullOrEmpty(_currentServerIp))
                    throw new InvalidOperationException(
                        "Sunucu IP adresi henüz bulunamadı. Bağlantı dizesi oluşturulamaz.");

                return $"Host={_currentServerIp};Database={DbName};Username={DbUser};" +
                       $"Password={DbPass};Pooling=true;MinPoolSize=1;MaxPoolSize=100;CommandTimeout=20;";
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        #endregion

        #region Connection Test (Kilitsiz — kendi parametreleriyle çalışır)

        /// <summary>
        /// Belirtilen IP adresine test bağlantısı yapar.
        /// Bu metot paylaşılan durumu (state) okumaz veya değiştirmez;
        /// parametre olarak aldığı IP'yi kullanır, bu nedenle kilide ihtiyaç duymaz.
        /// </summary>
        public async Task<bool> TestConnectionAsync(string serverIp)
        {
            if (string.IsNullOrWhiteSpace(serverIp)) return false;
            
            string cleanIp = serverIp.SanitizeServerAddress();
            
            if (string.IsNullOrWhiteSpace(cleanIp) || cleanIp == "0.0.0.0" || cleanIp == "::0") 
            {
                return false;
            }

            // Strict Connection String for testing (Pooling=false prevents keeping bad connections open)
            string testConnString = $"Host={cleanIp};Database={DbName};Username={DbUser};" +
                                    $"Password={DbPass};Pooling=false;CommandTimeout=2;";

            // Offload DNS resolution and network waiting to background thread (Prevent UI Freeze)
            return await Task.Run(async () =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // STRICTLY 2000ms Timeout
                try
                {
                    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                    optionsBuilder.UseNpgsql(testConnString);
                    using var dbContext = new AppDbContext(optionsBuilder.Options);
                    
                    // This strictly validates that PostgreSQL is running AND the credentials/DB Name are correct
                    return await dbContext.Database.CanConnectAsync(cts.Token);
                }
                catch
                {
                    return false;
                }
            });
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// ReaderWriterLockSlim kaynağını temizler.
        /// DI konteyneri Singleton ömrünü yönettiğinden,
        /// uygulama kapanışında otomatik çağrılır.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _rwLock.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}

