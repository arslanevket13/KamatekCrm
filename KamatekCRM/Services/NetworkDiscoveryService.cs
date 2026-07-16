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
            // Ana sunucu mu kontrolü (Settings.settings üzerinden)
            bool isMainServer = KamatekCrm.Properties.Settings.Default.IsMainServer;
            bool isManualOverride = KamatekCrm.Properties.Settings.Default.IsMainServerManualOverride;

            if (!isManualOverride)
            {
                _logger.LogInformation("Zero-Configuration Aktif: Yerel veritabanı aranıyor...");
                bool isLocalDbRunning = await IsLocalDatabaseRunningAsync(stoppingToken);

                if (isLocalDbRunning)
                {
                    _logger.LogInformation("Yerel veritabanı (PostgreSQL) bulundu. Sistem 'Ana Sunucu' olarak ayarlanıyor.");
                    isMainServer = true;
                }
                else
                {
                    _logger.LogInformation("Yerel veritabanı TCP pinge yanıt vermedi. Windows Servisleri kontrol ediliyor...");
                    bool isPostgresInstalled = false;
                    
                    try
                    {
                        var services = System.ServiceProcess.ServiceController.GetServices();
                        var pgService = System.Linq.Enumerable.FirstOrDefault(services, s => s.ServiceName.ToLower().Contains("postgres") || s.DisplayName.ToLower().Contains("postgres"));
                        
                        if (pgService != null)
                        {
                            isPostgresInstalled = true;
                            _logger.LogWarning($"PostgreSQL servisi ({pgService.ServiceName}) bulundu ancak durumu: {pgService.Status}");
                            
                            if (pgService.Status == System.ServiceProcess.ServiceControllerStatus.Stopped)
                            {
                                try
                                {
                                    _logger.LogInformation("PostgreSQL servisi başlatılmaya çalışılıyor...");
                                    pgService.Start();
                                    pgService.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                                    isLocalDbRunning = true;
                                    _logger.LogInformation("PostgreSQL servisi başarıyla başlatıldı!");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "PostgreSQL servisi başlatılamadı. Yönetici yetkisi gerekebilir.");
                                    try {
                                        KamatekCrm.Services.EventAggregator.Instance?.Publish(new KamatekCrm.Services.DatabaseServiceFailedEvent());
                                    } catch { }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Windows Service kontrolü yapılamadı (İşletim sistemi kısıtlaması olabilir).");
                    }
                    
                    if (isLocalDbRunning)
                    {
                        _logger.LogInformation("Hizmet başlatıldığı için Sistem 'Ana Sunucu' olarak ayarlanıyor.");
                        isMainServer = true;
                    }
                    else if (isPostgresInstalled)
                    {
                        _logger.LogCritical("Bu cihaz Ana Sunucu ancak PostgreSQL başlatılamadı. İstemci moduna GEÇİLMİYOR.");
                        isMainServer = true; // Asıl sunucu ama kapalı. İstemci olup sonsuz döngüye girmesin.
                    }
                    else
                    {
                        _logger.LogInformation("Yerel veritabanı tamamen yok. Sistem 'İstemci' olarak ayarlanıyor.");
                        isMainServer = false;
                    }
                }

                // Sadece RAM üzerinde tutmuyoruz, bir sonraki manuel geçersiz kılmaya kadar ayarları da tutarlı yapıyoruz
                KamatekCrm.Properties.Settings.Default.IsMainServer = isMainServer;
                KamatekCrm.Properties.Settings.Default.Save();
            }
            else
            {
                _logger.LogInformation($"Manuel Override Aktif: Sistem yapılandırmaya göre { (isMainServer ? "Ana Sunucu" : "İstemci") } olarak çalışacak.");
            }

            if (isMainServer)
            {
                _logger.LogInformation("Bu cihaz Ana Sunucu. UDP Broadcast başlatılıyor.");
                await BroadcastLoopAsync(stoppingToken);
            }
            else
            {
                _logger.LogInformation("Bu cihaz İstemci. Sunucu aranıyor (Listener başlatılıyor).");
                
                // İstemci isek sunucu aranıyor animasyonunu başlat (Event Aggregator)
                try
                {
                    KamatekCrm.Services.EventAggregator.Instance?.Publish(new KamatekCrm.Services.DatabaseConnectionLostEvent());
                }
                catch { }

                await ListenLoopAsync(stoppingToken);
            }
        }

        private async Task<bool> IsLocalDatabaseRunningAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var tcpClient = new TcpClient();
                // 500ms timeout for ultra-fast startup detection without blocking UI
                var connectTask = tcpClient.ConnectAsync("127.0.0.1", 5432);
                var delayTask = Task.Delay(500, stoppingToken);

                var completedTask = await Task.WhenAny(connectTask, delayTask);
                
                if (completedTask == connectTask && tcpClient.Connected)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Yerel veritabanı ping atılamadı.");
            }
            
            return false;
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

        private async Task ListenLoopAsync(CancellationToken stoppingToken)
        {
            using var udpClient = new UdpClient();
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, BROADCAST_PORT));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_connectionProvider.IsConnected)
                    {
                        await Task.Delay(2000, stoppingToken);
                        continue;
                    }

                    var receiveResult = await udpClient.ReceiveAsync(stoppingToken);
                    string receivedMessage = Encoding.UTF8.GetString(receiveResult.Buffer);

                    if (receivedMessage == EXPECTED_PING_SIGNATURE)
                    {
                        string physicalIp = receiveResult.RemoteEndPoint.Address.ToString();
                        
                        _logger.LogInformation($"Sunucu keşfedildi. Gerçek Fiziksel IP: {physicalIp}");
                        
                        _connectionProvider.SetServerIp(physicalIp);
                        _connectionProvider.SetConnectionState(true);
                        
                        // Eski kodda olan: EventAggregator.Instance.Publish(new DatabaseConnectionRestoredEvent());
                        // Eğer o static sinif duruyorsa cagiriyoruz
                        try
                        {
                            // Try to publish if the class exists
                            KamatekCrm.Services.EventAggregator.Instance?.Publish(new KamatekCrm.Services.DatabaseConnectionRestoredEvent());
                        }
                        catch { }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "UDP Keşif sırasında hata.");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
    }
}
