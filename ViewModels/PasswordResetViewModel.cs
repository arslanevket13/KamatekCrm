using System;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Şifre sıfırlama/değiştirme ViewModel
    /// </summary>
    public class PasswordResetViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private readonly User _user;

        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isSuccess;

        /// <summary>
        /// Kullanıcı adı (gösterim için)
        /// </summary>
        public string Username => _user.Username;

        /// <summary>
        /// Ad Soyad (gösterim için)
        /// </summary>
        public string AdSoyad => _user.AdSoyad;

        /// <summary>
        /// Yeni şifre
        /// </summary>
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

        /// <summary>
        /// Şifre onayı
        /// </summary>
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

        /// <summary>
        /// Şifreler eşleşiyor mu?
        /// </summary>
        public bool PasswordsMatch
        {
            get
            {
                if (string.IsNullOrEmpty(NewPassword) || string.IsNullOrEmpty(ConfirmPassword))
                    return true;
                return NewPassword == ConfirmPassword;
            }
        }

        /// <summary>
        /// Şifre eşleşme mesajı
        /// </summary>
        public string PasswordMatchMessage
        {
            get
            {
                if (string.IsNullOrEmpty(ConfirmPassword))
                    return string.Empty;
                return PasswordsMatch ? "✅ Şifreler eşleşiyor" : "❌ Şifreler eşleşmiyor";
            }
        }

        /// <summary>
        /// Durum mesajı
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                SetProperty(ref _statusMessage, value);
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }

        /// <summary>
        /// Başarılı mı?
        /// </summary>
        public bool IsSuccess
        {
            get => _isSuccess;
            set => SetProperty(ref _isSuccess, value);
        }

        /// <summary>
        /// Durum mesajı var mı?
        /// </summary>
        public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

        /// <summary>
        /// Kaydet komutu
        /// </summary>
        public ICommand SaveCommand { get; }

        /// <summary>
        /// Kaydetme başarılı event
        /// </summary>
        public event Action? SaveSuccessful;

        /// <summary>
        /// Constructor
        /// </summary>
        public PasswordResetViewModel(User user)
        {
            _context = new AppDbContext();
            _user = user;

            SaveCommand = new RelayCommand(_ => SavePassword(), _ => CanSavePassword());
        }

        /// <summary>
        /// Kayıt yapılabilir mi?
        /// </summary>
        private bool CanSavePassword()
        {
            return !string.IsNullOrWhiteSpace(NewPassword) &&
                   !string.IsNullOrWhiteSpace(ConfirmPassword) &&
                   NewPassword.Length >= 4 &&
                   PasswordsMatch;
        }

        /// <summary>
        /// Şifreyi kaydet
        /// </summary>
        private void SavePassword()
        {
            try
            {
                if (!PasswordsMatch)
                {
                    IsSuccess = false;
                    StatusMessage = "❌ Şifreler eşleşmiyor!";
                    return;
                }

                if (NewPassword.Length < 4)
                {
                    IsSuccess = false;
                    StatusMessage = "❌ Şifre en az 4 karakter olmalı!";
                    return;
                }

                // Veritabanından kullanıcıyı al
                var dbUser = _context.Users.Find(_user.Id);
                if (dbUser == null)
                {
                    IsSuccess = false;
                    StatusMessage = "❌ Kullanıcı bulunamadı!";
                    return;
                }

                // Şifreyi hash'le ve kaydet
                dbUser.PasswordHash = AuthService.HashPassword(NewPassword);
                _context.SaveChanges();

                IsSuccess = true;
                StatusMessage = "✅ Şifre başarıyla güncellendi!";

                SaveSuccessful?.Invoke();
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                StatusMessage = $"❌ Hata: {ex.Message}";
            }
        }
    }
}
