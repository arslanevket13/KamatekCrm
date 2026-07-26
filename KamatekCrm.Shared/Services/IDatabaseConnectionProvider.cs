using System.Threading.Tasks;

namespace KamatekCrm.Shared.Services
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
}
