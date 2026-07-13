using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Threading;

namespace KamatekCrm.Services;

public class NetworkDiscoveryService
{
    private const int BROADCAST_PORT = 54321;
    private const string BROADCAST_HEADER = "KAMATEK_SERVER_IP:";
    
    private CancellationTokenSource? _broadcastCts;
    private CancellationTokenSource? _listenCts;
    private CancellationTokenSource? _heartbeatCts;
    private string? _lastKnownServerIp;
    private bool _isConnectionLost = false;

    /// <summary>
    /// Başlatma metodu (App.xaml.cs tarafından çağrılır)
    /// appsettings.json'dan IsMainServer ayarını okur ve ona göre yayıncı veya dinleyici başlatır.
    /// </summary>
    public void Start()
    {
        bool isMainServer = false;
        try
        {
            string appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (!File.Exists(appSettingsPath))
            {
                string? projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName;
                if (!string.IsNullOrEmpty(projectRoot)) appSettingsPath = Path.Combine(projectRoot, "appsettings.json");
            }

            if (File.Exists(appSettingsPath))
            {
                var jsonString = File.ReadAllText(appSettingsPath);
                var jsonObject = JsonNode.Parse(jsonString);
                var isMainServerNode = jsonObject?["NetworkDiscovery"]?["IsMainServer"];
                if (isMainServerNode != null)
                {
                    isMainServer = isMainServerNode.GetValue<bool>();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] IsMainServer okunamadı: {ex.Message}");
        }

        if (isMainServer)
        {
            StartBroadcasting();
        }
        else
        {
            _ = DiscoverAndConnectAsync();
        }
    }

    /// <summary>
    /// 1. Sunucu Tarafı Yayını (Broadcaster)
    /// Eğer uygulama "Ana Bilgisayar" (IsServer = true) olarak başlatılmışsa çağrılır.
    /// Arka planda çalışarak her 3 saniyede bir lokal IP adresini ağa yayınlar.
    /// </summary>
    private void StartBroadcasting()
    {
        _broadcastCts = new CancellationTokenSource();
        Task.Run(() => BroadcastLoopAsync(_broadcastCts.Token));
        System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] Sunucu yayını başlatıldı (Port: {BROADCAST_PORT})");
    }

    public void StopBroadcasting()
    {
        _broadcastCts?.Cancel();
    }

    private async Task BroadcastLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;
            
            var endPoint = new IPEndPoint(IPAddress.Broadcast, BROADCAST_PORT);
            string localIp = GetLocalIpAddress();

            while (!cancellationToken.IsCancellationRequested)
            {
                string message = $"{BROADCAST_HEADER}{localIp}";
                byte[] bytes = Encoding.UTF8.GetBytes(message);

                try
                {
                    await udpClient.SendAsync(bytes, bytes.Length, endPoint);
                    System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] Broadcast: {message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] Yayın hatası: {ex.Message}");
                }

