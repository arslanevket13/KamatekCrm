using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using KamatekCrm.Commands;
using KamatekCrm.Services;

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
            CommandManager.InvalidateRequerySuggested();
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
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Hata mesaji gorunurlugu
        /// </summary>
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Giris komutu
        /// </summary>
        public ICommand LoginCommand { get; }

        private readonly IAuthService _authService;
        private readonly NavigationService _navigationService;
        private readonly NetworkDiscoveryService _discoveryService;
        private readonly ApiClient _apiClient;
        private readonly IToastService _toastService;

        /// <summary>
        /// Constructor
        /// </summary>
        public LoginViewModel(IAuthService authService, NavigationService navigationService, NetworkDiscoveryService discoveryService, ApiClient apiClient, IToastService toastService)
        {
            _authService = authService;
            _navigationService = navigationService;
            _discoveryService = discoveryService;
            _apiClient = apiClient;
            _toastService = toastService;
            LoginCommand = new RelayCommand(async param => await ExecuteLoginAsync(param), _ => CanLogin());
            
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

        public async Task InitializeDiscoveryAsync()
        {
            IsSearchingForServer = true;
            ServerStatusMessage = "Agda Sunucu Araniyor...";
            IsServerFound = false;

            var server = await _discoveryService.DiscoverServerAsync(3000);

            if (server != null && !string.IsNullOrWhiteSpace(server.ApiUrl))
            {
                _apiClient.SetBaseUrl(server.ApiUrl);
                IsServerFound = true;
                ServerStatusMessage = $"Sunucu Bulundu: {server.ApiUrl}";
            }
            else
            {
                // Fallback: Use localhost directly — API is likely on the same machine
                _apiClient.SetBaseUrl("http://localhost:5050");
                IsServerFound = true;
                ServerStatusMessage = "Localhost sunucuya baglanildi (http://localhost:5050)";
            }

            IsSearchingForServer = false;
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
