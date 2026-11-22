using System;
using System.IO;
using System.Windows;
using KamatekCrm.Services;
using Microsoft.Win32;
using Serilog;
using Wpf.Ui.Controls;

namespace KamatekCrm.Views
{
    public partial class PasswordRecoveryWindow : FluentWindow
    {
        private readonly IRecoveryKeyService _recoveryKeyService;
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;
        private string _newRecoveryKey = string.Empty;

        public PasswordRecoveryWindow(
            IRecoveryKeyService recoveryKeyService,
            IAuthService authService,
            IToastService toastService)
        {
            InitializeComponent();
            _recoveryKeyService = recoveryKeyService;
            _authService = authService;
            _toastService = toastService;
        }

        private void StaffOption_Click(object sender, RoutedEventArgs e)
        {
            ChoicePanel.Visibility = Visibility.Collapsed;
            StaffInfoPanel.Visibility = Visibility.Visible;
        }

        private void AdminOption_Click(object sender, RoutedEventArgs e)
        {
            ChoicePanel.Visibility = Visibility.Collapsed;
            AdminResetPanel.Visibility = Visibility.Visible;
            HideError();
        }

        private void BackToChoice_Click(object sender, RoutedEventArgs e)
        {
            StaffInfoPanel.Visibility = Visibility.Collapsed;
            AdminResetPanel.Visibility = Visibility.Collapsed;
            ChoicePanel.Visibility = Visibility.Visible;
            HideError();
        }

        private async void ResetAdminPassword_Click(object sender, RoutedEventArgs e)
        {
            HideError();
            var candidateKey = RecoveryKeyInput.Text?.Trim() ?? string.Empty;
            var newPassword = NewPasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(candidateKey))
            {
                ShowError("Lütfen kurtarma anahtarınızı girin.");
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                ShowError("Yeni parola en az 6 karakter olmalıdır.");
                return;
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                ShowError("Girilen parolalar birbiriyle eşleşmiyor.");
                return;
            }

            if (!_recoveryKeyService.VerifyStoredRecoveryKey(candidateKey))
            {
                ShowError("Kurtarma anahtarı doğrulanamadı. Lütfen anahtarı eksiksiz kontrol edin.");
                return;
            }

            ResetButton.IsEnabled = false;
            try
            {
                var result = await _authService.ResetAdminPasswordAsync(newPassword);
                if (!result.Success)
                {
                    ShowError(result.ErrorMessage ?? "Yönetici parolası güncellenemedi.");
                    return;
                }

                _newRecoveryKey = _recoveryKeyService.GenerateAndStoreNewRecoveryKey();
                NewRecoveryKeyText.Text = _newRecoveryKey;

                AdminResetPanel.Visibility = Visibility.Collapsed;
                SuccessPanel.Visibility = Visibility.Visible;
                _toastService.ShowSuccess("Yönetici parolası sıfırlandı ve yeni kurtarma anahtarı üretildi.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Çevrimdışı yönetici şifre sıfırlama başarısız oldu.");
                ShowError($"İşlem sırasında hata oluştu: {ex.Message}");
            }
            finally
            {
                ResetButton.IsEnabled = true;
            }
        }

        private void CopyNewKey_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_newRecoveryKey)) return;
            try
            {
                Clipboard.SetText(_newRecoveryKey);
                _toastService.ShowSuccess("Yeni kurtarma anahtarı panoya kopyalandı.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Yeni kurtarma anahtarı panoya kopyalanamadı.");
            }
        }

        private void SaveNewKey_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_newRecoveryKey)) return;
            var dialog = new SaveFileDialog
            {
                Title = "Yeni Kurtarma Anahtarını Kaydet",
                FileName = $"KamatekCRM-Kurtarma-Anahtari-{DateTime.Now:yyyyMMdd}.txt",
                Filter = "Metin Dosyası (*.txt)|*.txt"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(
                    dialog.FileName,
                    $"KamatekCRM Yeni Yönetici Kurtarma Anahtarı ({DateTime.Now:dd.MM.yyyy HH:mm}){Environment.NewLine}{_newRecoveryKey}{Environment.NewLine}");
                _toastService.ShowSuccess("Yeni kurtarma anahtarı kaydedildi.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Yeni kurtarma anahtarı dosyaya yazılamadı.");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorBanner.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorText.Text = string.Empty;
            ErrorBanner.Visibility = Visibility.Collapsed;
        }
    }
}
