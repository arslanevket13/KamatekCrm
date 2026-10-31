using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Services;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.ApplicationCore.DTOs.Transactions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Services;
using KamatekCrm.Settings;

namespace KamatekCrm.ViewModels
{
    public class CompanySettingsDto
    {
        public string CompanyName { get; set; } = "KAMATEK";
        public string CompanySubtitle { get; set; } = "ELEKTRİK VE GÜVENLİK SİSTEMLERİ";
        public string CompanySloganOrBranch { get; set; } = "ESKİŞEHİR DİAFON MERKEZİ";
        public string Website { get; set; } = "www.kamatekelektrik.com";
        public string TradeRegistryNo { get; set; } = "";
        public string MersisNo { get; set; } = "";
        public string BankName { get; set; } = "Ziraat Bankası";
        public string AccountHolder { get; set; } = "Kamatek Elektrik ve Güvenlik Sistemleri";
        public string IbanTry { get; set; } = "";
        public string IbanUsd { get; set; } = "";
        public string IbanEur { get; set; } = "";
        public string CustomLogoPath { get; set; } = "";
        public string CustomLogoBase64 { get; set; } = "";
        public string StampSignatureBase64 { get; set; } = "";
        public string PrimaryBrandColorHex { get; set; } = "#182649";
        public string AccentBrandColorHex { get; set; } = "#D11920";
        public int QuoteValidityDays { get; set; } = 7;
        public string QuoteTermsText { get; set; } = "";
        public string ServiceFormTermsText { get; set; } = "";
        public string WarrantyTermsText { get; set; } = "";
        public bool ShowUnitPricesInPdf { get; set; } = true;
        public bool ShowProductImagesInPdf { get; set; } = true;
        public bool ShowStampInPdf { get; set; } = true;
        public string WatermarkText { get; set; } = "";
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
        private readonly IDialogService? _dialogService;
        private readonly IApplicationAuthorizationService _authorizationService;
        private readonly IRetailTransactionService _retailTransactions;
        private readonly Services.Update.IUpdateService? _updateService;
        private readonly Diagnostics.Authorization.DiagnosticAccessGuard? _diagnosticGuard;
        private readonly IServiceProvider? _serviceProvider;
        private LegacyLedgerPreviewDto? _legacyLedgerPreview;

