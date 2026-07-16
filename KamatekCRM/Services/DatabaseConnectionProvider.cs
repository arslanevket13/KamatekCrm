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
    }
}
