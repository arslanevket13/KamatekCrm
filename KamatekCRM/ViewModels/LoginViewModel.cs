using KamatekCrm.Helpers;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Services;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Login ekrani ViewModel (UserControl icin)
    /// </summary>
    public partial class LoginViewModel : ViewModelBase, IDisposable
    {
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isLoading;
        private bool _rememberMe;
        private bool _disposed;
        private IDisposable? _connectionRestoredSubscription;
        private IDisposable? _connectionLostSubscription;

        [ObservableProperty]
        private bool _isSearchingForServer;

        partial void OnIsSearchingForServerChanged(bool value)
        {
            OnPropertyChanged(nameof(LoginButtonText));
            LoginCommand.NotifyCanExecuteChanged();
            DevQuickLoginCommand.NotifyCanExecuteChanged();
        }

        [ObservableProperty]
        private string _serverStatusMessage = "Sunucu aranıyor...";

        [ObservableProperty]
        private bool _isServerFound;

        [ObservableProperty]
        private bool _isCapsLockOn;

#if DEBUG
        private static bool _hasTriggeredDevAutoLogin;

        public bool IsDebugQuickLoginVisible => true;
#else
        public bool IsDebugQuickLoginVisible => false;
#endif

        public string LoginButtonText => IsLoading
            ? "Giriş yapılıyor..."
            : (IsSearchingForServer ? "Sunucu kontrol ediliyor..." : "Sisteme Giriş Yap");

        /// <summary>
        /// Kullanıcı adı
        /// </summary>
        public string Username
        {
            get => _username;
            set
            {
                SetProperty(ref _username, value);
                ErrorMessage = string.Empty;
                LoginCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Şifre
        /// </summary>
        public string Password
        {
            get => _password;
            set
            {
                SetProperty(ref _password, value);
                ErrorMessage = string.Empty;
                LoginCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Beni Hatırla seçeneği
        /// </summary>
        public bool RememberMe
        {
            get => _rememberMe;
            set => SetProperty(ref _rememberMe, value);
        }

        /// <summary>
        /// Hata mesajı
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                SetProperty(ref _errorMessage, value);
                OnPropertyChanged(nameof(HasError));
            }
        }

        /// <summary>
        /// Yükleniyor durumu
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(LoginButtonText));
                    LoginCommand.NotifyCanExecuteChanged();
                    DevQuickLoginCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Hata mesajı görünürlüğü
        /// </summary>
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        private readonly IAuthService _authService;
        private readonly NavigationService _navigationService;
        private readonly NetworkDiscoveryService _discoveryService;
        private readonly IToastService _toastService;
        private readonly IDatabaseConnectionProvider _connectionProvider;
        private readonly IForcedPasswordChangeService _forcedPasswordChangeService;
        private readonly IRecoveryKeyService _recoveryKeyService;

        /// <summary>
        /// Constructor
        /// </summary>
        public LoginViewModel(
            IAuthService authService,
            NavigationService navigationService,
            NetworkDiscoveryService discoveryService,
            IToastService toastService,
            IDatabaseConnectionProvider connectionProvider,
            IForcedPasswordChangeService forcedPasswordChangeService,
            IRecoveryKeyService recoveryKeyService)
        {
            _authService = authService;
            _navigationService = navigationService;
            _discoveryService = discoveryService;
            _toastService = toastService;
            _connectionProvider = connectionProvider;
            _forcedPasswordChangeService = forcedPasswordChangeService;
            _recoveryKeyService = recoveryKeyService;

            // Gerçek zamanlı sunucu durumunu dinle
            _connectionRestoredSubscription = EventAggregator.Instance.Subscribe<DatabaseConnectionRestoredEvent>(_ =>
            {
                var serverLabel = string.IsNullOrWhiteSpace(_connectionProvider.CurrentServerIp)
                    ? "Yerel Sunucu"
                    : _connectionProvider.CurrentServerIp;
                SetStartupStatus($"Sunucu bağlantısı hazır ({serverLabel})", isSearching: false, isFound: true, isNotFound: false);
#if DEBUG
                TryScheduleDevAutoLoginIfEnabled();
#endif
            });

            _connectionLostSubscription = EventAggregator.Instance.Subscribe<DatabaseConnectionLostEvent>(_ =>
            {
                SetStartupStatus("Sunucu bulunamadı! Lütfen ağ ayarlarını yapılandırın.", isSearching: false, isFound: false, isNotFound: true);
            });

            // Kayıtlı bilgileri yükle
            LoadSavedCredentials();

            InitializeDiscoveryAsync().SafeFireAndForget("LoginViewModel.InitializeDiscoveryAsync");
        }

        [ObservableProperty]
        private bool _isServerNotFound;

        /// <summary>
        /// Açılış bootstrap ve bağlantı olaylarından gelen canlı durum bilgisini UI thread'de günceller.
        /// </summary>
        public void SetStartupStatus(string message, bool isSearching, bool isFound, bool isNotFound)
        {
            if (_disposed) return;

            void Apply()
            {
                ServerStatusMessage = message;
                IsSearchingForServer = isSearching;
                IsServerFound = isFound;
                IsServerNotFound = isNotFound;
                LoginCommand.NotifyCanExecuteChanged();
                DevQuickLoginCommand.NotifyCanExecuteChanged();
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(Apply);
            }
            else
            {
                Apply();
            }
        }

        public async Task InitializeDiscoveryAsync()
        {
            if (string.IsNullOrWhiteSpace(_connectionProvider.GetConnectionString()))
            {
                SetStartupStatus(
                    "Veritabanı bağlantısı yapılandırılmamış. Ağ Ayarlarından bağlantı kurabilirsiniz.",
                    isSearching: false,
                    isFound: false,
                    isNotFound: true);
                return;
            }

            if (_connectionProvider.IsConnected)
            {
                var serverLabel = string.IsNullOrWhiteSpace(_connectionProvider.CurrentServerIp)
                    ? "Yerel Sunucu"
                    : _connectionProvider.CurrentServerIp;
                SetStartupStatus($"Sunucu bulundu ({serverLabel})", isSearching: false, isFound: true, isNotFound: false);
#if DEBUG
                TryScheduleDevAutoLoginIfEnabled();
#endif
            }
            else
            {
                SetStartupStatus("Sunucu aranıyor...", isSearching: true, isFound: false, isNotFound: false);

                // 5 saniyelik zaman aşımı geri düşüşü
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    if (!_disposed && IsSearchingForServer && !_connectionProvider.IsConnected)
                    {
                        SetStartupStatus(
                            "Sunucuya 5 sn içinde ulaşılamadı. Ağ Ayarlarından bağlantıyı yapılandırabilirsiniz.",
                            isSearching: false,
                            isFound: false,
                            isNotFound: true);
                    }
                });
            }

            await Task.CompletedTask;
        }

        [RelayCommand]
        private void ForgotPassword()
        {
            var recoveryWindow = new KamatekCrm.Views.PasswordRecoveryWindow(
                _recoveryKeyService,
                _authService,
                _toastService);

            var owner = System.Windows.Application.Current?.MainWindow;
            if (owner is not null)
            {
                recoveryWindow.Owner = owner;
            }
            recoveryWindow.ShowDialog();
        }

        [RelayCommand]
        private void OpenConnectionSettings()
        {
            var networkViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<KamatekCrm.ViewModels.NetworkSettingsViewModel>(App.ServiceProvider);
            networkViewModel.IsPreLoginMode = true; // Pre-login / Bootstrap modunu etkinleştir

            var parentWindow = System.Windows.Application.Current?.MainWindow;
            var workArea = SystemParameters.WorkArea;
            var settingsWindow = new System.Windows.Window
            {
                Title = "Ağ ve Bağlantı Ayarları",
                Content = new KamatekCrm.Views.NetworkSettingsView { DataContext = networkViewModel },
                Width = Math.Min(1100, workArea.Width * 0.92),
                Height = Math.Min(740, workArea.Height * 0.90),
                MinWidth = 860,
                MinHeight = 600,
                Owner = parentWindow,
                WindowStartupLocation = parentWindow != null ? System.Windows.WindowStartupLocation.CenterOwner : System.Windows.WindowStartupLocation.CenterScreen,
                ResizeMode = System.Windows.ResizeMode.CanResize
            };

            settingsWindow.Closed += (s, e) =>
            {
                networkViewModel.Dispose();
                InitializeDiscoveryAsync().SafeFireAndForget("LoginViewModel.InitializeDiscoveryAsync");
            };

            settingsWindow.ShowDialog();
        }

        /// <summary>
        /// Source Generator: Bu metottan otomatik olarak 'LoginCommand' ICommand özelliği üretilir.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanLogin))]
        private async Task Login(object? param)
        {
            await ExecuteLoginAsync(param);
        }

        [RelayCommand(CanExecute = nameof(CanDevQuickLogin))]
        private Task DevQuickLoginAsync()
        {
#if DEBUG
            return ExecuteDevQuickLoginAsync();
#else
            return Task.CompletedTask;
#endif
        }

        /// <summary>
        /// Giriş yapılabilir mi kontrolü
        /// </summary>
        private bool CanLogin()
        {
            return !string.IsNullOrWhiteSpace(Username) && !IsLoading && !IsSearchingForServer;
        }

        private bool CanDevQuickLogin()
        {
#if DEBUG
            return !IsLoading && !IsSearchingForServer;
#else
            return false;
#endif
        }

        /// <summary>
        /// Kayıtlı giriş bilgilerini yükle
        /// </summary>
        private void LoadSavedCredentials()
        {
            try
            {
                var props = Properties.Settings.Default;
                if (props.RememberMe)
                {
                    Username = props.SavedUsername;
                    RememberMe = true;
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Kayıtlı bilgiler yüklenemedi: {ex.Message}");
            }
        }

        /// <summary>
        /// Giriş başarılı olunca bilgileri kaydet
        /// </summary>
        private void SaveCredentials(string? token)
        {
            try
            {
                var props = Properties.Settings.Default;
                props.RememberMe = RememberMe;
                props.SavedUsername = RememberMe ? Username : string.Empty;
                props.Save();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Kayıt bilgileri saklanamadı: {ex.Message}");
            }
        }

        /// <summary>
        /// Giriş işlemini gerçekleştir
        /// </summary>
        public async Task ExecuteLoginAsync(object? parameter = null)
        {
            if (!_connectionProvider.IsConnected)
            {
                ErrorMessage = "Sunucu bağlantısı yok. Lütfen ağ ayarlarını kontrol edin.";
                IsServerNotFound = true;
                _toastService.ShowError("Sunucu bağlantısı sağlanamadı!");
                return;
            }

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                if (string.IsNullOrWhiteSpace(Username)) ErrorMessage = "Kullanıcı adı gerekli!";
                else if (string.IsNullOrWhiteSpace(Password)) ErrorMessage = "Şifre gerekli!";
                return;
            }

            ErrorMessage = string.Empty;
            IsLoading = true;

            try
            {
                bool isAuthenticated = false;

                // Masaüstü sürümünde API/token oturumu kullanılmıyor. Her giriş yerel
                // kimlik bilgileriyle doğrulanır; parametreyle kimlik atlama yapılmaz.
                if (await _authService.LoginAsync(Username, Password))
                {
                    isAuthenticated = true;
                }
                else
                {
                    ErrorMessage = "Hatalı kullanıcı adı veya şifre!";
                }

                if (isAuthenticated)
                {
                    await CompleteLoginSuccessAsync(saveCredentials: true);
                }
                else
                {
                    if (string.IsNullOrEmpty(ErrorMessage)) ErrorMessage = "Giriş başarısız.";
                    Password = string.Empty;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                Password = string.Empty;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CompleteLoginSuccessAsync(bool saveCredentials)
        {
            App.CurrentUser = _authService.CurrentUser;

            if (_authService.CurrentUser?.MustChangePassword == true)
            {
                var changed = await _forcedPasswordChangeService.RequireChangeAsync(_authService.CurrentUser);
                if (!changed)
                {
                    _authService.Logout();
                    App.CurrentUser = null;
                    ErrorMessage = "Devam etmek için geçici parolanızı değiştirmeniz gerekir.";
                    Password = string.Empty;
                    return;
                }

                _authService.CurrentUser.MustChangePassword = false;
            }

            if (saveCredentials)
            {
                SaveCredentials(null);
            }

            _navigationService.NavigateToMainContent();
        }

#if DEBUG
        private void TryScheduleDevAutoLoginIfEnabled()
        {
            if (_disposed || _hasTriggeredDevAutoLogin || _authService.HasLoggedOutManually)
            {
                return;
            }

            var autoLoginEnabled = string.Equals(
                KamatekCrm.Settings.AppSettings.Configuration["DevQuickLogin:AutoLogin"],
                "true",
                StringComparison.OrdinalIgnoreCase);

            if (!autoLoginEnabled || !_connectionProvider.IsConnected)
            {
                return;
            }

            _hasTriggeredDevAutoLogin = true;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.InvokeAsync(() =>
                    ExecuteDevQuickLoginAsync().SafeFireAndForget("LoginViewModel.DevAutoLogin"));
            }
            else
            {
                ExecuteDevQuickLoginAsync().SafeFireAndForget("LoginViewModel.DevAutoLogin");
            }
        }

        private async Task ExecuteDevQuickLoginAsync()
        {
            if (IsLoading || _disposed)
            {
                return;
            }

            if (!_connectionProvider.IsConnected)
            {
                ErrorMessage = "Sunucu bağlantısı yok. Lütfen ağ ayarlarını kontrol edin.";
                IsServerNotFound = true;
                _toastService.ShowError("Sunucu bağlantısı sağlanamadı!");
                return;
            }

            ErrorMessage = string.Empty;
            IsLoading = true;

            try
            {
                var preferredUsername = KamatekCrm.Settings.AppSettings.Configuration["DevQuickLogin:PreferredUsername"];
                if (await _authService.DevQuickLoginAsAdminAsync(preferredUsername))
                {
                    await CompleteLoginSuccessAsync(saveCredentials: false);
                }
                else
                {
                    ErrorMessage = "Hızlı Admin girişi başarısız oldu.";
                    _toastService.ShowError(ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "DEV-ONLY hızlı admin girişi başarısız oldu.");
                ErrorMessage = ex.Message;
                _toastService.ShowError(ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }
#endif

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _connectionRestoredSubscription?.Dispose();
            _connectionLostSubscription?.Dispose();
            _connectionRestoredSubscription = null;
            _connectionLostSubscription = null;
            GC.SuppressFinalize(this);
        }
    }
}
