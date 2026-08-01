using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Services;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;

namespace KamatekCrm.ViewModels
{
    public class CompanySettingsDto
    {
        public string CompanyName { get; set; } = "Kamatek Bilişim & Güvenlik Sistemleri";
        public string TaxOffice { get; set; } = "";
        public string TaxNumber { get; set; } = "";
        public string Address { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string Iban { get; set; } = "";
        public string CustomBackupPath { get; set; } = "";
        public string DefaultPrinter { get; set; } = "";
        public bool AutoPrintReceipt { get; set; } = false;
        public bool SoundAlertsEnabled { get; set; } = true;
    }

    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly IBackupService _backupService;
        private readonly IToastService? _toastService;
        private readonly IApplicationAuthorizationService _authorizationService;

        public SettingsViewModel(
            IToastService toastService,
            IApplicationAuthorizationService authorizationService,
            IBackupService backupService)
        {
            _toastService = toastService;
            _authorizationService = authorizationService;
            _backupService = backupService;
            
            // Ayarları Properties.Settings.Default'tan yükle
            string savedThemeId = Properties.Settings.Default.ThemePreference;
            if (string.IsNullOrEmpty(savedThemeId)) savedThemeId = "PremiumLight";
            
            _selectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == savedThemeId) 
                             ?? AvailableThemes.FirstOrDefault(t => t.Id == ThemeService.CurrentThemeName) 
                             ?? AvailableThemes.First();
                             
            _accentColor = Properties.Settings.Default.AccentColor;
            _isMainServer = Properties.Settings.Default.IsMainServer;
            
            LoadCompanySettings();
            LoadInstalledPrinters();
            LoadLastBackupInfo();
        }

        #region Properties & Tab Navigation

        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        private bool _isBusy;
          public bool IsBusy
          {
              get => _isBusy;
              set
              {
                  if (!SetProperty(ref _isBusy, value)) return;
                  TakeBackupCommand.NotifyCanExecuteChanged();
                  RestoreBackupCommand.NotifyCanExecuteChanged();
              }
          }

        private string _lastBackupText = "Hiç alınmadı";
        public string LastBackupText
        {
            get => _lastBackupText;
            set => SetProperty(ref _lastBackupText, value);
        }

        // Firma Bilgileri
        private string _companyName = "Kamatek Bilişim & Güvenlik";
        public string CompanyName
        {
            get => _companyName;
            set => SetProperty(ref _companyName, value);
        }

        private string _taxOffice = "";
        public string TaxOffice
        {
            get => _taxOffice;
            set => SetProperty(ref _taxOffice, value);
        }

        private string _taxNumber = "";
        public string TaxNumber
        {
            get => _taxNumber;
            set => SetProperty(ref _taxNumber, value);
        }

        private string _companyAddress = "";
        public string CompanyAddress
        {
            get => _companyAddress;
            set => SetProperty(ref _companyAddress, value);
        }

        private string _companyPhone = "";
        public string CompanyPhone
        {
            get => _companyPhone;
            set => SetProperty(ref _companyPhone, value);
        }

        private string _companyEmail = "";
        public string CompanyEmail
        {
            get => _companyEmail;
            set => SetProperty(ref _companyEmail, value);
        }

        private string _companyIban = "";
        public string CompanyIban
        {
            get => _companyIban;
            set => SetProperty(ref _companyIban, value);
        }

        private string _customBackupPath = "";
        public string CustomBackupPath
        {
            get => _customBackupPath;
            set => SetProperty(ref _customBackupPath, value);
        }

        // Yazıcı & Bildirimler
        public System.Collections.ObjectModel.ObservableCollection<string> AvailablePrinters { get; } = new();

        private string _selectedPrinter = "Varsayılan Sistem Yazıcısı";
        public string SelectedPrinter
        {
            get => _selectedPrinter;
            set => SetProperty(ref _selectedPrinter, value);
        }

        private bool _autoPrintReceipt;
        public bool AutoPrintReceipt
        {
            get => _autoPrintReceipt;
            set => SetProperty(ref _autoPrintReceipt, value);
        }

