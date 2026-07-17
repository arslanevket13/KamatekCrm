using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KamatekCrm.Services;
using MediatR; // Or Prism.Events if they use Prism, wait the old code used EventAggregator.Instance.Publish

namespace KamatekCrm.Services
{
    // Mock event classes if they don't exist in MediatR, or just use the old ones.
    // The old code used EventAggregator.Instance.Publish(new DatabaseConnectionRestoredEvent());
    // I'll keep the same but we must ensure it compiles.
    public class DatabaseServiceFailedEvent { }
    
    public class NetworkDiscoveryService : BackgroundService
    {
        private const int BROADCAST_PORT = 54321;
        private const string EXPECTED_PING_SIGNATURE = "KAMATEK_DISCOVERY_PING";
        
        private readonly IDatabaseConnectionProvider _connectionProvider;
        private readonly ILogger<NetworkDiscoveryService> _logger;

        public NetworkDiscoveryService(
            IDatabaseConnectionProvider connectionProvider,
            ILogger<NetworkDiscoveryService> logger)
        {
            _connectionProvider = connectionProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 1. Test Cached Server
            string savedAddress = KamatekCrm.Properties.Settings.Default.SavedServerAddress;
            if (!string.IsNullOrWhiteSpace(savedAddress))
            {
                _logger.LogInformation($"Zero-Configuration: Ön belleğe alınmış adres test ediliyor -> {savedAddress}");
                if (await _connectionProvider.TestConnectionAsync(savedAddress))
                {
                    ConfirmConnection(savedAddress);
                    return; // STOP
                }
            }

            // 2. Test Localhost (Is this machine the server?)
            _logger.LogInformation("Zero-Configuration: Yerel PostgreSQL kontrol ediliyor (127.0.0.1)...");
            if (await _connectionProvider.TestConnectionAsync("127.0.0.1"))
            {
                ConfirmConnection("127.0.0.1");
                _logger.LogInformation("Bu cihaz Ana Sunucu. UDP Broadcast başlatılıyor.");
                // Sunucu olduğumuz için ağdaki diğer istemcilere yayın yapmaya devam et
                _ = BroadcastLoopAsync(stoppingToken); 
                return; // STOP
            }

            // 3. Listen for UDP Broadcasts (Strict 3 second timeout)
            _logger.LogInformation("Zero-Configuration: UDP yayınları dinleniyor (Maks 3 saniye)...");
            string udpIp = await ListenForUdpBroadcastAsync(TimeSpan.FromSeconds(3), stoppingToken);
            if (!string.IsNullOrWhiteSpace(udpIp))
            {
                _logger.LogInformation($"UDP Sunucu Bulundu: {udpIp}. Bağlantı test ediliyor...");
                if (await _connectionProvider.TestConnectionAsync(udpIp))
                {
                    KamatekCrm.Properties.Settings.Default.SavedServerAddress = udpIp;
                    KamatekCrm.Properties.Settings.Default.Save();
                    ConfirmConnection(udpIp);
                    
                    _logger.LogInformation("Bu cihaz İstemci.");
                    // İstemci modundayken dinlemeye devam edebiliriz veya bırakabiliriz. 
                    // Bağlantı koparsa yeniden bu döngü çalışmalı (NetworkDiscoveryService restart).
                    return; // STOP
                }
            }

            // 4. Fallback to Connection Wizard
            _logger.LogCritical("Zero-Configuration: Sunucu bulunamadı. Manuel bağlantı sihirbazı çağrılıyor.");
            try
            {
                KamatekCrm.Services.EventAggregator.Instance?.Publish(new KamatekCrm.Services.DatabaseConnectionLostEvent());
                // TODO: ShowManualConnectionWizardEvent fırlatılabilir. Şu an DatabaseConnectionLostEvent, arayüzü çıkarıyor.
            }
            catch { }
        }

        private void ConfirmConnection(string ip)
        {
            _connectionProvider.SetServerIp(ip);
            _connectionProvider.SetConnectionState(true);
            try
            {
                KamatekCrm.Services.EventAggregator.Instance?.Publish(new KamatekCrm.Services.DatabaseConnectionRestoredEvent());
            }
            catch { }
        }

        private async Task<string> ListenForUdpBroadcastAsync(TimeSpan timeout, CancellationToken stoppingToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(timeout);

            try
            {
                using var udpClient = new UdpClient();
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, BROADCAST_PORT));

                var receiveTask = udpClient.ReceiveAsync(cts.Token);
                var result = await receiveTask;
                
                string receivedMessage = Encoding.UTF8.GetString(result.Buffer);
                if (receivedMessage == EXPECTED_PING_SIGNATURE)
                {
                    return result.RemoteEndPoint.Address.ToString();
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout oldu
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UDP Keşif sırasında hata.");
            }
            return string.Empty;
        }

        private async Task BroadcastLoopAsync(CancellationToken stoppingToken)
        {
            using var udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;
            var endPoint = new IPEndPoint(IPAddress.Broadcast, BROADCAST_PORT);
            byte[] bytes = Encoding.UTF8.GetBytes(EXPECTED_PING_SIGNATURE);

            // Eğer bu ana sunucuysa kendi veritabanına bağlanacağı için direkt 127.0.0.1 verip connected yapıyoruz
            _connectionProvider.SetServerIp("127.0.0.1");
            _connectionProvider.SetConnectionState(true);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await udpClient.SendAsync(bytes, bytes.Length, endPoint);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Broadcast yayın hatası.");
                }

                await Task.Delay(3000, stoppingToken);
            }
        }


    }
}
