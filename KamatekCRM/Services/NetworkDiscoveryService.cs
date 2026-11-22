using KamatekCrm.Helpers;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Services;
using KamatekCrm.Shared.Services;
using MediatR; // Or Prism.Events if they use Prism, wait the old code used EventAggregator.Instance.Publish

namespace KamatekCrm.Services
{
    // Mock event classes if they don't exist in MediatR, or just use the old ones.
    // The old code used EventAggregator.Instance.Publish(new DatabaseConnectionRestoredEvent());
    // I'll keep the same but we must ensure it compiles.
    public class DatabaseServiceFailedEvent { }

    public class DiscoveredServerInfo
    {
        public string IpAddress { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public int Port { get; set; } = 5432;
        public string DisplayText => !string.IsNullOrWhiteSpace(HostName) && HostName != IpAddress
            ? $"{HostName} ({IpAddress})"
            : IpAddress;
    }
    
    public class NetworkDiscoveryService : BackgroundService
    {
        private const int BROADCAST_PORT = 54321;
        private const string EXPECTED_PING_SIGNATURE = "KAMATEK_DISCOVERY_PING";
        
        private readonly IDatabaseConnectionProvider _connectionProvider;
        private readonly ILogger<NetworkDiscoveryService> _logger;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public NetworkDiscoveryService(
            IDatabaseConnectionProvider connectionProvider,
            ILogger<NetworkDiscoveryService> logger,
            Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _connectionProvider = connectionProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // DEV BYPASS: Debug modunda doğrudan 127.0.0.1 doğrulanır; erişilemiyorsa Heartbeat recovery devralır
            if (System.Diagnostics.Debugger.IsAttached)
            {
                _logger.LogInformation("DEVELOPER BYPASS: Debugger tespit edildi. Yerel sunucu (127.0.0.1) test ediliyor...");
                if (await _connectionProvider.TestConnectionAsync("127.0.0.1"))
                {
                    ConfirmConnection("127.0.0.1");
                }
                else
                {
                    _connectionProvider.SetServerIp("127.0.0.1");
                    _connectionProvider.SetConnectionState(false);
                    _logger.LogWarning("DEVELOPER BYPASS: 127.0.0.1 şu an erişilemiyor; ConnectionHeartbeatService otomatik yeniden bağlanmayı sürdürecek.");
                }
                return;
            }

            var discoveredIp = await TryDiscoverServerAsync(isStartup: true, stoppingToken);
            if (!string.IsNullOrWhiteSpace(discoveredIp))
            {
                ConfirmConnection(discoveredIp);

                bool isMainServer = _configuration.GetValue<bool>("NetworkDiscovery:IsMainServer", false);
                if (isMainServer && discoveredIp == "127.0.0.1")
                {
                    _logger.LogInformation("Bu cihaz Ana Sunucu. UDP Broadcast başlatılıyor.");
                    BroadcastLoopAsync(stoppingToken).SafeFireAndForget("NetworkDiscoveryService.BroadcastLoopAsync");
                }
                return;
            }

            // Fallback to Connection Wizard (HeartbeatService arka planda yeniden bağlanmayı denemeye devam eder)
            _logger.LogCritical("Zero-Configuration: Sunucu bulunamadı. Manuel bağlantı sihirbazı çağrılıyor.");
            try
            {
                KamatekCrm.Services.EventAggregator.Instance?.Publish(new KamatekCrm.Services.DatabaseConnectionLostEvent());
            }
            catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "NetworkDiscoveryService.cs"); }
        }

        /// <summary>
        /// Bağlantı koptuğunda veya başlangıçta sunucu IP adresini yeniden keşfetmeyi dener.
        /// </summary>
        public async Task<string?> TryRediscoverServerAsync(CancellationToken stoppingToken)
        {
            return await TryDiscoverServerAsync(isStartup: false, stoppingToken);
        }

