using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly BackupService _backupService;

        public SettingsViewModel()
        {
            _backupService = new BackupService();
            TakeBackupCommand = new RelayCommand(_ => TakeBackup(), _ => !IsBusy);
            RestoreBackupCommand = new RelayCommand(_ => RestoreBackup(), _ => !IsBusy);
            LoadLastBackupInfo();
        }

        #region Properties

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _lastBackupText = "Hiç alınmadı";
        public string LastBackupText
        {
            get => _lastBackupText;
            set => SetProperty(ref _lastBackupText, value);
        }

        #endregion

        #region Commands

        public ICommand TakeBackupCommand { get; }
        public ICommand RestoreBackupCommand { get; }

        #endregion

        #region Methods

        private void LoadLastBackupInfo()
        {
            try
            {
                var docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var backupFolder = Path.Combine(docPath, "KamatekBackups");

                if (Directory.Exists(backupFolder))
                {
                    var lastFile = new DirectoryInfo(backupFolder)
                        .GetFiles("*.zip")
                        .OrderByDescending(f => f.LastWriteTime)
                        .FirstOrDefault();

                    if (lastFile != null)
                    {
                        LastBackupText = lastFile.LastWriteTime.ToString("dd.MM.yyyy HH:mm");
                    }
                    else
                    {
                        LastBackupText = "Klasör boş";
                    }
                }
            }
            catch
            {
                LastBackupText = "Bilgi alınamadı";
            }
        }

        private async void TakeBackup()
        {
            IsBusy = true;
            try
            {
                string backupPath = "";
                await Task.Run(() => 
                {
                    backupPath = _backupService.BackupDatabase();
                });

                LoadLastBackupInfo();
                MessageBox.Show($"Yedekleme başarılı!\nKonum: {backupPath}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yedekleme sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void RestoreBackup()
        {
            // 1. Dosya seçme dialogu
            var docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var backupFolder = Path.Combine(docPath, "KamatekBackups");

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Geri Yüklenecek Yedek Dosyasını Seçin",
                Filter = "ZIP Dosyaları (*.zip)|*.zip",
                InitialDirectory = Directory.Exists(backupFolder) ? backupFolder : docPath
            };

            if (dialog.ShowDialog() != true) return;

            // 2. Kullanıcı onayı
            var confirm = MessageBox.Show(
                "DİKKAT: Bu işlem mevcut veritabanını silip seçilen yedeği yükleyecektir.\n\n" +
                "Tüm güncel veriler kaybolacak!\n\n" +
                "Devam etmek istiyor musunuz?",
                "Geri Yükleme Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                await Task.Run(() =>
                {
                    _backupService.RestoreDatabase(dialog.FileName);
                });

                // ═══════════════════════════════════════════════════════════════════
                // GHOST DATA ÖNLEME: MessageBox'tan ÖNCE restart yap
                // EF Core tracking cache'i eski verileri gösterebilir
                // ═══════════════════════════════════════════════════════════════════
                
                // Kullanıcıya bilgi ver ve hemen yeniden başlat
                MessageBox.Show(
                    "Geri yükleme başarılı!\n\nProgram şimdi yeniden başlatılacak.",
                    "Başarılı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Hemen restart - Ghost data önleme için kritik
                RestartApplication();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Geri yükleme sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                IsBusy = false;
            }
            // Not: IsBusy = false finally'de olmamalı çünkü restart yapılıyor
        }

        private void RestartApplication()
        {
            try
            {
                var appPath = Application.ResourceAssembly.Location.Replace(".dll", ".exe");
                System.Diagnostics.Process.Start(appPath);
                Application.Current.Shutdown();
            }
            catch
            {
                MessageBox.Show("Uygulama yeniden başlatılamadı. Lütfen manuel olarak kapatıp açın.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion
    }
}
