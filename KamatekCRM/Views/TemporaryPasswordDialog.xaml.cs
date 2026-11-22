using System;
using System.Diagnostics;
using System.Windows;

namespace KamatekCrm.Views
{
    /// <summary>
    /// İlk kurulum veya yönetici şifre sıfırlama sonrasında kopyalanabilir geçici şifre gösteren modal diyalog.
    /// </summary>
    public partial class TemporaryPasswordDialog : Window
    {
        public TemporaryPasswordDialog(
            string username,
            string temporaryPassword,
            string title = "Güvenli İlk Kurulum",
            string? headerTitle = null,
            string? subtitle = null)
        {
            InitializeComponent();

            Title = title;
            if (!string.IsNullOrWhiteSpace(headerTitle))
            {
                HeaderTitleText.Text = headerTitle;
            }

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                SubtitleText.Text = subtitle;
            }

            UsernameTextBox.Text = username;
            PasswordTextBox.Text = temporaryPassword;
        }

        public static void ShowDialogSafe(
            string username,
            string temporaryPassword,
            string title = "Güvenli İlk Kurulum",
            string? headerTitle = null,
            string? subtitle = null,
            Window? owner = null)
        {
            var dialog = new TemporaryPasswordDialog(username, temporaryPassword, title, headerTitle, subtitle);

            var resolvedOwner = owner ?? Application.Current?.MainWindow;
            if (resolvedOwner != null && resolvedOwner != dialog && resolvedOwner.IsVisible)
            {
                dialog.Owner = resolvedOwner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dialog.ShowDialog();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(PasswordTextBox.Text);
                CopiedFeedbackText.Visibility = Visibility.Visible;
                CopyButton.Content = "Kopyalandı";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Geçici şifre panoya kopyalanamadı: {ex.Message}");
                PasswordTextBox.Focus();
                PasswordTextBox.SelectAll();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DialogResult = true;
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"DialogResult atanamadı: {ex.Message}");
            }

            Close();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}