        private async Task<string?> TryDiscoverServerAsync(bool isStartup, CancellationToken stoppingToken)
        {
            // 1. Test Cached Server
            string savedAddress = KamatekCrm.Properties.Settings.Default.SavedServerAddress;
            if (!string.IsNullOrWhiteSpace(savedAddress))
            {
                _logger.LogInformation($"Zero-Configuration: Ön belleğe alınmış adres test ediliyor -> {savedAddress}");
                if (await _connectionProvider.TestConnectionAsync(savedAddress))
                {
                    return savedAddress;
                }
            }

            // 2. Test Localhost (Is this machine the server?)
            bool isMainServer = _configuration.GetValue<bool>("NetworkDiscovery:IsMainServer", false);
            if (isMainServer)
            {
                _logger.LogInformation("Zero-Configuration: Yerel PostgreSQL kontrol ediliyor (127.0.0.1)...");
                if (await _connectionProvider.TestConnectionAsync("127.0.0.1"))
                {
                    return "127.0.0.1";
                }
            }

            // 3. Listen for UDP Broadcasts
            int configuredTimeout = _configuration.GetValue<int>("NetworkDiscovery:TimeoutSeconds", 15);
            int timeoutSeconds = isStartup ? configuredTimeout : Math.Min(configuredTimeout, 4);
            _logger.LogInformation($"Zero-Configuration: UDP yayınları dinleniyor (Maks {timeoutSeconds} saniye)...");

            string udpIp = await ListenForUdpBroadcastAsync(TimeSpan.FromSeconds(timeoutSeconds), stoppingToken);
            if (!string.IsNullOrWhiteSpace(udpIp))
            {
                _logger.LogInformation($"UDP Sunucu Bulundu: {udpIp}. Bağlantı test ediliyor...");
                if (await _connectionProvider.TestConnectionAsync(udpIp))
                {
                    KamatekCrm.Properties.Settings.Default.SavedServerAddress = udpIp;
                    KamatekCrm.Properties.Settings.Default.Save();
                    return udpIp;
                }
            }

            // 4. Fallback to LAN Parallel Subnet Scan (SemaphoreSlim 32 + 300ms timeout)
            _logger.LogInformation("Zero-Configuration: UDP yayını bulunamadı. Hızlı LAN paralel alt ağ taraması başlatılıyor (SemaphoreSlim 32)...");
            string scannedIp = await ScanSubnetForPostgresAsync(stoppingToken);
            if (!string.IsNullOrWhiteSpace(scannedIp))
            {
                _logger.LogInformation($"LAN taramasında PostgreSQL sunucusu bulundu: {scannedIp}. Bağlantı test ediliyor...");
                if (await _connectionProvider.TestConnectionAsync(scannedIp))
                {
                    KamatekCrm.Properties.Settings.Default.SavedServerAddress = scannedIp;
                    KamatekCrm.Properties.Settings.Default.Save();
                    return scannedIp;
                }
            }

            return null;
        }