        private bool _soundAlertsEnabled = true;
        public bool SoundAlertsEnabled
        {
            get => _soundAlertsEnabled;
            set => SetProperty(ref _soundAlertsEnabled, value);
        }

        private bool _isMainServer;
        public bool IsMainServer
        {
            get => _isMainServer;
            set
            {
                if (!EnsureSettingsAuthorized()) return;
                if (SetProperty(ref _isMainServer, value))
                {
                    Properties.Settings.Default.IsMainServer = value;
                    Properties.Settings.Default.IsMainServerManualOverride = true; // Mark as manually overridden
                    Properties.Settings.Default.Save();
                    SaveIsMainServerToConfig(value);
                }
            }
        }
        
        private string _accentColor = "#2563EB";
        public string AccentColor
        {
            get => _accentColor;
            set
            {
                if (SetProperty(ref _accentColor, value))
                {
                    Properties.Settings.Default.AccentColor = value;
                    Properties.Settings.Default.Save();
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
                    Properties.Settings.Default.ThemePreference = value.Id;
                    Properties.Settings.Default.Save();
                }
            }
        }

        #endregion

        #region Commands

        private bool IsNotBusy() => !IsBusy;

        /// <summary>
        /// Ağ Yönetimi sayfasına navigasyon yapar. (Modal Pencere olarak)
        /// </summary>
        [RelayCommand]
        private void OpenNetworkSettings()
        {
            if (!EnsureSettingsAuthorized()) return;
            try
            {
                var networkVm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<KamatekCrm.ViewModels.NetworkSettingsViewModel>(App.ServiceProvider);
                var window = new System.Windows.Window
                {
                    Title = "Ağ ve Sunucu Yönetimi",
                    Content = new KamatekCrm.Views.NetworkSettingsView { DataContext = networkVm },
                    Width = 800,
                    Height = 800,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                    ResizeMode = System.Windows.ResizeMode.NoResize
                };
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Network Settings modal error: {ex.Message}");
            }
        }

        #endregion

        #region Methods