        public SettingsViewModel(
            IToastService toastService,
            IApplicationAuthorizationService authorizationService,
            IBackupService backupService,
            IRetailTransactionService retailTransactions,
            Services.Update.IUpdateService? updateService = null,
            Diagnostics.Authorization.DiagnosticAccessGuard? diagnosticGuard = null,
            IServiceProvider? serviceProvider = null,
            IDialogService? dialogService = null)
        {
            _toastService = toastService;
            _authorizationService = authorizationService;
            _backupService = backupService;
            _retailTransactions = retailTransactions;
            _updateService = updateService;
            // Guard null ise (DI dışı kurulum) buton gizli kalır: fail-closed.
            _diagnosticGuard = diagnosticGuard;
            _serviceProvider = serviceProvider;
            _dialogService = dialogService ?? serviceProvider?.GetService(typeof(IDialogService)) as IDialogService;

            
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
            LoadUpdateSettings();
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

        public System.Collections.ObjectModel.ObservableCollection<LegacyLedgerIssueDto> LegacyLedgerIssues { get; } = new();

        private string _legacyLedgerSummary = "Önizleme henüz çalıştırılmadı.";
        public string LegacyLedgerSummary
        {
            get => _legacyLedgerSummary;
            set => SetProperty(ref _legacyLedgerSummary, value);
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

        private string _companySubtitle = "ELEKTRİK VE GÜVENLİK SİSTEMLERİ";
        public string CompanySubtitle { get => _companySubtitle; set => SetProperty(ref _companySubtitle, value); }
        private string _companySloganOrBranch = "ESKİŞEHİR DİAFON MERKEZİ";
        public string CompanySloganOrBranch { get => _companySloganOrBranch; set => SetProperty(ref _companySloganOrBranch, value); }
        private string _website = "www.kamatekelektrik.com";
        public string Website { get => _website; set => SetProperty(ref _website, value); }
        private string _tradeRegistryNo = "";
        public string TradeRegistryNo { get => _tradeRegistryNo; set => SetProperty(ref _tradeRegistryNo, value); }
        private string _mersisNo = "";
        public string MersisNo { get => _mersisNo; set => SetProperty(ref _mersisNo, value); }
        private string _bankName = "Ziraat Bankası";
        public string BankName { get => _bankName; set => SetProperty(ref _bankName, value); }
        private string _accountHolder = "Kamatek Elektrik ve Güvenlik Sistemleri";
        public string AccountHolder { get => _accountHolder; set => SetProperty(ref _accountHolder, value); }
        private string _ibanTry = "";
        public string IbanTry { get => _ibanTry; set => SetProperty(ref _ibanTry, value); }
        private string _ibanUsd = "";
        public string IbanUsd { get => _ibanUsd; set => SetProperty(ref _ibanUsd, value); }
        private string _ibanEur = "";
        public string IbanEur { get => _ibanEur; set => SetProperty(ref _ibanEur, value); }
        private string _customLogoPath = "";
        public string CustomLogoPath { get => _customLogoPath; set => SetProperty(ref _customLogoPath, value); }
        private string _customLogoBase64 = "";
        public string CustomLogoBase64 { get => _customLogoBase64; set => SetProperty(ref _customLogoBase64, value); }
        private string _stampSignatureBase64 = "";
        public string StampSignatureBase64 { get => _stampSignatureBase64; set => SetProperty(ref _stampSignatureBase64, value); }
        private string _primaryBrandColorHex = "#182649";
        public string PrimaryBrandColorHex { get => _primaryBrandColorHex; set => SetProperty(ref _primaryBrandColorHex, value); }
        private string _accentBrandColorHex = "#D11920";
        public string AccentBrandColorHex { get => _accentBrandColorHex; set => SetProperty(ref _accentBrandColorHex, value); }
        private int _quoteValidityDays = 7;
        public int QuoteValidityDays { get => _quoteValidityDays; set => SetProperty(ref _quoteValidityDays, value); }
        private string _quoteTermsText = "";
        public string QuoteTermsText { get => _quoteTermsText; set => SetProperty(ref _quoteTermsText, value); }
        private string _serviceFormTermsText = "";
        public string ServiceFormTermsText { get => _serviceFormTermsText; set => SetProperty(ref _serviceFormTermsText, value); }
        private string _warrantyTermsText = "";
        public string WarrantyTermsText { get => _warrantyTermsText; set => SetProperty(ref _warrantyTermsText, value); }
        private bool _showUnitPricesInPdf = true;
        public bool ShowUnitPricesInPdf { get => _showUnitPricesInPdf; set => SetProperty(ref _showUnitPricesInPdf, value); }
        private bool _showProductImagesInPdf = true;
        public bool ShowProductImagesInPdf { get => _showProductImagesInPdf; set => SetProperty(ref _showProductImagesInPdf, value); }
        private bool _showStampInPdf = true;
        public bool ShowStampInPdf { get => _showStampInPdf; set => SetProperty(ref _showStampInPdf, value); }
        private string _watermarkText = "";
        public string WatermarkText { get => _watermarkText; set => SetProperty(ref _watermarkText, value); }
        private ImageSource? _logoPreviewSource;
        public ImageSource? LogoPreviewSource { get => _logoPreviewSource; set => SetProperty(ref _logoPreviewSource, value); }
        private ImageSource? _stampPreviewSource;
        public ImageSource? StampPreviewSource { get => _stampPreviewSource; set => SetProperty(ref _stampPreviewSource, value); }
        private bool _hasCustomLogo;
        public bool HasCustomLogo { get => _hasCustomLogo; set => SetProperty(ref _hasCustomLogo, value); }
        private bool _hasCustomStamp;
        public bool HasCustomStamp { get => _hasCustomStamp; set => SetProperty(ref _hasCustomStamp, value); }

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
        /// Geliştirici Merkezi butonunun görünürlüğü. Yetki <see cref="Diagnostics.Authorization.DiagnosticAccessGuard"/>
        /// üzerinden hesaplanır; menü gizleme tek başına güvenlik sayılmaz, servisler de ayrıca denetler.
        /// </summary>
        public bool IsDevCenterAvailable => _diagnosticGuard?.IsAuthorized == true || _authorizationService.IsAuthorized(ApplicationPermission.AccessSettings);

        /// <summary>
        /// Geliştirici Merkezi'ni ayrı pencerede açar. Yetkisizse pencere hiç oluşturulmaz.
        /// </summary>
        [RelayCommand]
        private void OpenDevCenter()
        {
            if (_diagnosticGuard is null)
            {
                _toastService?.ShowError("Geliştirici Merkezi kullanılamıyor", "Tanılama altyapısı bu oturumda yüklenmedi.");
                return;
            }

            if (!_diagnosticGuard.IsAuthorized)
            {
                _toastService?.ShowError("Yetkisiz işlem", _diagnosticGuard.DenialReason);
                return;
            }

            KamatekCrm.ViewModels.DevCenter.DevCenterViewModel? viewModel = null;
            try
            {
                viewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetRequiredService<KamatekCrm.ViewModels.DevCenter.DevCenterViewModel>(_serviceProvider ?? App.ServiceProvider);
                var window = new KamatekCrm.Views.DevCenter.DevCenterWindow(viewModel);
                window.Owner = System.Windows.Application.Current?.MainWindow;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                // Pencere/XAML yükleme hatası ana uygulamayı düşürmemeli.
                viewModel?.Dispose();
                _toastService?.ShowError("Geliştirici Tanılama Merkezi açılamadı", ex.Message);
                System.Diagnostics.Debug.WriteLine($"Dev Center modal error: {ex}");
            }
        }

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

        [RelayCommand]
        private async Task PreviewLegacyLedger()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var result = await _retailTransactions.PreviewLegacyLedgerAsync();
                if (result.IsFailure || result.Value is null)
                {
                    _toastService?.ShowError(result.Error);
                    return;
                }

                _legacyLedgerPreview = result.Value;
                LegacyLedgerIssues.Clear();
                foreach (var issue in result.Value.Issues) LegacyLedgerIssues.Add(issue);
                LegacyLedgerSummary = result.Value.Issues.Count == 0
                    ? "Düzeltme gerektiren eski cari satış bulunamadı. Veri yazılmadı."
                    : $"{result.Value.Issues.Count} satış / {result.Value.TotalCorrection:N2} ₺ bakiye farkı bulundu. Önizleme veri yazmadı.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ApplyLegacyLedgerCorrections()
        {
            if (IsBusy || _legacyLedgerPreview is null || _legacyLedgerPreview.Issues.Count == 0)
            {
                _toastService?.ShowWarning("Önce düzeltme önizlemesini çalıştırın.");
                return;
            }

            var confirm = MessageBox.Show(
                $"{_legacyLedgerPreview.Issues.Count} eski satış için {_legacyLedgerPreview.TotalCorrection:N2} ₺ telafi hareketi eklenecek.\n\nMevcut kayıtlar değiştirilmeyecek veya silinmeyecek. Devam edilsin mi?",
                "Cari Düzeltme Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                var result = await _retailTransactions.ApplyLegacyLedgerCorrectionsAsync(
                    _legacyLedgerPreview.Issues.Select(item => item.ReconciliationKey).ToArray(),
                    App.CurrentUser?.Username ?? "Sistem");
                if (result.IsFailure)
                {
                    _toastService?.ShowError(result.Error);
                    return;
                }

                _toastService?.ShowSuccess($"{result.Value} idempotent telafi hareketi eklendi.");
            }
            finally
            {
                IsBusy = false;
            }

            await PreviewLegacyLedger();
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
                        var profile = AppSettings.GetBrandProfile();
                        _companyName = profile.CompanyName;
                        _companySubtitle = profile.CompanySubtitle;
                        _companySloganOrBranch = profile.CompanySloganOrBranch;
                        _companyAddress = profile.Address;
                        _companyPhone = profile.Phone;
                        _companyEmail = profile.Email;
                        _website = profile.Website;
                        _taxOffice = profile.TaxOffice;
                        _taxNumber = profile.TaxNumber;
                        _tradeRegistryNo = profile.TradeRegistryNo;
                        _mersisNo = profile.MersisNo;
                        _bankName = profile.BankName;
                        _accountHolder = profile.AccountHolder;
                        _ibanTry = profile.IbanTry;
                        _ibanUsd = profile.IbanUsd;
                        _ibanEur = profile.IbanEur;
                        _companyIban = !string.IsNullOrWhiteSpace(profile.IbanTry) ? profile.IbanTry : (dto.Iban ?? "");
                        _customLogoPath = profile.CustomLogoPath;
                        _customLogoBase64 = profile.CustomLogoBase64;
                        _stampSignatureBase64 = profile.StampSignatureBase64;
                        _primaryBrandColorHex = profile.PrimaryBrandColorHex;
                        _accentBrandColorHex = profile.AccentBrandColorHex;
                        _quoteValidityDays = profile.QuoteValidityDays;
                        _quoteTermsText = profile.QuoteTermsText;
                        _serviceFormTermsText = profile.ServiceFormTermsText;
                        _warrantyTermsText = profile.WarrantyTermsText;
                        _showUnitPricesInPdf = profile.ShowUnitPricesInPdf;
                        _showProductImagesInPdf = profile.ShowProductImagesInPdf;
                        _showStampInPdf = profile.ShowStampInPdf;
                        _watermarkText = profile.WatermarkText;

                        _hasCustomLogo = !string.IsNullOrWhiteSpace(_customLogoBase64) || (!string.IsNullOrWhiteSpace(_customLogoPath) && File.Exists(_customLogoPath));
                        _hasCustomStamp = !string.IsNullOrWhiteSpace(_stampSignatureBase64);

                        RefreshLogoPreview();
                        RefreshStampPreview();

                        _companyName = dto.CompanyName ?? profile.CompanyName;
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
                    CompanySubtitle = CompanySubtitle,
                    CompanySloganOrBranch = CompanySloganOrBranch,
                    Website = Website,
                    TradeRegistryNo = TradeRegistryNo,
                    MersisNo = MersisNo,
                    BankName = BankName,
                    AccountHolder = AccountHolder,
                    IbanTry = !string.IsNullOrWhiteSpace(IbanTry) ? IbanTry : CompanyIban,
                    IbanUsd = IbanUsd,
                    IbanEur = IbanEur,
                    CustomLogoPath = CustomLogoPath,
                    CustomLogoBase64 = CustomLogoBase64,
                    StampSignatureBase64 = StampSignatureBase64,
                    PrimaryBrandColorHex = PrimaryBrandColorHex,
                    AccentBrandColorHex = AccentBrandColorHex,
                    QuoteValidityDays = QuoteValidityDays,
                    QuoteTermsText = QuoteTermsText,
                    ServiceFormTermsText = ServiceFormTermsText,
                    WarrantyTermsText = WarrantyTermsText,
                    ShowUnitPricesInPdf = ShowUnitPricesInPdf,
                    ShowProductImagesInPdf = ShowProductImagesInPdf,
                    ShowStampInPdf = ShowStampInPdf,
                    WatermarkText = WatermarkText,
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

                var profile = new CompanyBrandProfile
                {
                    CompanyName = CompanyName,
                    CompanySubtitle = CompanySubtitle,
                    CompanySloganOrBranch = CompanySloganOrBranch,
                    Address = CompanyAddress,
                    Phone = CompanyPhone,
                    Email = CompanyEmail,
                    Website = Website,
                    TaxOffice = TaxOffice,
                    TaxNumber = TaxNumber,
                    TradeRegistryNo = TradeRegistryNo,
                    MersisNo = MersisNo,
                    BankName = BankName,
                    AccountHolder = AccountHolder,
                    IbanTry = !string.IsNullOrWhiteSpace(IbanTry) ? IbanTry : CompanyIban,
                    IbanUsd = IbanUsd,
                    IbanEur = IbanEur,
                    CustomLogoPath = CustomLogoPath,
                    CustomLogoBase64 = CustomLogoBase64,
                    StampSignatureBase64 = StampSignatureBase64,
                    PrimaryBrandColorHex = PrimaryBrandColorHex,
                    AccentBrandColorHex = AccentBrandColorHex,
                    QuoteValidityDays = QuoteValidityDays,
                    QuoteTermsText = QuoteTermsText,
                    ServiceFormTermsText = ServiceFormTermsText,
                    WarrantyTermsText = WarrantyTermsText,
                    ShowUnitPricesInPdf = ShowUnitPricesInPdf,
                    ShowProductImagesInPdf = ShowProductImagesInPdf,
                    ShowStampInPdf = ShowStampInPdf,
                    WatermarkText = WatermarkText
                };
                AppSettings.SaveBrandProfile(profile);
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
            _toastService?.ShowSuccess("Firma ve PDF marka ayarları başarıyla kaydedildi.");
        }

        #region Branding & PDF Studio Methods & Commands

        private void RefreshLogoPreview()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(CustomLogoBase64))
                {
                    byte[] bytes = Convert.FromBase64String(CustomLogoBase64);
                    LogoPreviewSource = LoadBitmapFromBytes(bytes);
                    HasCustomLogo = true;
                    return;
                }

