using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Data;
using KamatekCrm.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Ağ & Bağlantı Yönetimi ViewModel.
    /// appsettings.json NetworkDiscovery bölümünü iki yönlü (read/write) kontrol eder.
    /// Sunucu modundayken bağlı istemcileri PostgreSQL pg_stat_activity üzerinden izler.
    /// </summary>
    public partial class NetworkSettingsViewModel : ViewModelBase
    {
        #region Dependencies

        private readonly IConfiguration _configuration;
        private readonly IDatabaseConnectionProvider _connectionProvider;
        private readonly NetworkDiscoveryService _discoveryService;
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IToastService _toastService;
        private readonly ILogger<NetworkSettingsViewModel> _logger;

        #endregion

        #region Constructor

        public NetworkSettingsViewModel(
            IConfiguration configuration,
            IDatabaseConnectionProvider connectionProvider,
            NetworkDiscoveryService discoveryService,
            IDbContextFactory<AppDbContext> dbContextFactory,
            IToastService toastService,
            ILogger<NetworkSettingsViewModel> logger)
        {
            _configuration = configuration;
            _connectionProvider = connectionProvider;
            _discoveryService = discoveryService;
            _dbContextFactory = dbContextFactory;
            _toastService = toastService;
            _logger = logger;

            ConnectedClients = new ObservableCollection<ConnectedClientModel>();

            // appsettings.json'dan mevcut değerleri yükle
            LoadSettingsFromConfig();

            // Çalışma anındaki durumu yansıt
            RefreshLiveStatus();

            // Sunucu modundaysa istemci listesini yükle
            if (_isMainServer && _connectionProvider.IsConnected)
            {
                Task.Run(async () => await LoadConnectedClientsAsync());
            }
        }

        #endregion

        #region Observable Properties

        private bool _isMainServer;
        /// <summary>
        /// Bu bilgisayar Ana Sunucu mu? (appsettings.json → NetworkDiscovery:IsMainServer)
        /// Değişiklik anında diske yazılır; yeniden başlatma gerektirir.
        /// </summary>
        public bool IsMainServer
        {
            get => _isMainServer;
            set
            {
                if (SetProperty(ref _isMainServer, value))
                {
                    _hasUnsavedChanges = true;
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    OnPropertyChanged(nameof(RoleDisplayText));
                    OnPropertyChanged(nameof(IsClientMode));
                }
            }
        }

        private bool _isAutoDiscoveryEnabled = true;
        /// <summary>
        /// Otomatik UDP keşfi aktif mi, yoksa manuel IP mi girilecek?
        /// (appsettings.json → NetworkDiscovery:Enabled)
        /// </summary>
        public bool IsAutoDiscoveryEnabled
        {
            get => _isAutoDiscoveryEnabled;
            set
            {
                if (SetProperty(ref _isAutoDiscoveryEnabled, value))
                {
                    _hasUnsavedChanges = true;
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    OnPropertyChanged(nameof(IsManualIpEnabled));
                }
            }
        }

        private string _manualServerIp = string.Empty;
        /// <summary>
        /// Manuel sunucu IP adresi. Sadece IsAutoDiscoveryEnabled == false iken aktif.
        /// </summary>
        public string ManualServerIp
        {
            get => _manualServerIp;
            set
            {
                if (SetProperty(ref _manualServerIp, value))
                {
                    _hasUnsavedChanges = true;
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }

        private string _connectionStatus = "Bilinmiyor";
        /// <summary>
        /// Anlık bağlantı durum metni.
        /// </summary>
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        private bool _isConnectionHealthy;
        /// <summary>
        /// Yeşil/Kırmızı gösterge için bağlantı sağlığı.
        /// </summary>
        public bool IsConnectionHealthy
        {
            get => _isConnectionHealthy;
            set => SetProperty(ref _isConnectionHealthy, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private bool _hasUnsavedChanges;
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set => SetProperty(ref _hasUnsavedChanges, value);
        }

        private int _configuredPort = 54321;
        public int ConfiguredPort
        {
            get => _configuredPort;
            set => SetProperty(ref _configuredPort, value);
        }

        private int _configuredTimeout = 15;
        public int ConfiguredTimeout
        {
            get => _configuredTimeout;
            set => SetProperty(ref _configuredTimeout, value);
        }

        #endregion

        #region Computed Properties

        /// <summary>
        /// Sunucu/İstemci rol metni.
        /// </summary>
        public string RoleDisplayText => _isMainServer ? "🖥️ ANA SUNUCU" : "💻 İSTEMCİ";

        /// <summary>
        /// Manuel IP giriş alanının aktif olup olmadığı.
        /// </summary>
        public bool IsManualIpEnabled => !_isAutoDiscoveryEnabled && !_isMainServer;

        /// <summary>
        /// İstemci modunda mı?
        /// </summary>
        public bool IsClientMode => !_isMainServer;

        #endregion

        #region Collections

        /// <summary>
        /// Sunucu modundayken PostgreSQL'e bağlı istemcilerin listesi.
        /// </summary>
        public ObservableCollection<ConnectedClientModel> ConnectedClients { get; }

        #endregion

        #region Commands

        private bool IsNotBusy() => !IsBusy;

        /// <summary>
        /// Değişiklikleri appsettings.json'a yazar ve kullanıcıya yeniden başlatma teklif eder.
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task SaveAndRestart()
        {
            IsBusy = true;
            try
            {
                WriteSettingsToConfig();

                // Properties.Settings'e de yaz (uygulamanın diğer bölümleri için)
                Properties.Settings.Default.IsMainServer = _isMainServer;
                Properties.Settings.Default.IsMainServerManualOverride = true;
                Properties.Settings.Default.Save();

                _hasUnsavedChanges = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));

                _toastService.ShowSuccess("Ağ ayarları kaydedildi.");

                var result = MessageBox.Show(
                    "Ağ ayarları başarıyla kaydedildi.\n\n" +
                    "Değişikliklerin etkili olması için uygulamanın yeniden başlatılması gerekmektedir.\n\n" +
                    "Şimdi yeniden başlatılsın mı?",
                    "Yeniden Başlatma Gerekli",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    RestartApplication();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ağ ayarları kaydedilirken hata.");
                _toastService.ShowError($"Ayarlar kaydedilemedi: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Ağ keşfini uygulama yeniden başlatılmadan manuel olarak tetikler.
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task ScanNetwork()
        {
            IsBusy = true;
            ConnectionStatus = "Ağ taranıyor...";
            try
            {
                // Önce önbelleğe alınmış sunucuyu test et
                string savedAddress = Properties.Settings.Default.SavedServerAddress;
                if (!string.IsNullOrWhiteSpace(savedAddress))
                {
                    ConnectionStatus = $"Önbellek test ediliyor: {savedAddress}...";
                    if (await _connectionProvider.TestConnectionAsync(savedAddress))
                    {
                        _connectionProvider.SetServerIp(savedAddress);
                        _connectionProvider.SetConnectionState(true);
                        ConnectionStatus = $"Bağlı → {savedAddress}";
                        IsConnectionHealthy = true;
                        _toastService.ShowSuccess($"Sunucu bulundu: {savedAddress}");

                        if (_isMainServer)
                            _ = LoadConnectedClientsAsync();
                        return;
                    }
                }

                // IsMainServer ise localhost'u test et
                if (_isMainServer)
                {
                    ConnectionStatus = "Yerel PostgreSQL test ediliyor (127.0.0.1)...";
                    if (await _connectionProvider.TestConnectionAsync("127.0.0.1"))
                    {
                        _connectionProvider.SetServerIp("127.0.0.1");
                        _connectionProvider.SetConnectionState(true);
                        ConnectionStatus = "Ana Sunucu — 127.0.0.1 üzerinde yayın yapılıyor";
                        IsConnectionHealthy = true;
                        _toastService.ShowSuccess("Yerel veritabanı bulundu. Bu makine Ana Sunucu.");
                        _ = LoadConnectedClientsAsync();
                        return;
                    }
                }

                // İstemci modunda — UDP dinle
                if (!_isMainServer && _isAutoDiscoveryEnabled)
                {
                    int port = _configuredPort;
                    int timeout = _configuredTimeout;
                    ConnectionStatus = $"UDP yayınları dinleniyor (Port: {port}, Timeout: {timeout}s)...";

                    // UDP dinleme — basitleştirilmiş versiyon
                    string discoveredIp = await ListenForDiscoveryAsync(port, timeout);

                    if (!string.IsNullOrWhiteSpace(discoveredIp))
                    {
                        ConnectionStatus = $"Sunucu bulundu: {discoveredIp}. Bağlantı test ediliyor...";
                        if (await _connectionProvider.TestConnectionAsync(discoveredIp))
                        {
                            _connectionProvider.SetServerIp(discoveredIp);
                            _connectionProvider.SetConnectionState(true);
                            Properties.Settings.Default.SavedServerAddress = discoveredIp;
                            Properties.Settings.Default.Save();
                            ConnectionStatus = $"Bağlı → {discoveredIp}";
                            IsConnectionHealthy = true;
                            _toastService.ShowSuccess($"Sunucu bulundu ve bağlanıldı: {discoveredIp}");
                            return;
                        }
                    }
                }

                ConnectionStatus = "Sunucu bulunamadı";
                IsConnectionHealthy = false;
                _toastService.ShowError("Ağ taraması tamamlandı, sunucu bulunamadı.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ağ taraması sırasında hata.");
                ConnectionStatus = $"Tarama hatası: {ex.Message}";
                IsConnectionHealthy = false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Manuel girilen IP adresini test eder ve bağlanır.
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task TestConnection()
        {
            if (string.IsNullOrWhiteSpace(_manualServerIp))
            {
                _toastService.ShowError("Lütfen bir IP adresi girin.");
                return;
            }

            IsBusy = true;
            string cleanIp = _manualServerIp.SanitizeServerAddress();
            ConnectionStatus = $"Test ediliyor: {cleanIp}...";

            try
            {
                bool success = await _connectionProvider.TestConnectionAsync(cleanIp);

                if (success)
                {
                    _connectionProvider.SetServerIp(cleanIp);
                    _connectionProvider.SetConnectionState(true);
                    Properties.Settings.Default.SavedServerAddress = cleanIp;
                    Properties.Settings.Default.Save();

                    ConnectionStatus = $"Bağlı → {cleanIp}";
                    IsConnectionHealthy = true;
                    _toastService.ShowSuccess($"Bağlantı başarılı: {cleanIp}");

                    // Bağlantı kurulduğunu sisteme bildir
                    try
                    {
                        EventAggregator.Instance?.Publish(new DatabaseConnectionRestoredEvent());
                    }
                    catch { }
                }
                else
                {
                    ConnectionStatus = $"Bağlantı başarısız: {cleanIp}";
                    IsConnectionHealthy = false;
                    _toastService.ShowError($"Bağlantı kurulamadı: {cleanIp}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bağlantı testi sırasında hata: {Ip}", cleanIp);
                ConnectionStatus = $"Test hatası: {ex.Message}";
                IsConnectionHealthy = false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Sunucu modundayken bağlı istemcileri PostgreSQL pg_stat_activity üzerinden yeniler.
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task RefreshClients()
        {
            if (!_isMainServer)
            {
                _toastService.ShowError("İstemci listesi yalnızca Ana Sunucu modunda görüntülenebilir.");
                return;
            }

            await LoadConnectedClientsAsync();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// appsettings.json'dan NetworkDiscovery bölümündeki değerleri okur.
        /// </summary>
        private void LoadSettingsFromConfig()
        {
            try
            {
                _isMainServer = _configuration.GetValue<bool>("NetworkDiscovery:IsMainServer", false);
                _isAutoDiscoveryEnabled = _configuration.GetValue<bool>("NetworkDiscovery:Enabled", true);
                _configuredPort = _configuration.GetValue<int>("NetworkDiscovery:Port", 54321);
                _configuredTimeout = _configuration.GetValue<int>("NetworkDiscovery:TimeoutSeconds", 15);

                // Manuel IP: Eğer FallbackToConfig açıksa ve önceden kaydedilmiş bir IP varsa yükle
                _manualServerIp = Properties.Settings.Default.SavedServerAddress ?? string.Empty;

                // Properties bildir
                OnPropertyChanged(nameof(IsMainServer));
                OnPropertyChanged(nameof(IsAutoDiscoveryEnabled));
                OnPropertyChanged(nameof(ManualServerIp));
                OnPropertyChanged(nameof(ConfiguredPort));
                OnPropertyChanged(nameof(ConfiguredTimeout));
                OnPropertyChanged(nameof(RoleDisplayText));
                OnPropertyChanged(nameof(IsClientMode));
                OnPropertyChanged(nameof(IsManualIpEnabled));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "appsettings.json okunurken hata.");
            }
        }

        /// <summary>
        /// Çalışma anındaki bağlantı durumunu UI'a yansıtır.
        /// </summary>
        private void RefreshLiveStatus()
        {
            IsConnectionHealthy = _connectionProvider.IsConnected;
            string currentIp = _connectionProvider.CurrentServerIp;

            if (_connectionProvider.IsConnected && !string.IsNullOrWhiteSpace(currentIp))
            {
                if (_isMainServer)
                {
                    int port = _configuredPort;
                    ConnectionStatus = $"Ana Sunucu — Port {port} üzerinde yayın yapılıyor (Bağlı: {currentIp})";
                }
                else
                {
                    ConnectionStatus = $"İstemci — Bağlı → {currentIp}";
                }
            }
            else
            {
                ConnectionStatus = _isMainServer
                    ? "Ana Sunucu — Veritabanı bağlantısı bekleniyor..."
                    : "İstemci — Sunucu aranıyor...";
            }
        }

        /// <summary>
        /// Değişiklikleri appsettings.json dosyasına yazar.
        /// </summary>
        private void WriteSettingsToConfig()
        {
            string appSettingsPath = GetAppSettingsPath();

            if (!File.Exists(appSettingsPath))
                throw new FileNotFoundException($"appsettings.json bulunamadı: {appSettingsPath}");

            string jsonString = File.ReadAllText(appSettingsPath);
            var jsonObject = JsonNode.Parse(jsonString);

            if (jsonObject == null)
                throw new InvalidOperationException("appsettings.json parse edilemedi.");

            // NetworkDiscovery bölümünü güncelle
            var networkSection = jsonObject["NetworkDiscovery"];
            if (networkSection == null)
            {
                jsonObject["NetworkDiscovery"] = new JsonObject();
                networkSection = jsonObject["NetworkDiscovery"];
            }

            networkSection!["IsMainServer"] = _isMainServer;
            networkSection!["Enabled"] = _isAutoDiscoveryEnabled;
            networkSection!["Port"] = _configuredPort;
            networkSection!["TimeoutSeconds"] = _configuredTimeout;
            networkSection!["FallbackToConfig"] = true;

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(appSettingsPath, jsonObject.ToJsonString(options));
        }

        /// <summary>
        /// appsettings.json dosya yolunu bulur (build output veya proje kökü).
        /// </summary>
        private static string GetAppSettingsPath()
        {
            string buildPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(buildPath)) return buildPath;

            // Dev ortamı için proje köküne fallback
            string? projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                string devPath = Path.Combine(projectRoot, "appsettings.json");
                if (File.Exists(devPath)) return devPath;
            }

            return buildPath; // Bulunamazsa yine build path döner (hata fırlatılır)
        }

        /// <summary>
        /// Uygulamayı yeniden başlatır.
        /// </summary>
        private static void RestartApplication()
        {
            try
            {
                var appPath = Application.ResourceAssembly.Location.Replace(".dll", ".exe");
                System.Diagnostics.Process.Start(appPath);
                Application.Current.Shutdown();
            }
            catch
            {
                MessageBox.Show(
                    "Uygulama yeniden başlatılamadı. Lütfen manuel olarak kapatıp açın.",
                    "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Manuel ağ taraması için basitleştirilmiş UDP dinleme.
        /// </summary>
        private static async Task<string> ListenForDiscoveryAsync(int port, int timeoutSeconds)
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                using var udpClient = new System.Net.Sockets.UdpClient();
                udpClient.Client.SetSocketOption(
                    System.Net.Sockets.SocketOptionLevel.Socket,
                    System.Net.Sockets.SocketOptionName.ReuseAddress, true);
                udpClient.Client.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, port));

                var result = await udpClient.ReceiveAsync(cts.Token);
                string message = System.Text.Encoding.UTF8.GetString(result.Buffer);

                if (message == "KAMATEK_DISCOVERY_PING")
                {
                    return result.RemoteEndPoint.Address.ToString();
                }
            }
            catch (OperationCanceledException) { }
            catch { }

            return string.Empty;
        }

        /// <summary>
        /// PostgreSQL pg_stat_activity üzerinden bağlı istemcileri yükler.
        /// Yalnızca Ana Sunucu modunda ve veritabanı bağlıyken çalışır.
        /// </summary>
        private async Task LoadConnectedClientsAsync()
        {
            if (!_connectionProvider.IsConnected)
            {
                _logger.LogWarning("LoadConnectedClientsAsync çağrıldı ancak veritabanı bağlantısı yok.");
                return;
            }

            IsBusy = true;
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();

                var clients = await context.Database
                    .SqlQueryRaw<ConnectedClientDto>(
                        @"SELECT client_addr AS ClientAddr,
                                 state AS State,
                                 query_start AS QueryStart
                          FROM pg_stat_activity
                          WHERE datname = 'kamatekcrm'
                            AND client_addr IS NOT NULL")
                    .ToListAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConnectedClients.Clear();
                    foreach (var client in clients)
                    {
                        ConnectedClients.Add(new ConnectedClientModel
                        {
                            ClientAddress = client.ClientAddr?.ToString() ?? "Bilinmiyor",
                            State = TranslateState(client.State),
                            QueryStart = client.QueryStart,
                            IsActive = client.State == "active"
                        });
                    }
                });

                _logger.LogInformation("Bağlı istemci sayısı: {Count}", ConnectedClients.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bağlı istemciler yüklenirken hata.");
                Application.Current.Dispatcher.Invoke(() => 
                {
                    _toastService?.ShowError($"İstemci listesi okunamadı: {ex.Message}");
                });
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// PostgreSQL bağlantı durumunu Türkçe'ye çevirir.
        /// </summary>
        private static string TranslateState(string? state) => state switch
        {
            "active" => "Aktif (Sorgu çalışıyor)",
            "idle" => "Boşta",
            "idle in transaction" => "İşlem içinde boşta",
            "idle in transaction (aborted)" => "İşlem içinde (iptal edildi)",
            "fastpath function call" => "Fastpath çağrısı",
            "disabled" => "Devre dışı",
            _ => state ?? "Bilinmiyor"
        };

        #endregion
    }

    #region Models

    /// <summary>
    /// pg_stat_activity ham DTO — EF Core SqlQueryRaw için gerekli.
    /// </summary>
    public class ConnectedClientDto
    {
        public System.Net.IPAddress? ClientAddr { get; set; }
        public string? State { get; set; }
        public DateTime? QueryStart { get; set; }
    }

    /// <summary>
    /// UI'da gösterilecek bağlı istemci modeli.
    /// </summary>
    public class ConnectedClientModel
    {
        public string ClientAddress { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public DateTime? QueryStart { get; set; }
        public bool IsActive { get; set; }

        /// <summary>
        /// Son sorgu zamanını insan tarafından okunabilir formata çevirir.
        /// </summary>
        public string QueryStartDisplay => QueryStart.HasValue
            ? QueryStart.Value.ToLocalTime().ToString("HH:mm:ss")
            : "—";
    }

    #endregion
}
