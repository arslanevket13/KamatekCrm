using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
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
        private readonly string _configuredConnectionString;

        private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.NoRecursion);

        public DatabaseConnectionProvider(IConfiguration configuration)
        {
            _configuredConnectionString = configuration.GetConnectionString("PostgreSQL")
                ?? configuration["DatabaseSettings:ConnectionStrings:PostgreSQL"]
                ?? throw new InvalidOperationException(
                    "PostgreSQL bağlantı dizesi yapılandırılmamış. " +
                    "ConnectionStrings:PostgreSQL veya DatabaseSettings:ConnectionStrings:PostgreSQL ayarını tanımlayın.");
        }

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
                var builder = new NpgsqlConnectionStringBuilder(_configuredConnectionString);
                if (!string.IsNullOrWhiteSpace(_currentServerIp))
                    builder.Host = _currentServerIp;

                return builder.ConnectionString;
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

            var connectionBuilder = new NpgsqlConnectionStringBuilder(_configuredConnectionString)
            {
                Host = cleanIp,
                Pooling = false,
                CommandTimeout = 2,
                Timeout = 2
            };
            string testConnString = connectionBuilder.ConnectionString;

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
