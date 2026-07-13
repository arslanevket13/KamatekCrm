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
            
            // Mevcut temayı yükle
            _selectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == ThemeService.CurrentThemeName) ?? AvailableThemes.First();
            
            LoadLastBackupInfo();
            LoadIsMainServerConfig();
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

        private bool _isMainServer;
        public bool IsMainServer
        {
            get => _isMainServer;
            set
            {
                if (SetProperty(ref _isMainServer, value))
                {
                    SaveIsMainServerToConfig(value);
                }
            }
        }

        #endregion

        #region Theme Properties

        public class ThemeOption
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Icon { get; set; } = string.Empty;
            public string ColorHex { get; set; } = string.Empty;
        }

        public System.Collections.ObjectModel.ObservableCollection<ThemeOption> AvailableThemes { get; } = new()
        {
            new ThemeOption { Id = "PremiumLight", Title = "Premium Light", Description = "Ultra-temiz beyaz tasarım", Icon = "☀️", ColorHex = "#FFFFFF" },
            new ThemeOption { Id = "MidnightDark", Title = "Midnight Dark", Description = "Göz yormayan koyu tema", Icon = "🌙", ColorHex = "#0B0E14" },
            new ThemeOption { Id = "Glassmorphism", Title = "Glassmorphism", Description = "Modern şeffaf akrilik", Icon = "✨", ColorHex = "#6366F1" }
        };

        private ThemeOption _selectedTheme;
        public ThemeOption SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetProperty(ref _selectedTheme, value) && value != null)
                {
                    ThemeService.ChangeTheme(value.Id);
                }
            }
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
                var appPath = System.Windows.Application.ResourceAssembly.Location.Replace(".dll", ".exe");
                System.Diagnostics.Process.Start(appPath);
                System.Windows.Application.Current.Shutdown();
            }
            catch
            {
                MessageBox.Show("Uygulama yeniden başlatılamadı. Lütfen manuel olarak kapatıp açın.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadIsMainServerConfig()
        {
            try
            {
                string appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!File.Exists(appSettingsPath))
                {
                    // Fallback to project root for dev
                    string? projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName;
                    if (!string.IsNullOrEmpty(projectRoot)) appSettingsPath = Path.Combine(projectRoot, "appsettings.json");
                }

                if (File.Exists(appSettingsPath))
                {
                    string jsonString = File.ReadAllText(appSettingsPath);
                    var jsonObject = System.Text.Json.Nodes.JsonNode.Parse(jsonString);
                    var isMainServerNode = jsonObject?["NetworkDiscovery"]?["IsMainServer"];
                    if (isMainServerNode != null)
                    {
                        _isMainServer = isMainServerNode.GetValue<bool>();
                        OnPropertyChanged(nameof(IsMainServer));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Config read error: {ex.Message}");
            }
        }

        private void SaveIsMainServerToConfig(bool value)
        {
            try
            {
                string appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!File.Exists(appSettingsPath))
                {
                    // Fallback to project root for dev
                    string? projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName;
                    if (!string.IsNullOrEmpty(projectRoot)) appSettingsPath = Path.Combine(projectRoot, "appsettings.json");
                }

                if (File.Exists(appSettingsPath))
                {
                    string jsonString = File.ReadAllText(appSettingsPath);
                    var jsonObject = System.Text.Json.Nodes.JsonNode.Parse(jsonString);
                    if (jsonObject != null && jsonObject["NetworkDiscovery"] != null)
                    {
                        jsonObject["NetworkDiscovery"]!["IsMainServer"] = value;
                        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                        File.WriteAllText(appSettingsPath, jsonObject.ToJsonString(options));
                        MessageBox.Show("Ağ ayarı kaydedildi. Değişikliklerin etkili olması için uygulamayı yeniden başlatmanız önerilir.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ayar kaydedilemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
