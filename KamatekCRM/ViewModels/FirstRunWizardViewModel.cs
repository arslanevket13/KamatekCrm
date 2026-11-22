using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Events;
using KamatekCrm.Helpers;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Services;
using KamatekCrm.Settings;
using KamatekCrm.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Serilog;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// 3 Adımlı İlk Kurulum Sihirbazı ViewModel:
    /// Adım 1: Kurulum Tipi (Yerel Otonom PostgreSQL / LAN İstemcisi / Gelişmiş)
    /// Adım 2: İşletme Bilgileri
    /// Adım 3: Yönetici Hesabı & Çevrimdışı Kurtarma Anahtarı
    /// </summary>
    public partial class FirstRunWizardViewModel : ViewModelBase
    {
        private readonly IManagedPostgresService _managedPostgresService;
        private readonly IDatabaseInitializationService _databaseInitializationService;
        private readonly IDatabaseConnectionProvider _connectionProvider;
        private readonly NetworkDiscoveryService _discoveryService;
        private readonly IAuthService _authService;
        private readonly IRecoveryKeyService _recoveryKeyService;
        private readonly NavigationService _navigationService;
        private readonly IToastService _toastService;
        private readonly IServiceProvider _serviceProvider;

        public FirstRunWizardViewModel(
            IManagedPostgresService managedPostgresService,
            IDatabaseInitializationService databaseInitializationService,
            IDatabaseConnectionProvider connectionProvider,
            NetworkDiscoveryService discoveryService,
            IAuthService authService,
            IRecoveryKeyService recoveryKeyService,
            NavigationService navigationService,
            IToastService toastService,
            IServiceProvider serviceProvider)
        {
            _managedPostgresService = managedPostgresService;
            _databaseInitializationService = databaseInitializationService;
            _connectionProvider = connectionProvider;
            _discoveryService = discoveryService;
            _authService = authService;
            _recoveryKeyService = recoveryKeyService;
            _navigationService = navigationService;
            _toastService = toastService;
            _serviceProvider = serviceProvider;

            LoadExistingCompanyInfo();
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsStep1))]
        [NotifyPropertyChangedFor(nameof(IsStep2))]
        [NotifyPropertyChangedFor(nameof(IsStep3))]
        [NotifyPropertyChangedFor(nameof(IsStep1Done))]
        [NotifyPropertyChangedFor(nameof(IsStep2Done))]
        private int _currentStep = 1;

        public bool IsStep1 => CurrentStep == 1;
        public bool IsStep2 => CurrentStep == 2;
        public bool IsStep3 => CurrentStep == 3;
        public bool IsStep1Done => CurrentStep > 1;
        public bool IsStep2Done => CurrentStep > 2;

        // ===== ADIM 1: KURULUM TİPİ =====
        [ObservableProperty]
        private bool _allowLanAccess;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        [NotifyCanExecuteChangedFor(nameof(CompleteSetupCommand))]
        private bool _isBusy;

        public bool IsNotBusy => !IsBusy;

        [ObservableProperty]
        private string _statusMessage = "Kurulum tipini seçerek başlayın.";

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private bool _isScanningNetwork;

        [ObservableProperty]
        private DiscoveredServerInfo? _selectedServer;

        public ObservableCollection<DiscoveredServerInfo> DiscoveredServers { get; } = new();

        public bool HasDiscoveredServers => DiscoveredServers.Count > 0;

        // ===== ADIM 2: İŞLETME BİLGİLERİ =====
        [ObservableProperty]
        private string _companyName = string.Empty;

        [ObservableProperty]
        private string _companyPhone = string.Empty;

        [ObservableProperty]
        private string _companyCity = string.Empty;

        [ObservableProperty]
        private string _companyDistrict = string.Empty;

        [ObservableProperty]
        private string _logoPath = string.Empty;

        // ===== ADIM 3: YÖNETİCİ HESABI & KURTARMA ANAHTARI =====
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CompleteSetupCommand))]
        private string _adminFullName = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CompleteSetupCommand))]
        private string _adminUsername = "admin";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMinPasswordLength))]
        [NotifyPropertyChangedFor(nameof(HasLetterOrDigit))]
        [NotifyPropertyChangedFor(nameof(PasswordsMatch))]
        [NotifyCanExecuteChangedFor(nameof(CompleteSetupCommand))]
        private string _adminPassword = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PasswordsMatch))]
        [NotifyCanExecuteChangedFor(nameof(CompleteSetupCommand))]
        private string _adminPasswordConfirm = string.Empty;

        [ObservableProperty]
        private bool _keepLoggedIn = true;

        [ObservableProperty]
        private string _recoveryKey = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CompleteSetupCommand))]
        private bool _hasConfirmedRecoveryKey;

        public bool HasMinPasswordLength => !string.IsNullOrEmpty(AdminPassword) && AdminPassword.Length >= 6;
        public bool HasLetterOrDigit => !string.IsNullOrEmpty(AdminPassword) && AdminPassword.Any(char.IsLetterOrDigit);
        public bool PasswordsMatch => !string.IsNullOrEmpty(AdminPassword) && string.Equals(AdminPassword, AdminPasswordConfirm, StringComparison.Ordinal);

        public void Initialize(int startStep = 1)
        {
            ClearError();
            CurrentStep = startStep is >= 1 and <= 3 ? startStep : 1;
            if (CurrentStep == 3)
            {
                EnsureRecoveryKeyGenerated();
            }
        }

        private void LoadExistingCompanyInfo()
        {
            var brand = AppSettings.GetBrandProfile();
            if (!string.IsNullOrWhiteSpace(brand.CompanyName) &&
                !string.Equals(brand.CompanyName, "KAMATEK", StringComparison.OrdinalIgnoreCase))
            {
                CompanyName = brand.CompanyName;
            }
            if (!string.IsNullOrWhiteSpace(brand.Phone) &&
                !string.Equals(brand.Phone, "0222 230 40 50", StringComparison.Ordinal))
            {
                CompanyPhone = brand.Phone;
            }
            LogoPath = brand.CustomLogoPath ?? string.Empty;
        }

        private void EnsureRecoveryKeyGenerated()
        {
            if (string.IsNullOrWhiteSpace(RecoveryKey))
            {
                RecoveryKey = _recoveryKeyService.GenerateRecoveryKey();
            }
        }

        private void SetError(string message)
        {
            ErrorMessage = message;
            HasError = !string.IsNullOrWhiteSpace(message);
        }

        private void ClearError()
        {
            ErrorMessage = string.Empty;
            HasError = false;
        }

        [RelayCommand]
        private async Task SetupLocalPostgresAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            ClearError();

            try
            {
                var progress = new Progress<string>(msg => StatusMessage = msg);
                var pgResult = await _managedPostgresService.EnsureProvisionedAndRunningAsync(
                    AllowLanAccess,
                    progress,
                    CancellationToken.None);

                if (!pgResult.Success)
                {
                    SetError(pgResult.ErrorMessage ?? "Yerel PostgreSQL kurulumu başarısız oldu.");
                    StatusMessage = "Kurulum tamamlanamadı.";
                    return;
                }

                _connectionProvider.UpdateConnectionString(pgResult.ConnectionString);
                _connectionProvider.SetServerIp("127.0.0.1");

                StatusMessage = "Veritabanı tabloları oluşturuluyor (migration)...";
                var initResult = await _databaseInitializationService.InitializeAsync(
                    CancellationToken.None,
                    seedDefaultAdminIfEmpty: false);

                _connectionProvider.SetConnectionState(true);
                EventAggregator.Instance.Publish(new DatabaseConnectionRestoredEvent());

                if (initResult.HasAnyUsers)
                {
                    AppSettings.IsFirstRunCompleted = true;
                    _toastService.ShowSuccess("Veritabanı bağlantısı kuruldu. Mevcut hesabınızla giriş yapabilirsiniz.");
                    _navigationService.NavigateToLogin();
                    return;
                }

                StatusMessage = "Yerel veritabanı hazır. İşletme bilgilerinizi girebilirsiniz.";
                CurrentStep = 2;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "İlk kurulum yerel PostgreSQL hazırlığı başarısız oldu.");
                SetError($"Kurulum hatası: {ex.Message}");
                StatusMessage = "Kurulum sırasında hata oluştu.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ScanNetworkServersAsync()
        {
            if (IsScanningNetwork || IsBusy) return;
            IsScanningNetwork = true;
            ClearError();
            StatusMessage = "Yerel ağdaki KamatekCRM sunucuları taranıyor...";

            try
            {
                DiscoveredServers.Clear();
                OnPropertyChanged(nameof(HasDiscoveredServers));

                var servers = await _discoveryService.ScanSubnetServersAsync(
                    onServerFound: null,
                    cancellationToken: CancellationToken.None);

                foreach (var srv in servers)
                {
                    DiscoveredServers.Add(srv);
                }

                OnPropertyChanged(nameof(HasDiscoveredServers));
                SelectedServer = DiscoveredServers.FirstOrDefault();

                StatusMessage = DiscoveredServers.Count > 0
                    ? $"{DiscoveredServers.Count} sunucu bulundu. Bağlanmak istediğinizi seçin."
                    : "Ağda açık sunucu bulunamadı. Gelişmiş bağlantı ayarlarını kullanabilirsiniz.";
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "İlk kurulum ağ taraması başarısız oldu.");
                SetError($"Ağ tarama hatası: {ex.Message}");
            }
            finally
            {
                IsScanningNetwork = false;
            }
        }

        [RelayCommand]
        private async Task ConnectToDiscoveredServerAsync()
        {
            if (SelectedServer is null)
            {
                SetError("Lütfen listeden bir ana bilgisayar seçin.");
                return;
            }

            if (IsBusy) return;
            IsBusy = true;
            ClearError();
            StatusMessage = $"{SelectedServer.DisplayText} sunucusuna bağlanılıyor...";

            try
            {
                _connectionProvider.SetServerIp(SelectedServer.IpAddress);
                bool ok = await _connectionProvider.TestConnectionAsync(SelectedServer.IpAddress);
                if (!ok)
                {
                    SetError("Seçilen sunucuya mevcut bağlantı bilgileriyle ulaşılamadı. Gelişmiş bağlantı ekranından kullanıcı/parola bilgilerini girin.");
                    return;
                }

                AppSettings.IsManagedLocalPostgres = false;
                AppSettings.IsFirstRunCompleted = true;
                _connectionProvider.SetConnectionState(true);
                EventAggregator.Instance.Publish(new DatabaseConnectionRestoredEvent());

                _toastService.ShowSuccess($"Ana bilgisayara bağlanıldı: {SelectedServer.DisplayText}");
                _navigationService.NavigateToLogin();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Keşfedilen sunucuya bağlantı kurulamadı.");
                SetError($"Bağlantı hatası: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task OpenAdvancedConnectionAsync()
        {
            ClearError();
            var networkVm = _serviceProvider.GetRequiredService<NetworkSettingsViewModel>();
            networkVm.IsPreLoginMode = true;

            var parentWindow = System.Windows.Application.Current?.MainWindow;
            var workArea = SystemParameters.WorkArea;
            var settingsWindow = new Window
            {
                Title = "Gelişmiş PostgreSQL Bağlantı Ayarları",
                Content = new Views.NetworkSettingsView { DataContext = networkVm },
                Width = Math.Min(1100, workArea.Width * 0.92),
                Height = Math.Min(740, workArea.Height * 0.90),
                MinWidth = 860,
                MinHeight = 600,
                Owner = parentWindow,
                WindowStartupLocation = parentWindow is not null
                    ? WindowStartupLocation.CenterOwner
                    : WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize
            };

            settingsWindow.ShowDialog();
            networkVm.Dispose();

            if (string.IsNullOrWhiteSpace(_connectionProvider.GetConnectionString()))
                return;

            IsBusy = true;
            try
            {
                StatusMessage = "Yapılandırılan sunucu doğrulanıyor...";
                var initResult = await _databaseInitializationService.InitializeAsync(
                    CancellationToken.None,
                    seedDefaultAdminIfEmpty: false);

                _connectionProvider.SetConnectionState(true);
                EventAggregator.Instance.Publish(new DatabaseConnectionRestoredEvent());

                if (initResult.HasAnyUsers)
                {
                    AppSettings.IsFirstRunCompleted = true;
                    _navigationService.NavigateToLogin();
                }
                else
                {
                    CurrentStep = 2;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Gelişmiş bağlantı sonrası veritabanı doğrulanamadı.");
                SetError($"Bağlantı doğrulanamadı: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void BrowseLogo()
        {
            var dialog = new OpenFileDialog
            {
                Title = "İşletme Logosu Seç",
                Filter = "Görsel Dosyaları (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                LogoPath = dialog.FileName;
            }
        }

        [RelayCommand]
        private void SaveCompanyInfo()
        {
            ClearError();
            if (string.IsNullOrWhiteSpace(CompanyName))
            {
                SetError("Lütfen işletme adını girin veya 'Şimdilik Atla' butonunu kullanın.");
                return;
            }

            try
            {
                var profile = AppSettings.GetBrandProfile();
                profile.CompanyName = CompanyName.Trim();
                if (!string.IsNullOrWhiteSpace(CompanyPhone))
                {
                    profile.Phone = CompanyPhone.Trim();
                }

                var locationParts = new[] { CompanyDistrict?.Trim(), CompanyCity?.Trim() }
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToArray();
                if (locationParts.Length > 0)
                {
                    profile.Address = string.Join(" / ", locationParts);
                }

                if (!string.IsNullOrWhiteSpace(LogoPath) && File.Exists(LogoPath))
                {
                    profile.CustomLogoPath = LogoPath;
                    var bytes = File.ReadAllBytes(LogoPath);
                    profile.CustomLogoBase64 = Convert.ToBase64String(bytes);
                }

                AppSettings.SaveBrandProfile(profile);
                EnsureRecoveryKeyGenerated();
                CurrentStep = 3;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "İşletme bilgileri kaydedilirken hata oluştu.");
                SetError($"İşletme bilgileri kaydedilemedi: {ex.Message}");
            }
        }

        [RelayCommand]
        private void SkipCompanyInfo()
        {
            ClearError();
            EnsureRecoveryKeyGenerated();
            CurrentStep = 3;
        }

        [RelayCommand]
        private void GoBackToStep2()
        {
            ClearError();
            CurrentStep = 2;
        }

        [RelayCommand]
        private void CopyRecoveryKey()
        {
            if (string.IsNullOrWhiteSpace(RecoveryKey)) return;
            try
            {
                Clipboard.SetText(RecoveryKey);
                _toastService.ShowSuccess("Kurtarma anahtarı panoya kopyalandı.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Kurtarma anahtarı panoya kopyalanamadı.");
                SetError("Panoya kopyalama başarısız oldu; lütfen anahtarı elle not alın.");
            }
        }

        [RelayCommand]
        private void SaveRecoveryKeyToFile()
        {
            if (string.IsNullOrWhiteSpace(RecoveryKey)) return;
            var dialog = new SaveFileDialog
            {
                Title = "Kurtarma Anahtarını Kaydet",
                FileName = $"KamatekCRM-Kurtarma-Anahtari-{DateTime.Now:yyyyMMdd}.txt",
                Filter = "Metin Dosyası (*.txt)|*.txt"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var content =
                    "====================================================" + Environment.NewLine +
                    "KAMATEK CRM — YÖNETİCİ KURTARMA ANAHTARI" + Environment.NewLine +
                    "====================================================" + Environment.NewLine +
                    $"Oluşturulma Tarihi : {DateTime.Now:dd.MM.yyyy HH:mm}" + Environment.NewLine +
                    $"Yönetici Kullanıcı : {AdminUsername}" + Environment.NewLine +
                    $"Kurtarma Anahtarı  : {RecoveryKey}" + Environment.NewLine +
                    "====================================================" + Environment.NewLine +
                    "Bu anahtarı şifrenizi unuttuğunuzda internet bağlantısı" + Environment.NewLine +
                    "olmadan yeni şifre belirlemek için kullanabilirsiniz." + Environment.NewLine +
                    "Lütfen güvenli bir yerde saklayın." + Environment.NewLine;

                File.WriteAllText(dialog.FileName, content);
                _toastService.ShowSuccess("Kurtarma anahtarı metin dosyası olarak kaydedildi.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Kurtarma anahtarı dosyaya yazılamadı.");
                SetError($"Dosya kaydedilemedi: {ex.Message}");
            }
        }

        private bool CanCompleteSetup()
        {
            return !IsBusy
                && !string.IsNullOrWhiteSpace(AdminFullName)
                && !string.IsNullOrWhiteSpace(AdminUsername)
                && HasMinPasswordLength
                && PasswordsMatch
                && HasConfirmedRecoveryKey;
        }

        [RelayCommand(CanExecute = nameof(CanCompleteSetup))]
        private async Task CompleteSetupAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            ClearError();

            try
            {
                var result = await _authService.CreateInitialAdminAsync(
                    AdminFullName,
                    AdminUsername,
                    AdminPassword);

                if (!result.Success || result.CreatedUser is null)
                {
                    SetError(result.ErrorMessage ?? "Yönetici hesabı oluşturulamadı.");
                    return;
                }

                AppSettings.RecoveryKeyHash = _recoveryKeyService.HashRecoveryKey(RecoveryKey);
                AppSettings.IsFirstRunCompleted = true;

                if (KeepLoggedIn)
                {
                    AppSettings.SaveAutoLoginSession(result.CreatedUser.Id, result.CreatedUser.Username);
                }
                else
                {
                    AppSettings.ClearAutoLoginSession();
                }

                _toastService.ShowSuccess("Kurulum tamamlandı! KamatekCRM'e hoş geldiniz.");
                _navigationService.NavigateToMainContent();
            }
            catch (Exception ex)
            {
                var baseEx = ex.GetBaseException();
                if (baseEx is Npgsql.PostgresException pgEx)
                {
                    Log.Error(
                        ex,
                        "İlk kurulum tamamlanırken veritabanı hatası (SqlState={SqlState}, Column={Column}, Constraint={Constraint})",
                        pgEx.SqlState,
                        pgEx.ColumnName,
                        pgEx.ConstraintName);
                    SetError($"Kurulum tamamlanamadı (Kod: {pgEx.SqlState}, Sütun: {pgEx.ColumnName ?? "-"}, Kısıt: {pgEx.ConstraintName ?? "-"}): {pgEx.MessageText}");
                }
                else
                {
                    Log.Error(ex, "İlk kurulum tamamlanırken hata oluştu: {BaseMessage}", baseEx.Message);
                    SetError($"Kurulum tamamlanamadı: {baseEx.Message}");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
