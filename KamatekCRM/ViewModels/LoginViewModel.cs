using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using KamatekCrm.Commands;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Login ekranı ViewModel (UserControl için)
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
            CommandManager.InvalidateRequerySuggested();
        }

        [ObservableProperty]
        private string _serverStatusMessage = string.Empty;

        [ObservableProperty]
        private bool _isServerFound;

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
            }
        }

        /// <summary>
        /// Şifre (code-behind'dan set edilir)
        /// </summary>
        public string Password
        {
            get => _password;
            set
            {
                SetProperty(ref _password, value);
                ErrorMessage = string.Empty;
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
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Hata mesajı görünürlüğü
        /// </summary>
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Giriş komutu
        /// </summary>
        public ICommand LoginCommand { get; }

        private readonly IAuthService _authService;
        private readonly NavigationService _navigationService;
        private readonly NetworkDiscoveryService _discoveryService;
        private readonly ApiClient _apiClient;

        /// <summary>
        /// Constructor
        /// </summary>
        public LoginViewModel(IAuthService authService, NavigationService navigationService, NetworkDiscoveryService discoveryService, ApiClient apiClient)
        {
            _authService = authService;
            _navigationService = navigationService;
            _discoveryService = discoveryService;
            _apiClient = apiClient;
            LoginCommand = new RelayCommand(async param => await ExecuteLoginAsync(param), _ => CanLogin());
            
            // Load saved settings
            LoadSavedCredentials();
            
            _ = InitializeDiscoveryAsync();
        }

        public async Task InitializeDiscoveryAsync()
        {
            IsSearchingForServer = true;
            ServerStatusMessage = "Ağda Sunucu Aranıyor...";
            IsServerFound = false;

            var server = await _discoveryService.DiscoverServerAsync(5000);

            if (server != null && !string.IsNullOrWhiteSpace(server.ApiUrl))
            {
                _apiClient.SetBaseUrl(server.ApiUrl);
                IsServerFound = true;
                ServerStatusMessage = $"Sunucu Bulundu: {server.ApiUrl}";
            }
            else
            {
                IsServerFound = false;
                ServerStatusMessage = "Sunucu bulunamadı. Lütfen manuel IP girin veya localhost kullanın.";
            }

            IsSearchingForServer = false;
        }

        // Default constructor REMOVED as we heavily rely on DI now and manual instantiation is error prone.
        // If design-time support is needed, a separate design-time ViewModel can be created.

        /// <summary>
        /// Giriş yapılabilir mi kontrolü
        /// </summary>
        private bool CanLogin()
        {
            return !string.IsNullOrWhiteSpace(Username) && !IsLoading && !IsSearchingForServer;
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
                    // Şifreyi güvenlik nedeniyle kaydetmiyoruz veya şifreli saklıyoruz.
                    // Burada basitlik için sadece kullanıcı adı hatırlanıyor.
                }
            }
            catch { }
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
                // Token saklama mekanizması eklenebilir
                props.Save();
            }
            catch { }
        }

        /// <summary>
        /// Giriş işlemini gerçekleştir
        /// </summary>
        public async Task ExecuteLoginAsync(object? parameter = null)
        {
            // Determine if we are auto-logging in with a token or manual login with PasswordBox
            string? autoToken = parameter as string;
            
            if (parameter is System.Windows.Controls.PasswordBox passwordBox)
            {
                Password = passwordBox.Password;
            }

            if (string.IsNullOrEmpty(autoToken) && (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password)))
            {
                 if(string.IsNullOrWhiteSpace(Username)) ErrorMessage = "Kullanıcı adı gerekli!";
                 else if(string.IsNullOrWhiteSpace(Password)) ErrorMessage = "Şifre gerekli!";
                 return;
            }

            ErrorMessage = string.Empty;
            IsLoading = true;

            try
            {
                await Task.Delay(500); // Simulate network/db latency for better UX

                bool isAuthenticated = false;

                if (!string.IsNullOrEmpty(autoToken))
                {
                    // Token Login (Simply validating existing session in local context)
                    // For local app, token might just be username or a simple hash
                    if (!string.IsNullOrEmpty(Username))
                    {
                         isAuthenticated = true;
                    }
                }
                else
                {
                    // Local DB Login
                    // IAuthService uses bool for Login return, not User object directly in current implementation?
                    // Let's check IAuthService.Login signature.
                    // IAuthService.Login(user, pass) returns bool. And sets CurrentUser.
                    if (_authService.Login(Username, Password))
                    {
                        isAuthenticated = true;
                        autoToken = Username; 
                        // Set global current user
                        App.CurrentUser = _authService.CurrentUser;
                    }
                    else
                    {
                        ErrorMessage = "Hatalı kullanıcı adı veya şifre!";
                    }
                }

                if (isAuthenticated)
                {
                    // Başarılı giriş - Ayarları kaydet
                    SaveCredentials(autoToken);
                    
                    // Ana içeriğe geç
                    _navigationService.NavigateToMainContent();
                }
                else
                {
                     if(string.IsNullOrEmpty(ErrorMessage)) ErrorMessage = "Giriş başarısız.";
                     Password = string.Empty;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Giriş hatası: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}

