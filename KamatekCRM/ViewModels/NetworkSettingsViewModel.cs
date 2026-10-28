using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Services;
using KamatekCrm.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Ağ & Bağlantı Yönetimi ViewModel.
    /// appsettings.json NetworkDiscovery ve DatabaseSettings bölümlerini iki yönlü (read/write) kontrol eder.
    /// Sunucu modundayken bağlı istemcileri PostgreSQL pg_stat_activity üzerinden izler.
    /// Pre-Login (giriş öncesi kurulum) ve Hot-Swap dinamik bağlantı yeteneklerine sahiptir.
    /// </summary>
    public partial class NetworkSettingsViewModel : ViewModelBase, IDisposable
    {
        #region Dependencies

        private readonly IConfiguration _configuration;
        private readonly IDatabaseConnectionProvider _connectionProvider;
        private readonly NetworkDiscoveryService _discoveryService;
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IToastService _toastService;
        private readonly ILogger<NetworkSettingsViewModel> _logger;
        private readonly IApplicationAuthorizationService _authorizationService;
        private readonly System.Windows.Threading.DispatcherTimer _refreshTimer;

        public void Dispose()
        {
            _refreshTimer.Stop();
        }

        #endregion

        #region Constructor

        public NetworkSettingsViewModel(
            IConfiguration configuration,
            IDatabaseConnectionProvider connectionProvider,
            NetworkDiscoveryService discoveryService,
            IDbContextFactory<AppDbContext> dbContextFactory,
            IToastService toastService,
            ILogger<NetworkSettingsViewModel> logger,
            IApplicationAuthorizationService authorizationService)
        {
            _configuration = configuration;
            _connectionProvider = connectionProvider;
            _discoveryService = discoveryService;
            _dbContextFactory = dbContextFactory;
            _toastService = toastService;
            _logger = logger;
            _authorizationService = authorizationService;

            ConnectedClients = new ObservableCollection<ConnectedClientModel>();
            DiscoveredServers = new ObservableCollection<DiscoveredServerInfo>();

            _refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _refreshTimer.Tick += async (s, e) =>
            {
                if (_isMainServer && _connectionProvider.IsConnected && _isAutoRefreshEnabled && !IsBusy)
                {
                    await LoadConnectedClientsAsync();
                }
            };

            // appsettings.json'dan mevcut değerleri yükle
            LoadSettingsFromConfig();

            // Çalışma anındaki durumu yansıt
            RefreshLiveStatus();

            // Sunucu modundaysa istemci listesini yükle ve timer'ı başlat
            if (_isMainServer)
            {
                _refreshTimer.Start();
                if (_connectionProvider.IsConnected)
                {
                    Task.Run(async () => await LoadConnectedClientsAsync());
                }
            }
        }

        #endregion

        #region Observable Properties

        private bool _isPreLoginMode = true;
        /// <summary>
        /// Giriş öncesi kurulum modunda mıyız? (LoginView üzerinden açıldığında RBAC kontrollerini bypass eder).
        /// </summary>
        public bool IsPreLoginMode
        {
            get => _isPreLoginMode;
            set => SetProperty(ref _isPreLoginMode, value);
        }

        private bool _isMainServer;
        /// <summary>
        /// Bu bilgisayar Ana Sunucu mu? (appsettings.json → NetworkDiscovery:IsMainServer)
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
                    OnPropertyChanged(nameof(IsManualIpEnabled));

                    if (value && !_refreshTimer.IsEnabled) _refreshTimer.Start();
                    else if (!value && _refreshTimer.IsEnabled) _refreshTimer.Stop();
                }
            }
        }

        private bool _isAutoRefreshEnabled = true;
        public bool IsAutoRefreshEnabled
        {
            get => _isAutoRefreshEnabled;
            set
            {
                if (SetProperty(ref _isAutoRefreshEnabled, value))
                {
                    if (value && _isMainServer) _refreshTimer.Start();
                    else _refreshTimer.Stop();
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
        /// Manuel sunucu IP adresi.
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
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    SaveAndRestartCommand.NotifyCanExecuteChanged();
                    SaveSettingsCommand.NotifyCanExecuteChanged();
                    ScanNetworkCommand.NotifyCanExecuteChanged();
                    TestConnectionCommand.NotifyCanExecuteChanged();
                    RefreshClientsCommand.NotifyCanExecuteChanged();
                }
            }
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

        private string _dbUsername = "postgres";
        /// <summary>
        /// PostgreSQL veritabanı kullanıcı adı.
        /// </summary>
        public string DbUsername
        {
            get => _dbUsername;
            set
            {
                if (SetProperty(ref _dbUsername, value))
                {
                    _hasUnsavedChanges = true;
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }

        private string _dbPassword = string.Empty;
        /// <summary>
        /// PostgreSQL veritabanı şifresi.
        /// </summary>
        public string DbPassword
        {
            get => _dbPassword;
            set
            {
                if (SetProperty(ref _dbPassword, value))
                {
                    _hasUnsavedChanges = true;
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }

        private string _dbName = "KamatekCrm";
        /// <summary>
        /// PostgreSQL veritabanı adı.
        /// </summary>
        public string DbName
        {
            get => _dbName;
            set
            {
                if (SetProperty(ref _dbName, value))
                {
                    _hasUnsavedChanges = true;
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }

        private int _configuredDbPort = 5432;
        public int ConfiguredDbPort
        {
            get => _configuredDbPort;
            set => SetProperty(ref _configuredDbPort, value);
        }

        private DiscoveredServerInfo? _selectedDiscoveredServer;
        public DiscoveredServerInfo? SelectedDiscoveredServer
        {
            get => _selectedDiscoveredServer;
            set
            {
                if (SetProperty(ref _selectedDiscoveredServer, value) && value != null)
                {
                    ManualServerIp = value.IpAddress;
                }
            }
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

        /// <summary>
        /// Ağ taramasında bulunan PostgreSQL sunucuları.
        /// </summary>
        public ObservableCollection<DiscoveredServerInfo> DiscoveredServers { get; }

        #endregion

        #region Commands

        private bool CanExecuteSettingsAction() => !IsBusy;

        /// <summary>
        /// Bulunan sunucuyu Host kutusuna atar.
        /// </summary>
        [RelayCommand]
        private void SelectDiscoveredServer(DiscoveredServerInfo? server)
        {
            if (server != null)
            {
                SelectedDiscoveredServer = server;
                ManualServerIp = server.IpAddress;
                _toastService.ShowInfo($"Sunucu seçildi: {server.DisplayText}");
            }
        }

        /// <summary>
        /// Ayarları appsettings.json'a kaydeder, bellekteki connection string'i günceller (Hot-Swap)
        /// ve gerekirse veritabanı migration & varsayılan admin oluşturma adımlarını çalıştırır.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExecuteSettingsAction))]
        private async Task SaveSettings()
        {
            await SaveSettingsCoreAsync(promptRestart: false);
        }

        /// <summary>
        /// Ayarları kaydeder ve uygulamayı .NET 9 Single-File uyumlu şekilde yeniden başlatır.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExecuteSettingsAction))]
        private async Task SaveAndRestart()
        {
            bool saved = await SaveSettingsCoreAsync(promptRestart: true);
            if (saved)
            {
                RestartApplication();
            }
        }

        /// <summary>
        /// Ortak kaydetme ve Hot-Swap çekirdeği.
        /// </summary>
        private async Task<bool> SaveSettingsCoreAsync(bool promptRestart)
        {
            // Pre-Login / Giriş öncesi değilse ve oturum açılmışsa RBAC yetki denetimi yap
            if (!_isPreLoginMode && App.CurrentUser != null)
            {
                var authorization = _authorizationService.Authorize(ApplicationPermission.AccessSettings);
                if (authorization.IsFailure)
                {
                    _toastService.ShowError(authorization.Error);
                    return false;
                }
            }

            IsBusy = true;
            try
            {
                string targetHost = _isMainServer
                    ? "127.0.0.1"
                    : (!string.IsNullOrWhiteSpace(_manualServerIp) ? _manualServerIp.SanitizeServerAddress() : "127.0.0.1");

                if (string.IsNullOrWhiteSpace(targetHost)) targetHost = "127.0.0.1";

                int targetPort = _configuredDbPort > 0 ? _configuredDbPort : 5432;
                string targetDb = string.IsNullOrWhiteSpace(_dbName) ? "kamatekcrm" : _dbName.Trim();
                string targetUser = string.IsNullOrWhiteSpace(_dbUsername) ? "postgres" : _dbUsername.Trim();
                string targetPassword = _dbPassword ?? string.Empty;

                // 1. appsettings.json dosyasına DPAPI korumasıyla yaz
                WriteSettingsToConfig();

                // 2. Properties.Settings ayarlarını güncelle
                Properties.Settings.Default.IsMainServer = _isMainServer;
                Properties.Settings.Default.IsMainServerManualOverride = true;
                Properties.Settings.Default.SavedServerAddress = targetHost;
                Properties.Settings.Default.Save();

                // 3. Hot-Swap: Bellekteki aktif connection provider ve string'i anında güncelle
                var activeConnBuilder = new Npgsql.NpgsqlConnectionStringBuilder
                {
                    Host = targetHost,
                    Port = targetPort,
                    Database = targetDb,
                    Username = targetUser,
                    Password = targetPassword
                };
                string activeConnectionString = activeConnBuilder.ConnectionString;

                _connectionProvider.UpdateConnectionString(activeConnectionString);
                _connectionProvider.SetServerIp(targetHost);
                _connectionProvider.SetConnectionState(true);

                // 4. İlk kurulum veritabanı migration & Admin seeder kontrolü
                try
                {
                    var dbInitializer = App.ServiceProvider?.GetService<IDatabaseInitializationService>();
                    if (dbInitializer != null)
                    {
                        var initResult = await dbInitializer.InitializeAsync();
                        if (initResult.AdminCreated)
                        {
                            MessageBox.Show(
                                $"İlk yönetici hesabı oluşturuldu.\n\nKullanıcı adı: admin\nGeçici şifre: {initResult.TemporaryAdminPassword}\n\nBu şifre yalnızca bir kez gösterilir. Giriş yaptıktan sonra şifrenizi güncelleyin.",
                                "İlk Kurulum Tamamlandı",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception migEx)
                {
                    _logger.LogWarning(migEx, "İlk kurulum şema migration'ı sırasında uyarı alındı: {Message}", migEx.Message);
                }

                // 5. Restored event'i yayınla
                try
                {
                    EventAggregator.Instance?.Publish(new DatabaseConnectionRestoredEvent());
                }
                catch { }

                _hasUnsavedChanges = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                IsConnectionHealthy = true;
                ConnectionStatus = $"Bağlantı güncellendi ve kaydedildi → {targetHost}:{targetPort}";

                _toastService.ShowSuccess("Ağ ve veritabanı ayarları başarıyla kaydedildi (Hot-Swap uygulandı).");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ağ ayarları kaydedilirken hata oluştu.");
                _toastService.ShowError($"Ayarlar kaydedilemedi: {ex.Message}");
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Ağ keşfini uygulama yeniden başlatılmadan manuel olarak tetikler.
        /// Yerel alt ağı SemaphoreSlim(32) ile paralel tarar ve 5432 portu açık sunucuları listeler.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExecuteSettingsAction))]
        private async Task ScanNetwork()
        {
            IsBusy = true;
            ConnectionStatus = "Ağdaki PostgreSQL sunucuları taranıyor (5432 portu)...";
            DiscoveredServers.Clear();

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var servers = await _discoveryService.ScanSubnetServersAsync(server =>
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        if (!DiscoveredServers.Any(s => s.IpAddress == server.IpAddress))
                        {
                            DiscoveredServers.Add(server);
                        }
                    });
                }, cts.Token);

                if (DiscoveredServers.Count > 0)
                {
                    ConnectionStatus = $"{DiscoveredServers.Count} sunucu bulundu. Bağlanmak için listeden seçin.";
                    _toastService.ShowSuccess($"{DiscoveredServers.Count} adet sunucu bulundu.");
                    if (string.IsNullOrWhiteSpace(ManualServerIp))
                    {
                        SelectedDiscoveredServer = DiscoveredServers[0];
                        ManualServerIp = DiscoveredServers[0].IpAddress;
                    }
                }
                else
                {
                    ConnectionStatus = "Ağda açık PostgreSQL sunucusu bulunamadı.";
                    _toastService.ShowWarning("Ağda PostgreSQL sunucusu bulunamadı. Lütfen sunucu IP adresini manuel girin.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ağ taraması sırasında hata.");
                ConnectionStatus = $"Tarama hatası: {ex.Message}";
                _toastService.ShowError($"Ağ tarama hatası: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Connection Doctor: Adım adım bağlantı teşhisi.
        /// Adım 1: TcpClient ile Host:Port erişim testi (Firewall / Servis kontrolü).
        /// Adım 2: NpgsqlConnection ile veritabanı ve kimlik doğrulama testi.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExecuteSettingsAction))]
        private async Task TestConnection()
        {
            string targetHost = _isMainServer
                ? "127.0.0.1"
                : (!string.IsNullOrWhiteSpace(_manualServerIp) ? _manualServerIp.SanitizeServerAddress() : "127.0.0.1");

            if (string.IsNullOrWhiteSpace(targetHost))
            {
                _toastService.ShowError("Lütfen bir sunucu IP adresi girin.");
                ConnectionStatus = "Hata: Sunucu IP adresi boş.";
                return;
            }

            int targetPort = _configuredDbPort > 0 ? _configuredDbPort : 5432;
            string targetDb = string.IsNullOrWhiteSpace(_dbName) ? "kamatekcrm" : _dbName.Trim();
            string targetUser = string.IsNullOrWhiteSpace(_dbUsername) ? "postgres" : _dbUsername.Trim();
            string targetPassword = _dbPassword ?? string.Empty;

            IsBusy = true;
            ConnectionStatus = $"Adım 1/2: {targetHost}:{targetPort} portuna erişim test ediliyor...";

            try
            {
                // Adım 1: TCP Port Kontrolü
                using (var tcpClient = new TcpClient())
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    try
                    {
                        await tcpClient.ConnectAsync(targetHost, targetPort, cts.Token);
                    }
                    catch
                    {
                        IsConnectionHealthy = false;
                        ConnectionStatus = $"Erişim Hatası: {targetHost}:{targetPort} portuna ulaşılamıyor.";
                        _toastService.ShowError("Sunucu IP adresine veya 5432 portuna ulaşılamıyor. Ana bilgisayarda PostgreSQL servisinin çalıştığından ve Güvenlik Duvarı (Firewall) izninden emin olun.");
                        return;
                    }
                }

                // Adım 2: Npgsql Veritabanı ve Kimlik Doğrulama
                ConnectionStatus = "Adım 2/2: Veritabanı kimlik doğrulaması test ediliyor...";
                var testConnBuilder = new Npgsql.NpgsqlConnectionStringBuilder
                {
                    Host = targetHost,
                    Port = targetPort,
                    Database = targetDb,
                    Username = targetUser,
                    Password = targetPassword,
                    Timeout = 5,
                    CommandTimeout = 5,
                    Pooling = false
                };

                await using var conn = new Npgsql.NpgsqlConnection(testConnBuilder.ConnectionString);
                using var dbCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                try
                {
                    await conn.OpenAsync(dbCts.Token);
                    IsConnectionHealthy = true;
                    ConnectionStatus = $"Bağlantı başarılı: {targetHost} (PostgreSQL {conn.ServerVersion})";
                    _toastService.ShowSuccess("Bağlantı testi başarılı!");

                    _connectionProvider.SetServerIp(targetHost);
                    _connectionProvider.SetConnectionState(true);
                    Properties.Settings.Default.SavedServerAddress = targetHost;
                    Properties.Settings.Default.Save();
                }
                catch (Npgsql.PostgresException pgEx)
                {
                    if (pgEx.SqlState == "28P01") // invalid_password
                    {
                        IsConnectionHealthy = false;
                        ConnectionStatus = "Kimlik Doğrulama Hatası: Kullanıcı adı veya şifre geçersiz.";
                        _toastService.ShowError("Sunucuya ulaşıldı ancak veritabanı kullanıcı adı veya şifresi hatalı.");
                    }
                    else if (pgEx.SqlState == "3D000") // invalid_catalog_name (veritabanı henüz oluşturulmamış)
                    {
                        IsConnectionHealthy = true;
                        ConnectionStatus = $"Sunucu ve kimlik doğrulandı. '{targetDb}' veritabanı kayıt sırasında oluşturulacaktır.";
                        _toastService.ShowSuccess("Sunucuya ve kimliğe erişildi. Veritabanı kaydetme sırasında otomatik oluşturulacaktır.");
                    }
                    else
                    {
                        IsConnectionHealthy = false;
                        ConnectionStatus = $"PostgreSQL Hatası [{pgEx.SqlState}]: {pgEx.MessageText}";
                        _toastService.ShowError($"PostgreSQL Hatası: {pgEx.MessageText}");
                    }
                }
                catch (Exception ex)
                {
                    IsConnectionHealthy = false;
                    ConnectionStatus = $"Bağlantı Hatası: {ex.Message}";
                    _toastService.ShowError($"Bağlantı kurulamadı: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bağlantı testi sırasında genel hata.");
                IsConnectionHealthy = false;
                ConnectionStatus = $"Test Hatası: {ex.Message}";
                _toastService.ShowError($"Test Hatası: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Sunucu modundayken bağlı istemcileri PostgreSQL pg_stat_activity üzerinden yeniler.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExecuteSettingsAction))]
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
        /// appsettings.json'dan NetworkDiscovery ve DatabaseSettings bölümündeki değerleri okur.
        /// </summary>
        private void LoadSettingsFromConfig()
        {
            try
            {
                _isMainServer = _configuration.GetValue<bool>("NetworkDiscovery:IsMainServer", false);
                _isAutoDiscoveryEnabled = _configuration.GetValue<bool>("NetworkDiscovery:Enabled", true);
                _configuredPort = _configuration.GetValue<int>("NetworkDiscovery:Port", 54321);
                _configuredTimeout = _configuration.GetValue<int>("NetworkDiscovery:TimeoutSeconds", 15);

                _manualServerIp = Properties.Settings.Default.SavedServerAddress ?? string.Empty;

                // PostgreSQL Veritabanı ayarlarını yükle
                string connStr = _configuration.GetConnectionString("PostgreSQL")
                    ?? _configuration["DatabaseSettings:ConnectionStrings:PostgreSQL"]
                    ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    try
                    {
                        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connStr);
                        if (!string.IsNullOrWhiteSpace(builder.Host) && builder.Host != "127.0.0.1" && string.IsNullOrWhiteSpace(_manualServerIp))
                        {
                            _manualServerIp = builder.Host;
                        }
                        if (!string.IsNullOrWhiteSpace(builder.Username))
                            _dbUsername = builder.Username;
                        _dbPassword = Security.DataProtectionHelper.Unprotect(builder.Password ?? string.Empty);
                        if (!string.IsNullOrWhiteSpace(builder.Database))
                            _dbName = builder.Database;
                        if (builder.Port > 0)
                            _configuredDbPort = builder.Port;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "PostgreSQL bağlantı dizesi ayrıştırılamadı.");
                    }
                }

                OnPropertyChanged(nameof(IsMainServer));
                OnPropertyChanged(nameof(IsAutoDiscoveryEnabled));
                OnPropertyChanged(nameof(ManualServerIp));
                OnPropertyChanged(nameof(ConfiguredPort));
                OnPropertyChanged(nameof(ConfiguredTimeout));
                OnPropertyChanged(nameof(DbUsername));
                OnPropertyChanged(nameof(DbPassword));
                OnPropertyChanged(nameof(DbName));
                OnPropertyChanged(nameof(ConfiguredDbPort));
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
        /// Değişiklikleri appsettings.json dosyasına DPAPI ile şifrelenmiş biçimde yazar.
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

            // 1. NetworkDiscovery bölümünü güncelle
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

            // 2. PostgreSQL Connection String oluştur ve kaydet
            string targetHost = _isMainServer
                ? "127.0.0.1"
                : (!string.IsNullOrWhiteSpace(_manualServerIp) ? _manualServerIp.SanitizeServerAddress() : "127.0.0.1");
            if (string.IsNullOrWhiteSpace(targetHost)) targetHost = "127.0.0.1";

            int targetPort = _configuredDbPort > 0 ? _configuredDbPort : 5432;

            var connBuilder = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = targetHost,
                Port = targetPort,
                Database = string.IsNullOrWhiteSpace(_dbName) ? "kamatekcrm" : _dbName.Trim(),
                Username = string.IsNullOrWhiteSpace(_dbUsername) ? "postgres" : _dbUsername.Trim(),
                Password = Security.DataProtectionHelper.Protect(_dbPassword ?? string.Empty)
            };
            string newConnectionString = connBuilder.ConnectionString;

            var dbSection = jsonObject["DatabaseSettings"];
            if (dbSection == null)
            {
                jsonObject["DatabaseSettings"] = new JsonObject();
                dbSection = jsonObject["DatabaseSettings"];
            }

            var connStringsSection = dbSection["ConnectionStrings"];
            if (connStringsSection == null)
            {
                dbSection["ConnectionStrings"] = new JsonObject();
                connStringsSection = dbSection["ConnectionStrings"];
            }

            connStringsSection["PostgreSQL"] = newConnectionString;

            if (jsonObject["ConnectionStrings"] != null)
            {
                jsonObject["ConnectionStrings"]!["PostgreSQL"] = newConnectionString;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string updatedJson = jsonObject.ToJsonString(options);

            File.WriteAllText(appSettingsPath, updatedJson);

            // Dev ortamı için proje dizinindeki appsettings.json dosyasına da yaz
            string? projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                string devPath = Path.Combine(projectRoot, "appsettings.json");
                if (File.Exists(devPath) && !string.Equals(devPath, appSettingsPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(devPath, updatedJson);
                }
            }
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

            return buildPath;
        }

        /// <summary>
        /// .NET 9 Single-File executable uyumlu şekilde uygulamayı yeniden başlatır.
        /// </summary>
        private static void RestartApplication()
        {
            try
            {
                string? processPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = processPath,
                        UseShellExecute = true
                    });
                    Application.Current.Shutdown();
                    return;
                }

                // Fallback: ResourceAssembly.Location kontrolü
                var appPath = Application.ResourceAssembly.Location;
                if (!string.IsNullOrEmpty(appPath))
                {
                    if (appPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        appPath = appPath.Substring(0, appPath.Length - 4) + ".exe";

                    if (File.Exists(appPath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = appPath,
                            UseShellExecute = true
                        });
                        Application.Current.Shutdown();
                        return;
                    }
                }

                MessageBox.Show(
                    "Uygulama yeniden başlatılamadı. Lütfen uygulamayı manuel olarak kapatıp açın.",
                    "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Uygulama yeniden başlatılırken bir hata oluştu: {ex.Message}\nLütfen uygulamayı manuel başlatın.",
                    "Yeniden Başlatma", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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

                Application.Current?.Dispatcher?.Invoke(() =>
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
                Application.Current?.Dispatcher?.Invoke(() =>
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