        /// <summary>
        /// Aktif yerel ağ bağdaştırıcısının alt ağındaki (192.168.x.1-254) 5432 portu açık PostgreSQL sunucularını
        /// SemaphoreSlim(32) ve 300ms zaman aşımı ile paralel tarar.
        /// Her bulunan sunucuda UI güncellemeleri için onServerFound callback tetiklenir.
        /// </summary>
        public async Task<System.Collections.Generic.List<DiscoveredServerInfo>> ScanSubnetServersAsync(
            Action<DiscoveredServerInfo>? onServerFound = null,
            CancellationToken cancellationToken = default)
        {
            var discoveredServers = new System.Collections.Concurrent.ConcurrentBag<DiscoveredServerInfo>();
            var subnetPrefix = GetLocalSubnetPrefix();
            if (string.IsNullOrEmpty(subnetPrefix))
            {
                subnetPrefix = "192.168.1.";
            }

            using var semaphore = new SemaphoreSlim(32, 32);
            var tasks = new System.Collections.Generic.List<Task>();

            for (int i = 1; i <= 254; i++)
            {
                string targetIp = $"{subnetPrefix}{i}";
                tasks.Add(Task.Run(async () =>
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        if (cancellationToken.IsCancellationRequested) return;

                        using var ipCts = new CancellationTokenSource(300); // 300ms kısa bağlantı zaman aşımı
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ipCts.Token);
                        using var client = new TcpClient();

                        await client.ConnectAsync(targetIp, 5432, linkedCts.Token).ConfigureAwait(false);
                        if (client.Connected)
                        {
                            string hostName = targetIp;
                            try
                            {
                                var hostEntryTask = Dns.GetHostEntryAsync(targetIp);
                                if (await Task.WhenAny(hostEntryTask, Task.Delay(350, cancellationToken)).ConfigureAwait(false) == hostEntryTask)
                                {
                                    var hostEntry = await hostEntryTask.ConfigureAwait(false);
                                    if (!string.IsNullOrWhiteSpace(hostEntry.HostName))
                                    {
                                        hostName = hostEntry.HostName;
                                    }
                                }
                            }
                            catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "NetworkDiscoveryService.cs"); }

                            var serverInfo = new DiscoveredServerInfo
                            {
                                IpAddress = targetIp,
                                HostName = hostName,
                                Port = 5432
                            };
                            discoveredServers.Add(serverInfo);
                            onServerFound?.Invoke(serverInfo);
                        }
                    }
                    catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "NetworkDiscoveryService.cs"); }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "NetworkDiscoveryService.cs"); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Alt ağ taraması sırasında beklenmeyen durum.");
            }

            return discoveredServers.OrderBy(s => s.IpAddress).ToList();
        }

        /// <summary>
        /// Yerel alt ağdaki olası PostgreSQL sunucularını SemaphoreSlim(32) ile paralel tarar.
        /// </summary>
        private async Task<string> ScanSubnetForPostgresAsync(CancellationToken stoppingToken)
        {
            var subnetPrefix = GetLocalSubnetPrefix();
            if (string.IsNullOrEmpty(subnetPrefix))
                return string.Empty;

            using var semaphore = new SemaphoreSlim(32, 32);
            using var searchCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            string? foundIp = null;
            var tasks = new System.Collections.Generic.List<Task>();

            for (int i = 1; i <= 254; i++)
            {
                string targetIp = $"{subnetPrefix}{i}";
                tasks.Add(Task.Run(async () =>
                {
                    if (searchCts.IsCancellationRequested || foundIp != null) return;

                    await semaphore.WaitAsync(searchCts.Token).ConfigureAwait(false);
                    try
                    {
                        if (searchCts.IsCancellationRequested || foundIp != null) return;

                        using var ipCts = new CancellationTokenSource(300); // 300ms kısa bağlantı zaman aşımı
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(searchCts.Token, ipCts.Token);
                        using var client = new TcpClient();

                        await client.ConnectAsync(targetIp, 5432, linkedCts.Token).ConfigureAwait(false);
                        if (client.Connected)
                        {
                            foundIp = targetIp;
                            searchCts.Cancel(); // Bulundu, diğer bekleyen istekleri iptal et
                        }
                    }
                    catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "NetworkDiscoveryService.cs"); }
                    finally
                    {
                        semaphore.Release();
                    }
                }, stoppingToken));
            }

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "NetworkDiscoveryService.cs"); }

            return foundIp ?? string.Empty;
        }

        private static string? GetLocalSubnetPrefix()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        var bytes = ip.GetAddressBytes();
                        // 192.168.x. veya 10.x.x. vb.
                        return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.";
                    }
                }
            }
            catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "NetworkDiscoveryService.cs"); }
            return null;
        }

        private void ConfirmConnection(string ip)
        {
            _connectionProvider.SetServerIp(ip);
            _connectionProvider.SetConnectionState(true);
            try
            {
                KamatekCrm.Services.EventAggregator.Instance?.Publish(new KamatekCrm.Services.DatabaseConnectionRestoredEvent());
            }
            catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "NetworkDiscoveryService.cs"); }
        }

        private async Task<string> ListenForUdpBroadcastAsync(TimeSpan timeout, CancellationToken stoppingToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(timeout);

            int port = _configuration.GetValue<int>("NetworkDiscovery:Port", 54321);

            try
            {
                using var udpClient = new UdpClient();
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));

                var receiveTask = udpClient.ReceiveAsync(cts.Token);
                var result = await receiveTask;
                
                string receivedMessage = Encoding.UTF8.GetString(result.Buffer);
                if (receivedMessage == EXPECTED_PING_SIGNATURE)
                {
                    return result.RemoteEndPoint.Address.ToString();
                }
            }
            catch (OperationCanceledException catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "NetworkDiscoveryService.cs"); }
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
            
            int port = _configuration.GetValue<int>("NetworkDiscovery:Port", 54321);
            var endPoint = new IPEndPoint(IPAddress.Broadcast, port);
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
