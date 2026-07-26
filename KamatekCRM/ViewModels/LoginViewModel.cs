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
    public partial class LoginViewModel : ViewModelBase
    {
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isLoading;
        private bool _rememberMe;

        [ObservableProperty]
        private bool _isSearchingForServer;

        partial void OnIsSearchingForServerChanged(bool value)
        {
            LoginCommand.NotifyCanExecuteChanged();
        }

        [ObservableProperty]
        private string _serverStatusMessage = string.Empty;

        [ObservableProperty]
        private bool _isServerFound;

        /// <summary>
        /// Kullanici adi
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
        /// Sifre (code-behind'dan set edilir)
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
        /// Beni Hatirla secenegi
        /// </summary>
        public bool RememberMe
        {
            get => _rememberMe;
            set => SetProperty(ref _rememberMe, value);
        }

        /// <summary>
        /// Hata mesaji
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
        /// Yukleniyor durumu
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                SetProperty(ref _isLoading, value);
                LoginCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Hata mesaji gorunurlugu
        /// </summary>
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);



        private readonly IAuthService _authService;
        private readonly NavigationService _navigationService;
        private readonly NetworkDiscoveryService _discoveryService;
        private readonly IToastService _toastService;
        private readonly IDatabaseConnectionProvider _connectionProvider;

        /// <summary>
        /// Constructor
        /// </summary>
        public LoginViewModel(IAuthService authService, NavigationService navigationService, NetworkDiscoveryService discoveryService, IToastService toastService, IDatabaseConnectionProvider connectionProvider)
        {
            _authService = authService;
            _navigationService = navigationService;
            _discoveryService = discoveryService;
            _toastService = toastService;
            _connectionProvider = connectionProvider;

            // Gerçek zamanlı sunucu durumunu dinle
            EventAggregator.Instance.Subscribe<DatabaseConnectionRestoredEvent>(_ =>
            {
                if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsServerFound = true;
                        IsSearchingForServer = false;
                        ServerStatusMessage = $"Sunucu bulundu ({_connectionProvider.CurrentServerIp})";
                        LoginCommand.NotifyCanExecuteChanged();
                    });
                }
            });

            EventAggregator.Instance.Subscribe<DatabaseConnectionLostEvent>(_ =>
            {
                if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsServerFound = false;
                        IsSearchingForServer = false;
                        ServerStatusMessage = "Sunucu bulunamadı! Lütfen ağ ayarlarını yapılandırın.";
                        LoginCommand.NotifyCanExecuteChanged();
                    });
                }
            });
            
            // Load saved settings, then apply dev defaults if empty
            LoadSavedCredentials();
            
            // Development: Inject default credentials so user doesn't have to type every time
            if (string.IsNullOrWhiteSpace(Username))
            {
                Username = "admin";
            }
            if (string.IsNullOrWhiteSpace(Password))
            {
                Password = "123";
            }
            
            _ = InitializeDiscoveryAsync();
        }

        [ObservableProperty]
        private bool _isServerNotFound;

        public async Task InitializeDiscoveryAsync()
        {
            if (_connectionProvider.IsConnected)
            {
                IsServerFound = true;
                IsSearchingForServer = false;
                IsServerNotFound = false;
                ServerStatusMessage = $"Sunucu bulundu ({_connectionProvider.CurrentServerIp})";
                LoginCommand.NotifyCanExecuteChanged();
            }
            else
            {
                IsSearchingForServer = true;
                IsServerNotFound = false;
                ServerStatusMessage = "Ağda sunucu aranıyor...";
                IsServerFound = false;
                LoginCommand.NotifyCanExecuteChanged();

                // Start Timeout Fallback
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(7));
                    if (IsSearchingForServer && !_connectionProvider.IsConnected)
                    {
                        if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                IsSearchingForServer = false;
                                IsServerNotFound = true;
                                ServerStatusMessage = "Sunucuya bağlanılamadı. Lütfen ayarları kontrol edin.";
                                LoginCommand.NotifyCanExecuteChanged();
                            });
                        }
                    }
                });
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private void OpenConnectionSettings()
        {
            var networkViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<KamatekCrm.ViewModels.NetworkSettingsViewModel>(App.ServiceProvider);
            var settingsWindow = new System.Windows.Window
            {
                Title = "Ağ ve Bağlantı Ayarları",
                Content = new KamatekCrm.Views.NetworkSettingsView { DataContext = networkViewModel },
                Width = 600,
                Height = 500,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                ResizeMode = System.Windows.ResizeMode.NoResize
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

        /// <summary>
        /// Giris yapilabilir mi kontrolu
        /// </summary>
        private bool CanLogin()
        {
            return !string.IsNullOrWhiteSpace(Username) && !IsLoading && !IsSearchingForServer;
        }

        /// <summary>
        /// Kayitli giris bilgilerini yukle
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
                _toastService.ShowError($"Kayitli bilgiler yuklenemedi: {ex.Message}");
            }
        }

        /// <summary>
        /// Giris basarili olunca bilgileri kaydet
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
                _toastService.ShowError($"Kayit bilgileri saklanamadi: {ex.Message}");
            }
        }

        /// <summary>
        /// Giris islemini gerceklestir — API uzerinden
        /// </summary>
        public async Task ExecuteLoginAsync(object? parameter = null)
        {
            if (!_connectionProvider.IsConnected)
            {
                ErrorMessage = "Sunucu bağlantısı yok. Lütfen ağ ayarlarını kontrol edin.";
                _toastService.ShowError("Sunucu bağlantısı sağlanamadı!");
                return;
            }

            // Determine if we are auto-logging in with a token
            string? autoToken = parameter as string;

            if (string.IsNullOrEmpty(autoToken) && (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password)))
            {
                 if(string.IsNullOrWhiteSpace(Username)) ErrorMessage = "Kullanici adi gerekli!";
                 else if(string.IsNullOrWhiteSpace(Password)) ErrorMessage = "Sifre gerekli!";
                 return;
            }

            ErrorMessage = string.Empty;
            IsLoading = true;

            try
            {
                bool isAuthenticated = false;

                if (!string.IsNullOrEmpty(autoToken))
                {
                    // Token Login (pre-existing session)
                    if (!string.IsNullOrEmpty(Username))
                    {
                         isAuthenticated = true;
                    }
                }
                else
                {
                    // API-based Login via AuthService
                    if (await _authService.LoginAsync(Username, Password))
                    {
                        isAuthenticated = true;
                        // Set global current user
                        App.CurrentUser = _authService.CurrentUser;
                    }
                    else
                    {
                        ErrorMessage = "Hatali kullanici adi veya sifre!";
                    }
                }

                if (isAuthenticated)
                {
                    // Basarili giris - Ayarlari kaydet
                    SaveCredentials(null);
                    
                    // Ana icerigi gec
                    _navigationService.NavigateToMainContent();
                }
                else
                {
                     if(string.IsNullOrEmpty(ErrorMessage)) ErrorMessage = "Giris basarisiz.";
                     Password = string.Empty;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Giris hatasi: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
