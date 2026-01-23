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
        }

        /// <summary>
        /// Giriş yapılabilir mi?
        /// </summary>
        private bool CanLogin()
        {
            return !string.IsNullOrWhiteSpace(Username) &&
                   !string.IsNullOrWhiteSpace(Password) &&
                   !IsLoading;
        }

        /// <summary>
        /// Giriş işlemini gerçekleştir
        /// </summary>
        public void ExecuteLogin()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Kullanıcı adı ve şifre gerekli!";
                return;
            }

            ErrorMessage = string.Empty;
            IsLoading = true;

            try
            {
                if (AuthService.Login(Username, Password))
                {
                    // Başarılı giriş - Ana içeriğe geç
                    NavigationService.Instance.NavigateToMainContent();
                }
                else
                {
                    ErrorMessage = "Hatalı kullanıcı adı veya şifre!";
                    Password = string.Empty;
                }
            }
            catch (System.Exception ex)
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
