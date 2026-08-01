using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Sifre sifirlama/degistirme ViewModel
    /// API uzerinden sifre degistirme islemi yapar.
    /// </summary>
    public partial class PasswordResetViewModel : ViewModelBase
    {
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext> _dbContextFactory;
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
                SaveCommand.NotifyCanExecuteChanged();
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
                SaveCommand.NotifyCanExecuteChanged();
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

        public event Action? SaveSuccessful;
        public event Action? CancelRequested;

        [RelayCommand]
        private void Cancel() => CancelRequested?.Invoke();

        public PasswordResetViewModel(User user, IAuthService authService, Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext> dbContextFactory)
        {
            _user = user;
            _authService = authService;
            _dbContextFactory = dbContextFactory;
        }

        private bool CanSavePassword()
        {
            return !string.IsNullOrWhiteSpace(NewPassword) &&
                   !string.IsNullOrWhiteSpace(ConfirmPassword) &&
                   PasswordPolicy.Validate(NewPassword) is null &&
                   PasswordsMatch &&
                   CanChangeTargetPassword();
        }

        [RelayCommand(CanExecute = nameof(CanSavePassword))]
        private async Task SaveAsync()
        {
            try
            {
                if (!CanChangeTargetPassword())
                {
                    IsSuccess = false;
                    StatusMessage = "Bu kullanıcının parolasını değiştirme yetkiniz yok.";
                    return;
                }

                if (!PasswordsMatch)
                {
                    IsSuccess = false;
                    StatusMessage = "Sifreler eslesmiyor!";
                    return;
                }

                var policyError = PasswordPolicy.Validate(NewPassword);
                if (policyError != null)
                {
                    IsSuccess = false;
                    StatusMessage = policyError;
                    return;
                }

                using var context = await _dbContextFactory.CreateDbContextAsync();
                var dbUser = await context.Users.FindAsync(_user.Id);
                
                if (dbUser != null)
                {
                    dbUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);
                    dbUser.MustChangePassword = false;
                    await context.SaveChangesAsync();
                    
                    IsSuccess = true;
                    StatusMessage = "Sifre basariyla guncellendi!";
                    SaveSuccessful?.Invoke();
                }
                else
                {
                    IsSuccess = false;
                    StatusMessage = "Hata: Kullanici bulunamadi.";
                }
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                StatusMessage = $"Hata: {ex.Message}";
            }
        }

        private bool CanChangeTargetPassword() =>
            _authService.IsAdmin || _authService.CurrentUser?.Id == _user.Id;
    }
}

