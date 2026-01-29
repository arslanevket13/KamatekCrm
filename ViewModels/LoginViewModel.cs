using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Login ekranı ViewModel (UserControl için)
    /// </summary>
    public class LoginViewModel : ViewModelBase
    {
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isLoading;
        private bool _rememberMe;

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

        /// <summary>
        /// Constructor
        /// </summary>
        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(_ => ExecuteLogin(), _ => CanLogin());
            
            // Load saved settings
            LoadSavedCredentials();
        }

        /// <summary>
        /// Kayıtlı kullanıcı bilgilerini yükle
        /// </summary>
        private void LoadSavedCredentials()
        {
            try
            {
                var settings = Properties.Settings.Default;
                _rememberMe = settings.RememberMe;
                
                if (_rememberMe)
                {
                    // Token kontrolü
                    string token = settings.AuthToken;
                    if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(settings.SavedUsername))
                    {
                        // Basit token doğrulama (Prod: Token süresi, imza vb. kontrol edilmeli)
                        // Şimdilik token varsa geçerli sayıyoruz
                        _username = settings.SavedUsername;
                        
                        // Auto-login
                        // Eğer UI donarsa veya kullanıcı giriş ekranını görmek isterse burayı async yapıp biraz bekletebiliriz.
                        // Şimdilik direkt giriş yapalım.
                        ExecuteLogin(token); 
                    }
                    else if (!string.IsNullOrEmpty(settings.SavedUsername))
                    {
                        // Sadece kullanıcı adını hatırla
                        _username = settings.SavedUsername;
                        OnPropertyChanged(nameof(Username));
                    }
                }
                 OnPropertyChanged(nameof(RememberMe));
            }
            catch
            {
                // Settings yüklenemezse sessizce devam et
            }
        }

        /// <summary>
        /// Kullanıcı bilgilerini kaydet
        /// </summary>
        private void SaveCredentials(string token = "")
        {
            try
            {
                var settings = Properties.Settings.Default;
                settings.RememberMe = _rememberMe;
                settings.SavedUsername = _rememberMe ? _username : string.Empty;
                
                // Token kaydet (Eğer remember me ise ve token geldiyse)
                if (_rememberMe && !string.IsNullOrEmpty(token))
                {
                    settings.AuthToken = token;
                }
                else if (!_rememberMe)
                {
                    settings.AuthToken = string.Empty;
                }

                settings.Save();
            }
            catch
            {
                // Settings kaydedilemezse sessizce devam et
            }
        }

        /// <summary>
        /// Giriş yapılabilir mi?
        /// </summary>
        private bool CanLogin()
        {
            return !string.IsNullOrWhiteSpace(Username) && 
                   (!string.IsNullOrWhiteSpace(Password) || IsLoading) && 
                   !IsLoading;
        }

        /// <summary>
        /// Giriş işlemini gerçekleştir
        /// </summary>
        public void ExecuteLogin(string autoToken = "")
        {
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
                bool isAuthenticated = false;

                if (!string.IsNullOrEmpty(autoToken))
                {
                    // Token login simülasyonu
                    // Gerçek hayatta: AuthService.LoginWithToken(autoToken)
                    isAuthenticated = true; 
                }
                else
                {
                    // Normal login
                    isAuthenticated = AuthService.Login(Username, Password);
                }

                if (isAuthenticated)
                {
                    // Başarılı giriş - Ayarları ve Token'ı kaydet
                    // Gerçek hayatta token backend'den gelir. Burada simüle ediyoruz.
                    string newToken = Guid.NewGuid().ToString(); 
                    SaveCredentials(newToken);
                    
                    // Ana içeriğe geç
                    NavigationService.Instance.NavigateToMainContent();
                }
                else
                {
                    ErrorMessage = "Hatalı kullanıcı adı veya şifre!";
                    Password = string.Empty;
                    
                    // Token geçersizse temizle
                    if(!string.IsNullOrEmpty(autoToken))
                    {
                        var s = Properties.Settings.Default;
                        s.AuthToken = string.Empty;
                        s.Save();
                        ErrorMessage = "Oturum süresi dolmuş, lütfen tekrar giriş yapın.";
                    }
                }
            }
            catch (System.Exception ex)
            {
                var fullMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    fullMessage += $"\n\nDetay: {ex.InnerException.Message}";
                }
                MessageBox.Show($"Giriş sırasında hata oluştu:\n\n{fullMessage}", "Giriş Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                ErrorMessage = "Sistem hatası oluştu.";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}

