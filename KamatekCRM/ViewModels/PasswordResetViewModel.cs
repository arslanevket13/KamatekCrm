using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Sifre sifirlama/degistirme ViewModel
    /// API uzerinden sifre degistirme islemi yapar.
    /// </summary>
    public class PasswordResetViewModel : ViewModelBase
    {
        private readonly ApiClient _apiClient;
        private readonly IAuthService _authService;
        private readonly User _user;

        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isSuccess;

        public string Username => _user.Username;
        public string AdSoyad => _user.AdSoyad;

        public string NewPassword
        {
            get => _newPassword;
            set
            {
                SetProperty(ref _newPassword, value);
                OnPropertyChanged(nameof(PasswordsMatch));
                OnPropertyChanged(nameof(PasswordMatchMessage));
            }
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                SetProperty(ref _confirmPassword, value);
                OnPropertyChanged(nameof(PasswordsMatch));
                OnPropertyChanged(nameof(PasswordMatchMessage));
            }
        }

        public bool PasswordsMatch
        {
            get
            {
                if (string.IsNullOrEmpty(NewPassword) || string.IsNullOrEmpty(ConfirmPassword))
                    return true;
                return NewPassword == ConfirmPassword;
            }
        }

        public string PasswordMatchMessage
        {
            get
            {
                if (string.IsNullOrEmpty(ConfirmPassword))
                    return string.Empty;
                return PasswordsMatch ? "Sifreler eslesiyor" : "Sifreler eslesmiyor";
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                SetProperty(ref _statusMessage, value);
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }

        public bool IsSuccess
        {
            get => _isSuccess;
            set => SetProperty(ref _isSuccess, value);
        }

        public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action? SaveSuccessful;
        public event Action? CancelRequested;

        public PasswordResetViewModel(User user, IAuthService authService, ApiClient apiClient)
        {
            _user = user;
            _authService = authService;
            _apiClient = apiClient;

            SaveCommand = new RelayCommand(async _ => await SavePasswordAsync(), _ => CanSavePassword());
            CancelCommand = new RelayCommand(_ => CancelRequested?.Invoke());
        }

        private bool CanSavePassword()
        {
            return !string.IsNullOrWhiteSpace(NewPassword) &&
                   !string.IsNullOrWhiteSpace(ConfirmPassword) &&
                   NewPassword.Length >= 4 &&
                   PasswordsMatch;
        }

        private async Task SavePasswordAsync()
        {
            try
            {
                if (!PasswordsMatch)
                {
                    IsSuccess = false;
                    StatusMessage = "Sifreler eslesmiyor!";
                    return;
                }

                if (NewPassword.Length < 4)
                {
                    IsSuccess = false;
                    StatusMessage = "Sifre en az 4 karakter olmali!";
                    return;
                }

                // API uzerinden sifre degistir
                var payload = new { UserId = _user.Id, NewPassword = NewPassword };
                var response = await _apiClient.PutAsync<object>($"api/users/{_user.Id}/password", payload);

                if (response.Success)
                {
                    IsSuccess = true;
                    StatusMessage = "Sifre basariyla guncellendi!";
                    SaveSuccessful?.Invoke();
                }
                else
                {
                    IsSuccess = false;
                    StatusMessage = $"Hata: {response.Message}";
                }
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                StatusMessage = $"Hata: {ex.Message}";
            }
        }
    }
}
