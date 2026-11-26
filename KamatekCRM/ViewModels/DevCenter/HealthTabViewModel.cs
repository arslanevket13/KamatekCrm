using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Diagnostics.Authorization;
using KamatekCrm.Diagnostics.Core;
using KamatekCrm.Helpers;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Services;
using KamatekCrm.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Serilog;

namespace KamatekCrm.ViewModels.DevCenter
{
    /// <summary>
    /// API ve veritabanı sağlığını gerçek çağrılarla kontrol eder.
    /// Ağır işlem yoktur; her kontrol kısa zaman aşımıyla arka planda yapılır.
    /// </summary>
    public sealed partial class HealthTabViewModel : DevCenterTabViewModel
    {
        private const string MaskedPasswordPlaceholder = "••••••••••••••••••••••••";

        private readonly DiagnosticDataService _data;
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly DiagnosticAccessGuard? _accessGuard;
        private readonly IToastService? _toastService;
        private readonly IManagedPostgresService? _managedPostgresService;

        private string _databaseStatus = "Kontrol edilmedi";
        private string _apiStatus = "Kontrol edilmedi";
        private string _lastCheckedText = "Henüz kontrol edilmedi";
        private bool _isBusy;

        private bool _isManagedLocalPostgres;
        private string _dbHost = "-";
        private int _dbPort = 5432;
        private string _dbName = "-";
        private string _dbUsername = "-";
        private string _managedDataDirectory = string.Empty;
        private string _actualPassword = string.Empty;
        private bool _isPasswordRevealed;
        private CancellationTokenSource? _clipboardClearCts;

        public HealthTabViewModel(
            DiagnosticDataService data,
            IDbContextFactory<AppDbContext> dbContextFactory,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            DiagnosticAccessGuard? accessGuard = null,
            IToastService? toastService = null,
            IManagedPostgresService? managedPostgresService = null)
            : base("API / DB Sağlığı", "🩺")
        {
            _data = data;
            _dbContextFactory = dbContextFactory;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _accessGuard = accessGuard;
            _toastService = toastService;
            _managedPostgresService = managedPostgresService;

            LoadConnectionInfo();
        }

        public string DatabaseStatus { get => _databaseStatus; private set => SetProperty(ref _databaseStatus, value); }
        public string ApiStatus { get => _apiStatus; private set => SetProperty(ref _apiStatus, value); }
        public string LastCheckedText { get => _lastCheckedText; private set => SetProperty(ref _lastCheckedText, value); }
        public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

        public bool IsManagedLocalPostgres
        {
            get => _isManagedLocalPostgres;
            private set
            {
                if (SetProperty(ref _isManagedLocalPostgres, value))
                {
                    OnPropertyChanged(nameof(ConnectionModeText));
                }
            }
        }

        public string DbHost
        {
            get => _dbHost;
            private set
            {
                if (SetProperty(ref _dbHost, value))
                {
                    OnPropertyChanged(nameof(HostAndPortText));
                }
            }
        }

        public int DbPort
        {
            get => _dbPort;
            private set
            {
                if (SetProperty(ref _dbPort, value))
                {
                    OnPropertyChanged(nameof(HostAndPortText));
                }
            }
        }

        public string DbName { get => _dbName; private set => SetProperty(ref _dbName, value); }
        public string DbUsername { get => _dbUsername; private set => SetProperty(ref _dbUsername, value); }