                if (!string.IsNullOrWhiteSpace(CustomLogoPath) && File.Exists(CustomLogoPath))
                {
                    byte[] bytes = File.ReadAllBytes(CustomLogoPath);
                    LogoPreviewSource = LoadBitmapFromBytes(bytes);
                    HasCustomLogo = true;
                    return;
                }

                LoadDefaultLogoPreview();
            }
            catch
            {
                LoadDefaultLogoPreview();
            }
        }

        private void LoadDefaultLogoPreview()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/Images/KamatekLogo.png");
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                LogoPreviewSource = bitmap;
                HasCustomLogo = false;
            }
            catch
            {
                LogoPreviewSource = null;
                HasCustomLogo = false;
            }
        }

        private void RefreshStampPreview()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(StampSignatureBase64))
                {
                    byte[] bytes = Convert.FromBase64String(StampSignatureBase64);
                    StampPreviewSource = LoadBitmapFromBytes(bytes);
                    HasCustomStamp = true;
                }
                else
                {
                    StampPreviewSource = null;
                    HasCustomStamp = false;
                }
            }
            catch
            {
                StampPreviewSource = null;
                HasCustomStamp = false;
            }
        }

        private static BitmapImage LoadBitmapFromBytes(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        [RelayCommand]
        private async Task UploadLogoAsync()
        {
            if (!EnsureSettingsAuthorized()) return;

            string? selectedPath = null;
            if (_dialogService != null)
            {
                selectedPath = await _dialogService.ShowOpenFileDialogAsync("Kurumsal Logo Seçin", "Resim Dosyaları (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg");
            }
            else
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Kurumsal Logo Seçin",
                    Filter = "Resim Dosyaları (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
                };
                if (dialog.ShowDialog() == true)
                    selectedPath = dialog.FileName;
            }

            if (string.IsNullOrWhiteSpace(selectedPath)) return;

            try
            {
                FileSecurityValidator.ValidateImage(selectedPath);
                byte[] bytes = await File.ReadAllBytesAsync(selectedPath);
                CustomLogoBase64 = Convert.ToBase64String(bytes);
                CustomLogoPath = selectedPath;
                LogoPreviewSource = LoadBitmapFromBytes(bytes);
                HasCustomLogo = true;

                SaveCompanySettings();
                _toastService?.ShowSuccess("Kurumsal logo başarıyla yüklendi ve kaydedildi.");
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Logo Yükleme Hatası", ex.Message);
            }
        }

        [RelayCommand]
        private void ResetToDefaultBrand()
        {
            if (!EnsureSettingsAuthorized()) return;

            var defaultProfile = CompanyBrandProfile.CreateDefaultKamatek();
            CompanyName = defaultProfile.CompanyName;
            CompanySubtitle = defaultProfile.CompanySubtitle;
            CompanySloganOrBranch = defaultProfile.CompanySloganOrBranch;
            CompanyAddress = defaultProfile.Address;
            CompanyPhone = defaultProfile.Phone;
            CompanyEmail = defaultProfile.Email;
            Website = defaultProfile.Website;
            TaxOffice = defaultProfile.TaxOffice;
            TaxNumber = defaultProfile.TaxNumber;
            TradeRegistryNo = defaultProfile.TradeRegistryNo;
            MersisNo = defaultProfile.MersisNo;
            BankName = defaultProfile.BankName;
            AccountHolder = defaultProfile.AccountHolder;
            IbanTry = defaultProfile.IbanTry;
            IbanUsd = defaultProfile.IbanUsd;
            IbanEur = defaultProfile.IbanEur;
            CompanyIban = defaultProfile.IbanTry;
            CustomLogoPath = "";
            CustomLogoBase64 = "";
            StampSignatureBase64 = "";
            PrimaryBrandColorHex = defaultProfile.PrimaryBrandColorHex;
            AccentBrandColorHex = defaultProfile.AccentBrandColorHex;
            QuoteValidityDays = defaultProfile.QuoteValidityDays;
            QuoteTermsText = defaultProfile.QuoteTermsText;
            ServiceFormTermsText = defaultProfile.ServiceFormTermsText;
            WarrantyTermsText = defaultProfile.WarrantyTermsText;
            ShowUnitPricesInPdf = defaultProfile.ShowUnitPricesInPdf;
            ShowProductImagesInPdf = defaultProfile.ShowProductImagesInPdf;
            ShowStampInPdf = defaultProfile.ShowStampInPdf;
            WatermarkText = defaultProfile.WatermarkText;

            LoadDefaultLogoPreview();
            StampPreviewSource = null;
            HasCustomLogo = false;
            HasCustomStamp = false;

            SaveCompanySettings();
            _toastService?.ShowSuccess("Varsayılan Kamatek kurumsal kimliğine dönüldü.");
        }

        [RelayCommand]
        private async Task UploadStampAsync()
        {
            if (!EnsureSettingsAuthorized()) return;

            string? selectedPath = null;
            if (_dialogService != null)
            {
                selectedPath = await _dialogService.ShowOpenFileDialogAsync("Dijital Kaşe / İmza Seçin", "Şeffaf PNG Dosyaları (*.png)|*.png|Tüm Resimler (*.png;*.jpg)|*.png;*.jpg");
            }
            else
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Dijital Kaşe / İmza Seçin",
                    Filter = "Şeffaf PNG Dosyaları (*.png)|*.png|Tüm Resimler (*.png;*.jpg)|*.png;*.jpg"
                };
                if (dialog.ShowDialog() == true)
                    selectedPath = dialog.FileName;
            }

            if (string.IsNullOrWhiteSpace(selectedPath)) return;

            try
            {
                FileSecurityValidator.ValidateImage(selectedPath);
                byte[] bytes = await File.ReadAllBytesAsync(selectedPath);
                StampSignatureBase64 = Convert.ToBase64String(bytes);
                StampPreviewSource = LoadBitmapFromBytes(bytes);
                HasCustomStamp = true;

                SaveCompanySettings();
                _toastService?.ShowSuccess("Dijital kaşe / imza başarıyla yüklendi.");
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Kaşe Yükleme Hatası", ex.Message);
            }
        }

        [RelayCommand]
        private void RemoveStamp()
        {
            if (!EnsureSettingsAuthorized()) return;

            StampSignatureBase64 = "";
            StampPreviewSource = null;
            HasCustomStamp = false;

            SaveCompanySettings();
            _toastService?.ShowInfo("Dijital kaşe kaldırıldı.");
        }

        [RelayCommand]
        private void ApplyBrandPalette(string paletteName)
        {
            switch (paletteName)
            {
                case "Kamatek":
                    PrimaryBrandColorHex = "#182649";
                    AccentBrandColorHex = "#D11920";
                    break;
                case "CorporateBlue":
                    PrimaryBrandColorHex = "#0D47A1";
                    AccentBrandColorHex = "#1E88E5";
                    break;
                case "ModernAnthracite":
                    PrimaryBrandColorHex = "#263238";
                    AccentBrandColorHex = "#FF6F00";
                    break;
                case "SecurityGreen":
                    PrimaryBrandColorHex = "#1B5E20";
                    AccentBrandColorHex = "#2E7D32";
                    break;
            }
            _toastService?.ShowInfo($"Kurumsal renk paleti uygulandı: {paletteName}");
        }

        [RelayCommand]
        private void PreviewSamplePdf()
        {
            try
            {
                var currentProfile = new CompanyBrandProfile
                {
                    CompanyName = CompanyName,
                    CompanySubtitle = CompanySubtitle,
                    CompanySloganOrBranch = CompanySloganOrBranch,
                    Address = CompanyAddress,
                    Phone = CompanyPhone,
                    Email = CompanyEmail,
                    Website = Website,
                    TaxOffice = TaxOffice,
                    TaxNumber = TaxNumber,
                    TradeRegistryNo = TradeRegistryNo,
                    MersisNo = MersisNo,
                    BankName = BankName,
                    AccountHolder = AccountHolder,
                    IbanTry = !string.IsNullOrWhiteSpace(IbanTry) ? IbanTry : CompanyIban,
                    IbanUsd = IbanUsd,
                    IbanEur = IbanEur,
                    CustomLogoPath = CustomLogoPath,
                    CustomLogoBase64 = CustomLogoBase64,
                    StampSignatureBase64 = StampSignatureBase64,
                    PrimaryBrandColorHex = PrimaryBrandColorHex,
                    AccentBrandColorHex = AccentBrandColorHex,
                    QuoteValidityDays = QuoteValidityDays,
                    QuoteTermsText = QuoteTermsText,
                    ServiceFormTermsText = ServiceFormTermsText,
                    WarrantyTermsText = WarrantyTermsText,
                    ShowUnitPricesInPdf = ShowUnitPricesInPdf,
                    ShowProductImagesInPdf = ShowProductImagesInPdf,
                    ShowStampInPdf = ShowStampInPdf,
                    WatermarkText = WatermarkText
                };

                var pdfService = _serviceProvider?.GetService(typeof(PdfService)) as PdfService 
                                 ?? new PdfService(null!, null!);

                string tempPdf = pdfService.GenerateSampleQuotePreviewPdf(currentProfile);

                if (File.Exists(tempPdf))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = tempPdf,
                        UseShellExecute = true
                    });
                    _toastService?.ShowSuccess("Örnek Teklif PDF'i oluşturuldu ve açıldı.");
                }
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Örnek PDF Önizleme Hatası", ex.Message);
            }
        }

        #endregion

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

        #region Auto-Update System Properties & Commands

        private bool _checkForUpdatesOnStartup = true;
        public bool CheckForUpdatesOnStartup
        {
            get => _checkForUpdatesOnStartup;
            set
            {
                if (SetProperty(ref _checkForUpdatesOnStartup, value))
                    SaveUpdateSettings();
            }
        }

        private bool _autoDownloadUpdates;
        public bool AutoDownloadUpdates
        {
            get => _autoDownloadUpdates;
            set
            {
                if (SetProperty(ref _autoDownloadUpdates, value))
                    SaveUpdateSettings();
            }
        }

        private string _updateChannel = "Stable";
        public string UpdateChannel
        {
            get => _updateChannel;
            set
            {
                if (value != _updateChannel)
                {
                    if (!EnsureSettingsAuthorized()) return;
                    if (SetProperty(ref _updateChannel, value))
                        SaveUpdateSettings();
                }
            }
        }

        public ObservableCollection<string> AvailableUpdateChannels { get; } = new() { "Stable", "Beta" };

        private bool _installOnClose;
        public bool InstallOnClose
        {
            get => _installOnClose;
            set
            {
                if (SetProperty(ref _installOnClose, value))
                    SaveUpdateSettings();
            }
        }

        public string CurrentAppVersionText => _updateService?.CurrentVersion ?? "v1.0.0";

        private string _lastCheckTimeText = "Henüz kontrol edilmedi";
        public string LastCheckTimeText
        {
            get => _lastCheckTimeText;
            set => SetProperty(ref _lastCheckTimeText, value);
        }

        private string _lastDownloadedVersionText = "İndirilmiş sürüm yok";
        public string LastDownloadedVersionText
        {
            get => _lastDownloadedVersionText;
            set => SetProperty(ref _lastDownloadedVersionText, value);
        }

        private bool _isUpdateCheckBusy;
        public bool IsUpdateCheckBusy
        {
            get => _isUpdateCheckBusy;
            set => SetProperty(ref _isUpdateCheckBusy, value);
        }

        public bool IsUpdateDownloaded => _updateService?.IsUpdateDownloaded ?? false;

        private void LoadUpdateSettings()
        {
            if (_updateService == null) return;
            var settings = _updateService.GetSettings();
            _checkForUpdatesOnStartup = settings.CheckForUpdatesOnStartup;
            _autoDownloadUpdates = settings.AutoDownloadUpdates;
            _updateChannel = settings.UpdateChannel;
            _installOnClose = settings.InstallOnClose;

            LastCheckTimeText = settings.LastCheckTime.HasValue
                ? settings.LastCheckTime.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
                : "Henüz kontrol edilmedi";

            LastDownloadedVersionText = !string.IsNullOrWhiteSpace(settings.LastDownloadedVersion)
                ? settings.LastDownloadedVersion
                : "İndirilmiş sürüm yok";

            OnPropertyChanged(nameof(CheckForUpdatesOnStartup));
            OnPropertyChanged(nameof(AutoDownloadUpdates));
            OnPropertyChanged(nameof(UpdateChannel));
            OnPropertyChanged(nameof(InstallOnClose));
            OnPropertyChanged(nameof(IsUpdateDownloaded));
        }

        private void SaveUpdateSettings()
        {
            if (_updateService == null) return;
            var settings = _updateService.GetSettings();
            settings.CheckForUpdatesOnStartup = CheckForUpdatesOnStartup;
            settings.AutoDownloadUpdates = AutoDownloadUpdates;
            settings.UpdateChannel = UpdateChannel;
            settings.InstallOnClose = InstallOnClose;
            _updateService.SaveSettings(settings);
        }

        [RelayCommand]
        private async Task CheckForUpdatesNowAsync()
        {
            if (_updateService == null)
            {
                _toastService?.ShowWarning("Güncelleme servisi aktif değil.");
                return;
            }

            IsUpdateCheckBusy = true;
            try
            {
                var update = await _updateService.CheckForUpdatesAsync(isAutoCheck: false);
                LoadUpdateSettings();

                if (update != null)
                {
                    var updateWindow = new Views.UpdateNotificationWindow(_updateService)
                    {
                        Owner = System.Windows.Application.Current.MainWindow
                    };
                    updateWindow.ShowDialog();
                    OnPropertyChanged(nameof(IsUpdateDownloaded));
                }
                else
                {
                    _toastService?.ShowInfo("Uygulamanız güncel. Yeni bir sürüm bulunamadı.");
                }
            }
            finally
            {
                IsUpdateCheckBusy = false;
            }
        }

        [RelayCommand]
        private void ApplyUpdateNow()
        {
            if (_updateService == null || !_updateService.IsUpdateDownloaded) return;
            _updateService.ApplyUpdateAndRestart();
        }

        #endregion
    }
}
