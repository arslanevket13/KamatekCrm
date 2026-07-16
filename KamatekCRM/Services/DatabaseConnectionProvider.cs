using System;

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

    public class DatabaseConnectionProvider : IDatabaseConnectionProvider
    {
        private string _currentServerIp = "";
        private bool _isConnected;
        private readonly object _lockObj = new object();

        private const string DbUser = "postgres"; // varsayılan
        private const string DbPass = "123456";   // varsayılan
        private const string DbName = "kamatekcrm"; // varsayılan

        public string CurrentServerIp 
        {
            get
            {
                lock (_lockObj) return _currentServerIp;
            }
        }

        public bool IsConnected
        {
            get
            {
                lock (_lockObj) return _isConnected;
            }
        }

        public void SetServerIp(string ipAddress)
        {
            lock (_lockObj)
            {
                _currentServerIp = ipAddress;
            }
        }

        public void SetConnectionState(bool isConnected)
        {
            lock (_lockObj)
            {
                _isConnected = isConnected;
            }
        }

        public string GetConnectionString()
        {
            lock (_lockObj)
            {
                if (string.IsNullOrEmpty(_currentServerIp))
                    throw new InvalidOperationException("Sunucu IP adresi henüz bulunamadı. Bağlantı dizesi oluşturulamaz.");

                return $"Host={_currentServerIp};Database={DbName};Username={DbUser};Password={DbPass};Pooling=true;MinPoolSize=1;MaxPoolSize=100;CommandTimeout=20;";
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

            // Offload DNS resolution and network waiting to background thread (Prevent UI Freeze)
            return await Task.Run(async () =>
            {
                using var cts = new System.Threading.CancellationTokenSource(2000); // STRICTLY 2000ms Timeout
                try
                {
                    using var tcpClient = new System.Net.Sockets.TcpClient();
                    
                    var connectTask = tcpClient.ConnectAsync(cleanIp, 5432);
                    var delayTask = Task.Delay(2000, cts.Token);
                    
                    var completedTask = await Task.WhenAny(connectTask, delayTask);
                    
                    if (completedTask == connectTask && tcpClient.Connected)
                    {
                        return true;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
