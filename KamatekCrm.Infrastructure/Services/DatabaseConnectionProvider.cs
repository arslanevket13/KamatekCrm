using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.Infrastructure.Services
{
    public static class NetworkExtensions
    {
        public static string SanitizeServerAddress(this string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            var uriPrefixes = new[] { "http://", "https://", "ftp://" };
            foreach (var prefix in uriPrefixes)
            {
                if (input.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    input = input.Substring(prefix.Length);
            }

            input = input.Replace("\\", "").Replace("//", "");

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
    /// </summary>
    public class DatabaseConnectionProvider : IDatabaseConnectionProvider, IDisposable
    {
        private string _currentServerIp = "";
        private bool _isConnected;
        private bool _disposed;

        private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.NoRecursion);

        private const string DbUser = "postgres";
        private const string DbPass = "1313";
        private const string DbName = "kamatekcrm";

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

        public async Task<bool> TestConnectionAsync(string serverIp)
        {
            if (string.IsNullOrWhiteSpace(serverIp)) return false;
            
            string cleanIp = serverIp.SanitizeServerAddress();
            
            if (string.IsNullOrWhiteSpace(cleanIp) || cleanIp == "0.0.0.0" || cleanIp == "::0") 
            {
                return false;
            }

            string testConnString = $"Host={cleanIp};Database={DbName};Username={DbUser};" +
                                    $"Password={DbPass};Pooling=false;CommandTimeout=2;";

            return await Task.Run(async () =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                    optionsBuilder.UseNpgsql(testConnString);
                    using var dbContext = new AppDbContext(optionsBuilder.Options);
                    
                    return await dbContext.Database.CanConnectAsync(cts.Token);
                }
                catch
                {
                    return false;
                }
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _rwLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