                await Task.Delay(3000, cancellationToken); // Her 3 saniyede bir yayınla
            }
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[NetworkDiscovery] Yayın durduruldu.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] Kritik Broadcast Hatası: {ex.Message}");
        }
    }

    /// <summary>
    /// 2. İstemci Tarafı Dinlemesi (Listener)
    /// İstemci (Client) bilgisayarlarda çağrılır. Ana bilgisayarı bulana kadar portu dinler.
    /// </summary>
    /// <param name="timeoutSeconds">Maksimum arama süresi (Saniye)</param>
    private async Task DiscoverAndConnectAsync(int timeoutSeconds = 10)
    {
        _listenCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            using var udpClient = new UdpClient();
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, BROADCAST_PORT));

            System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] Sunucu aranıyor... (Port: {BROADCAST_PORT}, Timeout: {timeoutSeconds}sn)");

            while (!_listenCts.Token.IsCancellationRequested)
            {
                var receiveTask = udpClient.ReceiveAsync();
                
                // Timeout'u asenkron olarak engellememek için Task.WhenAny kullanımı
                var completedTask = await Task.WhenAny(receiveTask, Task.Delay(-1, _listenCts.Token));

                if (completedTask == receiveTask)
                {
                    var result = await receiveTask;
                    string receivedMessage = Encoding.UTF8.GetString(result.Buffer);

                    if (receivedMessage.StartsWith(BROADCAST_HEADER))
                    {
                        string serverIp = receivedMessage.Substring(BROADCAST_HEADER.Length).Trim();
                        System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] Sunucu başarıyla bulundu! IP: {serverIp}");
                        
                        _lastKnownServerIp = serverIp;
                        await UpdateConnectionStringAsync(serverIp);
                        
                        // 3. Eğer bağlantı daha önce koptuysa, geri geldiğini bildir
                        if (_isConnectionLost)
                        {
                            _isConnectionLost = false;
                            EventAggregator.Instance.Publish(new DatabaseConnectionRestoredEvent());
                        }

                        // Heartbeat başlat (Zaten çalışıyorsa iptal edip yeniden başlatır)
                        StartHeartbeatAsync();

                        return; // Sunucuyu bulduk ve ayarları güncelledik, işlem tamam.
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 3. Güvenlik ve Hata Yönetimi - Timeout Durumu
            ShowErrorDialog("Ana Bilgisayar Bulunamadı", "Yerel ağda ana bilgisayar bulunamadı. Lütfen sunucunun açık olduğundan ve ağa bağlı olduğundan emin olun.");
        }
        catch (SocketException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] UDP Port Hatası: {ex.Message}");
            ShowErrorDialog("Ağ Hatası", $"Dinleme portu ({BROADCAST_PORT}) kullanılıyor veya erişilemiyor.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] Dinleme Hatası: {ex.Message}");
        }
    }

    /// <summary>
    /// 2. Heartbeat (Kalp Atışı) Mekanizması
    /// Her 5 saniyede bir ana sunucuya Ping atarak bağlantıyı kontrol eder.
    /// Ardı ardına 2 başarısızlıkta DatabaseConnectionLostEvent fırlatır ve yeniden arama (Discover) başlatır.
    /// </summary>
    public void StartHeartbeatAsync()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts = new CancellationTokenSource();
        Task.Run(() => HeartbeatLoopAsync(_heartbeatCts.Token));
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_lastKnownServerIp)) return;

        int consecutiveFailures = 0;
        using var ping = new System.Net.NetworkInformation.Ping();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var reply = await ping.SendPingAsync(_lastKnownServerIp, 2000); // 2 saniye timeout
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    consecutiveFailures = 0;
                }
                else
                {
                    consecutiveFailures++;
                }
            }
            catch
            {
                consecutiveFailures++;
            }

            if (consecutiveFailures >= 2 && !_isConnectionLost)
            {
                System.Diagnostics.Debug.WriteLine("[NetworkDiscovery] Heartbeat Başarısız! Bağlantı Koptu.");
                _isConnectionLost = true;
                
                // Event Aggregator ile koptuğunu global olarak duyur
                EventAggregator.Instance.Publish(new DatabaseConnectionLostEvent());
                
                // Tekrar dinlemeye geç (Süresiz arama için timeout'u büyük verebiliriz)
                _ = DiscoverAndConnectAsync(timeoutSeconds: 3600);
                
                break; // Döngüden çık, Discover yeniden bağlandığında heartbeat'i tekrar başlatacak.
            }

            await Task.Delay(5000, cancellationToken); // Her 5 saniyede bir kontrol
        }
    }

    /// <summary>
    /// Dinamik Connection String Güncellemesi
    /// Bulunan sunucu IP adresini appsettings.json içerisindeki Host=... kısmına yazar.
    /// </summary>
    private async Task UpdateConnectionStringAsync(string newIpAddress)
    {
        try
        {
            string appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            
            if (!File.Exists(appSettingsPath))
            {
                // Geliştirme ortamında (bin/Debug) çalışıyorsak kök dizine fallback
                string? projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName;
                if (!string.IsNullOrEmpty(projectRoot))
                {
                    appSettingsPath = Path.Combine(projectRoot, "appsettings.json");
                }
            }

            if (!File.Exists(appSettingsPath))
            {
                System.Diagnostics.Debug.WriteLine("[NetworkDiscovery] appsettings.json bulunamadı!");
                return;
            }

            string jsonString = await File.ReadAllTextAsync(appSettingsPath);
            var jsonObject = JsonNode.Parse(jsonString);

            if (jsonObject != null)
            {
                var connectionStringNode = jsonObject["DatabaseSettings"]?["ConnectionStrings"]?["PostgreSQL"];
                if (connectionStringNode != null)
                {
                    string currentConnString = connectionStringNode.ToString();
                    
                    // Host= kısmını yeni IP ile değiştir (Regex yerine güvenli parça birleştirme)
                    string[] parts = currentConnString.Split(';');
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i].Trim().StartsWith("Host=", StringComparison.OrdinalIgnoreCase))
                        {
                            parts[i] = $"Host={newIpAddress}";
                            break;
                        }
                    }

                    string newConnString = string.Join(";", parts);
                    jsonObject["DatabaseSettings"]!["ConnectionStrings"]!["PostgreSQL"] = newConnString;

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(appSettingsPath, jsonObject.ToJsonString(options));

                    System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] ConnectionString güncellendi -> Host: {newIpAddress}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] ConnectionString güncelleme hatası: {ex.Message}");
        }
    }

    /// <summary>
    /// Mevcut makinenin yerel (Local) IPv4 adresini bulur.
    /// </summary>
    private string GetLocalIpAddress()
    {
        try 
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] IP Adresi alınamadı: {ex.Message}");
        }
        return "127.0.0.1";
    }

    private void ShowErrorDialog(string title, string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }
}