        private void LoadLastBackupInfo()
        {
            try
            {
                var backupFolder = string.IsNullOrWhiteSpace(CustomBackupPath)
                    ? _backupService.DefaultBackupDirectory
                    : CustomBackupPath;

                if (Directory.Exists(backupFolder))
                {
                    var lastFile = new DirectoryInfo(backupFolder)
                        .GetFiles(_backupService.BackupFilePattern)
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

        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task TakeBackup()
        {
            if (!EnsureSettingsAuthorized()) return;
            IsBusy = true;
            try
            {
                string backupPath = "";
                await Task.Run(() => 
                {
                    backupPath = _backupService.BackupDatabase(
                        string.IsNullOrWhiteSpace(CustomBackupPath) ? null : CustomBackupPath);
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

        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task RestoreBackup()
        {
            if (!EnsureSettingsAuthorized()) return;
            // 1. Dosya seçme dialogu
            var docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var backupFolder = string.IsNullOrWhiteSpace(CustomBackupPath)
                ? _backupService.DefaultBackupDirectory
                : CustomBackupPath;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Geri Yüklenecek Yedek Dosyasını Seçin",
                Filter = "Kamatek PostgreSQL Yedeği (*.backup)|*.backup",
                InitialDirectory = Directory.Exists(backupFolder) ? backupFolder : docPath
            };

            if (dialog.ShowDialog() != true) return;

            var validation = await Task.Run(() => _backupService.ValidateBackup(dialog.FileName));
            if (!validation.IsValid)
            {
                MessageBox.Show(
                    $"Bu yedek güvenli biçimde doğrulanamadı:\n\n{validation.Message}",
                    "Geçersiz Yedek",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

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
                string recoveryBackup = string.Empty;
                await Task.Run(() =>
                {
                    recoveryBackup = _backupService.RestoreDatabase(dialog.FileName);
                });

                // ═══════════════════════════════════════════════════════════════════
                // GHOST DATA ÖNLEME: MessageBox'tan ÖNCE restart yap
                // EF Core tracking cache'i eski verileri gösterebilir
                // ═══════════════════════════════════════════════════════════════════
                
                // Kullanıcıya bilgi ver ve hemen yeniden başlat
                MessageBox.Show(
                    $"Geri yükleme başarılı!\n\nİşlem öncesi kurtarma yedeği:\n{recoveryBackup}\n\nProgram şimdi yeniden başlatılacak.",
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
                        
                        var result = MessageBox.Show("Ağ ayarı (Ana Sunucu) değiştirildi. Değişikliğin anında etkili olması için programın yeniden başlatılması gerekmektedir.\n\nŞimdi yeniden başlatılsın mı?", 
                            "Yeniden Başlat Gerekli", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            
                        if (result == MessageBoxResult.Yes)
                        {
                            RestartApplication();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ayar kaydedilemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Company & Printer Helper Methods

        private void LoadCompanySettings()
        {
            try
            {
                string filePath = GetCompanySettingsFilePath();
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var dto = System.Text.Json.JsonSerializer.Deserialize<CompanySettingsDto>(json);
                    if (dto != null)
                    {
                        _companyName = dto.CompanyName ?? "Kamatek Bilişim & Güvenlik";
                        _taxOffice = dto.TaxOffice ?? "";
                        _taxNumber = dto.TaxNumber ?? "";
                        _companyAddress = dto.Address ?? "";
                        _companyPhone = dto.Phone ?? "";
                        _companyEmail = dto.Email ?? "";
                        _companyIban = dto.Iban ?? "";
                        _customBackupPath = dto.CustomBackupPath ?? "";
                        _selectedPrinter = dto.DefaultPrinter ?? "Varsayılan Sistem Yazıcısı";
                        _autoPrintReceipt = dto.AutoPrintReceipt;
                        _soundAlertsEnabled = dto.SoundAlertsEnabled;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Company settings load error: {ex.Message}");
            }
        }

        private void SaveCompanySettings()
        {
            try
            {
                string filePath = GetCompanySettingsFilePath();
                var dto = new CompanySettingsDto
                {
                    CompanyName = CompanyName,
                    TaxOffice = TaxOffice,
                    TaxNumber = TaxNumber,
                    Address = CompanyAddress,
                    Phone = CompanyPhone,
                    Email = CompanyEmail,
                    Iban = CompanyIban,
                    CustomBackupPath = CustomBackupPath,
                    DefaultPrinter = SelectedPrinter,
                    AutoPrintReceipt = AutoPrintReceipt,
                    SoundAlertsEnabled = SoundAlertsEnabled
                };
                string json = System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Company settings save error: {ex.Message}");
            }
        }

        private static string GetCompanySettingsFilePath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KamatekCRM");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "company_settings.json");
        }

        private void LoadInstalledPrinters()
        {
            try
            {
                AvailablePrinters.Clear();
                AvailablePrinters.Add("Varsayılan Sistem Yazıcısı");
                foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    AvailablePrinters.Add(printer);
                }
            }
            catch
            {
                if (!AvailablePrinters.Contains("Varsayılan Sistem Yazıcısı"))
                    AvailablePrinters.Add("Varsayılan Sistem Yazıcısı");
            }
        }

        [RelayCommand]
        private void SelectTab(string tabIndexStr)
        {
            if (int.TryParse(tabIndexStr, out int index))
            {
                SelectedTabIndex = index;
            }
        }

        [RelayCommand]
        private void SaveCompanyInfo()
        {
            if (!EnsureSettingsAuthorized()) return;
            SaveCompanySettings();
            _toastService?.ShowSuccess("Firma ve sistem ayarları başarıyla kaydedildi.");
        }

        [RelayCommand]
        private void SelectBackupFolder()
        {
            if (!EnsureSettingsAuthorized()) return;
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Yedekleme Klasörü Seçin"
            };

            if (dialog.ShowDialog() == true)
            {
                CustomBackupPath = dialog.FolderName;
                SaveCompanySettings();
                _toastService?.ShowInfo($"Yedekleme konumu güncellendi: {CustomBackupPath}");
            }
        }

        private bool EnsureSettingsAuthorized()
        {
            var authorization = _authorizationService.Authorize(ApplicationPermission.AccessSettings);
            if (authorization.IsSuccess) return true;

            _toastService?.ShowError("Yetkisiz işlem", authorization.Error);
            return false;
        }

        #endregion
    }
}
