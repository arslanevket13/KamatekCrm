using System.Windows;
using Microsoft.Win32;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.Services
{
    public class WpfDialogService : IDialogService
    {
        public Task ShowMessageAsync(string message, string title = "Bilgi")
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            });
            return Task.CompletedTask;
        }

        public Task ShowWarningAsync(string message, string title = "Uyarı")
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            });
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string message, string title = "Hata")
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            });
            return Task.CompletedTask;
        }

        public Task<bool> ShowConfirmationAsync(string message, string title = "Onay")
        {
            bool result = false;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var dialogResult = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                result = dialogResult == MessageBoxResult.Yes;
            });
            return Task.FromResult(result);
        }

        public Task<string?> ShowOpenFileDialogAsync(string title = "Dosya Seç", string filter = "Tüm Dosyalar (*.*)|*.*")
        {
            string? filePath = null;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var dialog = new OpenFileDialog
                {
                    Title = title,
                    Filter = filter
                };
                if (dialog.ShowDialog() == true)
                {
                    filePath = dialog.FileName;
                }
            });
            return Task.FromResult(filePath);
        }

        public Task<string?> ShowSaveFileDialogAsync(string title = "Dosya Kaydet", string filter = "Tüm Dosyalar (*.*)|*.*", string? defaultFileName = null)
        {
            string? filePath = null;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var dialog = new SaveFileDialog
                {
                    Title = title,
                    Filter = filter,
                    FileName = defaultFileName ?? string.Empty
                };
                if (dialog.ShowDialog() == true)
                {
                    filePath = dialog.FileName;
                }
            });
            return Task.FromResult(filePath);
        }
    }
}