        public string ManagedDataDirectory
        {
            get => _managedDataDirectory;
            private set
            {
                if (SetProperty(ref _managedDataDirectory, value))
                {
                    OnPropertyChanged(nameof(ManagedDataDirectoryDisplay));
                    OpenDataDirectoryCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsPasswordRevealed
        {
            get => _isPasswordRevealed;
            private set
            {
                if (SetProperty(ref _isPasswordRevealed, value))
                {
                    OnPropertyChanged(nameof(DisplayedPassword));
                    OnPropertyChanged(nameof(TogglePasswordButtonText));
                }
            }
        }

        public string DisplayedPassword =>
            IsPasswordRevealed && CanExecuteAuthorizedCommand()
                ? (string.IsNullOrEmpty(_actualPassword) ? "(Tanımsız)" : _actualPassword)
                : MaskedPasswordPlaceholder;

        public string ConnectionModeText => IsManagedLocalPostgres ? "Yönetilen Yerel" : "Harici";
        public string HostAndPortText => $"{DbHost}:{DbPort}";
        public string ManagedDataDirectoryDisplay =>
            string.IsNullOrWhiteSpace(ManagedDataDirectory)
                ? "Harici sunucu (yerel veri dizini yok)"
                : ManagedDataDirectory;

        public string TogglePasswordButtonText => IsPasswordRevealed ? "Parolayı Gizle" : "Parolayı Göster";

        public override Task RefreshAsync() => CheckAsync();

        [RelayCommand]
        private async Task CheckAsync()
        {
            if (IsBusy)
            {
                return;
            }

            _data.EnsureAccess();
            IsBusy = true;

            try
            {
                LoadConnectionInfo();
                DatabaseStatus = await CheckDatabaseAsync();
                ApiStatus = await CheckApiAsync();
                LastCheckedText = $"Son kontrol: {DateTime.Now:HH:mm:ss}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanExecuteAuthorizedCommand() => _accessGuard?.IsAuthorized == true;

        private bool CanOpenDataDirectory() =>
            CanExecuteAuthorizedCommand()
            && !string.IsNullOrWhiteSpace(ManagedDataDirectory)
            && Directory.Exists(ManagedDataDirectory);

        [RelayCommand(CanExecute = nameof(CanExecuteAuthorizedCommand))]
        private void TogglePasswordVisibility()
        {
            _data.EnsureAccess();
            IsPasswordRevealed = !IsPasswordRevealed;
        }

        [RelayCommand(CanExecute = nameof(CanExecuteAuthorizedCommand))]
        private void CopyPassword()
        {
            _data.EnsureAccess();

            if (string.IsNullOrEmpty(_actualPassword))
            {
                _toastService?.ShowWarning("Aktif bağlantı dizesinde parola bulunamadı.");
                return;
            }

            if (!TrySetClipboardText(_actualPassword))
            {
                _toastService?.ShowError("Parola panoya kopyalanamadı.");
                return;
            }

            _toastService?.ShowSuccess("Parola panoya kopyalandı (30 sn sonra temizlenir)");
            ScheduleClipboardAutoClear(_actualPassword);
        }

        [RelayCommand(CanExecute = nameof(CanExecuteAuthorizedCommand))]
        private void CopyPgAdminSummary()
        {
            _data.EnsureAccess();

            var summary =
                $"Host: {DbHost}{Environment.NewLine}" +
                $"Port: {DbPort}{Environment.NewLine}" +
                $"Database: {DbName}{Environment.NewLine}" +
                $"Username: {DbUsername}{Environment.NewLine}" +
                $"Password: {_actualPassword}";

            if (!TrySetClipboardText(summary))
            {
                _toastService?.ShowError("pgAdmin bağlantı bilgileri panoya kopyalanamadı.");
                return;
            }

            _toastService?.ShowSuccess("pgAdmin bağlantı bilgileri panoya kopyalandı (30 sn sonra temizlenir)");
            ScheduleClipboardAutoClear(summary);
        }

        [RelayCommand(CanExecute = nameof(CanOpenDataDirectory))]
        private void OpenDataDirectory()
        {
            _data.EnsureAccess();

            if (string.IsNullOrWhiteSpace(ManagedDataDirectory) || !Directory.Exists(ManagedDataDirectory))
            {
                _toastService?.ShowWarning("Yönetilen PostgreSQL veri dizini bulunamadı.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{ManagedDataDirectory}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "PostgreSQL veri klasörü açılamadı: {Directory}", ManagedDataDirectory);
                _toastService?.ShowError("Veri klasörü açılamadı.");
            }
        }

        private void LoadConnectionInfo()
        {
            IsManagedLocalPostgres = AppSettings.IsManagedLocalPostgres;
            _actualPassword = string.Empty;

            try
            {
                var activeConnectionString = AppSettings.ActiveConnectionString;
                if (!string.IsNullOrWhiteSpace(activeConnectionString))
                {
                    var builder = new NpgsqlConnectionStringBuilder(activeConnectionString);
                    DbHost = string.IsNullOrWhiteSpace(builder.Host) ? "-" : builder.Host;
                    DbPort = builder.Port > 0
                        ? builder.Port
                        : (IsManagedLocalPostgres ? AppSettings.ManagedPostgresPort : 5432);
                    DbName = string.IsNullOrWhiteSpace(builder.Database) ? "-" : builder.Database;
                    DbUsername = string.IsNullOrWhiteSpace(builder.Username) ? "-" : builder.Username;
                    _actualPassword = builder.Password ?? string.Empty;
                }
                else
                {
                    DbHost = IsManagedLocalPostgres ? "127.0.0.1" : "-";
                    DbPort = IsManagedLocalPostgres ? AppSettings.ManagedPostgresPort : 5432;
                    DbName = "-";
                    DbUsername = "-";
                }
            }
            catch (Exception ex)
            {
                // Güvenlik: bağlantı dizesi veya parola asla loglanmaz; yalnızca istisna tipi kaydedilir.
                Log.Warning("Veritabanı bağlantı bilgileri ayrıştırılamadı ({ExceptionType}).", ex.GetType().Name);
                DbHost = "-";
                DbPort = IsManagedLocalPostgres ? AppSettings.ManagedPostgresPort : 5432;
                DbName = "-";
                DbUsername = "-";
                _actualPassword = string.Empty;
            }

            ManagedDataDirectory = ResolveManagedDataDirectory();
            OnPropertyChanged(nameof(DisplayedPassword));
            NotifySecretCommandsCanExecuteChanged();
        }

        private string ResolveManagedDataDirectory()
        {
            if (!IsManagedLocalPostgres)
            {
                return string.Empty;
            }

            try
            {
                if (_managedPostgresService is not null)
                {
                    return _managedPostgresService.ResolveDataDirectory(out _);
                }

                var programDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "KamatekCRM",
                    "pgdata");
                if (Directory.Exists(programDataPath))
                {
                    return programDataPath;
                }

                var localAppDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "KamatekCRM",
                    "pgdata");
                return Directory.Exists(localAppDataPath) ? localAppDataPath : programDataPath;
            }
            catch (Exception ex)
            {
                Log.Warning("PostgreSQL veri dizini çözümlenemedi ({ExceptionType}).", ex.GetType().Name);
                return string.Empty;
            }
        }

        private void NotifySecretCommandsCanExecuteChanged()
        {
            TogglePasswordVisibilityCommand.NotifyCanExecuteChanged();
            CopyPasswordCommand.NotifyCanExecuteChanged();
            CopyPgAdminSummaryCommand.NotifyCanExecuteChanged();
            OpenDataDirectoryCommand.NotifyCanExecuteChanged();
        }

        private static bool TrySetClipboardText(string text)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                return true;
            }
            catch (Exception ex)
            {
                // Güvenlik: panoya yazılan metin (parola) loglanmaz.
                Log.Warning("Pano yazma işlemi başarısız oldu ({ExceptionType}).", ex.GetType().Name);
                return false;
            }
        }

        private void ScheduleClipboardAutoClear(string expectedSecret)
        {
            _clipboardClearCts?.Cancel();
            _clipboardClearCts?.Dispose();
            var cts = new CancellationTokenSource();
            _clipboardClearCts = cts;

            ClearClipboardAfterDelayAsync(expectedSecret, cts.Token)
                .SafeFireAndForget("HealthTabViewModel.ClearClipboardAfterDelayAsync");
        }

        private static async Task ClearClipboardAfterDelayAsync(string expectedSecret, CancellationToken ct)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Log.Debug("Önceki pano otomatik temizleme zamanlayıcısı yenilendi.");
                return;
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                return;
            }

            await dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsText() &&
                        string.Equals(System.Windows.Clipboard.GetText(), expectedSecret, StringComparison.Ordinal))
                    {
                        System.Windows.Clipboard.Clear();
                    }
                }
                catch (Exception ex)
                {
                    // Güvenlik: parola loglanmaz; yalnızca istisna tipi kaydedilir.
                    Log.Warning("Pano otomatik temizlenemedi ({ExceptionType}).", ex.GetType().Name);
                }
            });
        }

        private async Task<string> CheckDatabaseAsync()
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var canConnect = await dbContext.Database.CanConnectAsync(timeout.Token);
                return canConnect ? "Bağlı" : "Bağlantı kurulamadı";
            }
            catch (OperationCanceledException)
            {
                return "Zaman aşımı (5 sn)";
            }
            catch (Exception ex)
            {
                // Mesaj yerine yalnızca tip adı: bağlantı dizesi sızmasın.
                return $"Hata: {ex.GetType().Name}";
            }
        }

        private async Task<string> CheckApiAsync()
        {
            var baseUrl = _configuration["AppSettings:ApiBaseUrl"] ?? ProcessManager.API_LOCAL_URL;

            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                using var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/health");
                return response.IsSuccessStatusCode
                    ? $"Sağlıklı ({(int)response.StatusCode})"
                    : $"Yanıt kodu: {(int)response.StatusCode}";
            }
            catch (TaskCanceledException)
            {
                return "Zaman aşımı (5 sn)";
            }
            catch (Exception ex)
            {
                return $"Erişilemiyor: {ex.GetType().Name}";
            }
        }
    }
}
