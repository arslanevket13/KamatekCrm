using System;
using System.Windows;
using KamatekCrm.Services.Update;

namespace KamatekCrm.Views
{
    public partial class UpdateNotificationWindow : Window
    {
        private readonly IUpdateService _updateService;

        public UpdateNotificationWindow(IUpdateService updateService)
        {
            InitializeComponent();
            _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));

            PopulateUpdateInfo();
        }

        private void PopulateUpdateInfo()
        {
            var update = _updateService.AvailableUpdate;
            TxtCurrentVersion.Text = _updateService.CurrentVersion;

            if (update != null)
            {
                TxtTargetVersion.Text = update.TargetVersion;
                TxtReleaseNotes.Text = update.ReleaseNotes;
                double sizeMb = update.DownloadSizeBytes / (1024.0 * 1024.0);
                TxtSizeInfo.Text = sizeMb > 0 ? $"Boyut: {sizeMb:F1} MB" : "Boyut: Belirtilmedi";
            }

            if (_updateService.IsUpdateDownloaded)
            {
                ShowDownloadedState();
            }
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            BtnDownload.IsEnabled = false;
            BtnLater.IsEnabled = false;
            PnlProgress.Visibility = Visibility.Visible;
            ProgressBarDownload.Value = 0;

            var progress = new Progress<int>(percent =>
            {
                ProgressBarDownload.Value = percent;
                TxtProgressPercent.Text = $"%{percent}";
                if (percent >= 100)
                {
                    TxtProgressStatus.Text = "İndirme tamamlandı! Yüklemeye hazır.";
                }
            });

            bool success = await _updateService.DownloadUpdateAsync(progress);
            if (success)
            {
                ShowDownloadedState();
            }
            else
            {
                PnlProgress.Visibility = Visibility.Collapsed;
                BtnDownload.IsEnabled = true;
                BtnLater.IsEnabled = true;
            }
        }

        private void ShowDownloadedState()
        {
            PnlProgress.Visibility = Visibility.Collapsed;
            GridInitialButtons.Visibility = Visibility.Collapsed;
            GridDownloadedButtons.Visibility = Visibility.Visible;
        }

        private void BtnRestartNow_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
            _updateService.ApplyUpdateAndRestart();
        }

        private void BtnInstallOnClose_Click(object sender, RoutedEventArgs e)
        {
            _updateService.PrepareUpdateOnClose();
            DialogResult = true;
            Close();
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
