using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KamatekCrm.Shared.Services;
using Serilog;
using Velopack;
using Velopack.Sources;

namespace KamatekCrm.Services.Update
{
    public class VelopackUpdateService : IUpdateService
    {
        private const string RepoUrl = "https://github.com/arslanevket13/KamatekCrm";
        private static readonly SemaphoreSlim _checkLock = new(1, 1);
        private static readonly SemaphoreSlim _downloadLock = new(1, 1);

        private readonly IToastService? _toastService;
        private readonly IDialogService? _dialogService;
        private UpdateSettings _settings;
        private int _downloadProgress;
        private bool _isChecking;
        private bool _isDownloading;
        private bool _isUpdateDownloaded;
        private UpdateCheckResult? _availableUpdate;
        private UpdateInfo? _velopackUpdateInfo;

        public event Action<int>? DownloadProgressChanged;
        public event Action<UpdateCheckResult>? UpdateAvailableNotification;

        public VelopackUpdateService(IToastService? toastService = null, IDialogService? dialogService = null)
        {
            _toastService = toastService;
            _dialogService = dialogService;
            _settings = LoadSettingsInternal();
        }

        public string CurrentVersion
        {
            get
            {
                try
                {
                    var asm = Assembly.GetExecutingAssembly();
                    var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                    if (!string.IsNullOrWhiteSpace(infoAttr?.InformationalVersion))
                    {
                        var versionPart = infoAttr.InformationalVersion.Split('+')[0];
                        return versionPart.StartsWith('v') ? versionPart : $"v{versionPart}";
                    }
                    var ver = asm.GetName().Version;
                    return ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v1.0.0";
                }
                catch
                {
                    return "v1.0.0";
                }
            }
        }

        public string UpdateChannel => _settings.UpdateChannel;

        public bool IsChecking
        {
            get => _isChecking;
            private set => _isChecking = value;
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            private set
            {
                _isDownloading = value;
            }
        }

        public int DownloadProgress
        {
            get => _downloadProgress;
            private set
            {
                _downloadProgress = value;
                DownloadProgressChanged?.Invoke(value);
            }
        }

        public bool IsUpdateDownloaded
        {
            get => _isUpdateDownloaded;
            private set => _isUpdateDownloaded = value;
        }

        public UpdateCheckResult? AvailableUpdate => _availableUpdate;

        public UpdateSettings GetSettings() => _settings;

        public void SaveSettings(UpdateSettings settings)
        {
            _settings = settings ?? new UpdateSettings();
            SaveSettingsInternal(_settings);
        }

        public async Task<UpdateCheckResult?> CheckForUpdatesAsync(bool isAutoCheck, CancellationToken ct = default)
        {
            if (!await _checkLock.WaitAsync(0, ct))
            {
                Log.Warning("Update check requested while another check is in progress.");
                return _availableUpdate;
            }

            IsChecking = true;
            try
            {
                Log.Information("=== Checking for updates (Current: {CurrentVersion}, Channel: {Channel}, AutoCheck: {IsAuto}) ===",
                    CurrentVersion, UpdateChannel, isAutoCheck);

                bool allowPrerelease = string.Equals(UpdateChannel, "Beta", StringComparison.OrdinalIgnoreCase);
                var source = new GithubSource(RepoUrl, accessToken: null, prerelease: allowPrerelease);
                var mgr = new UpdateManager(source);

                if (!mgr.IsInstalled)
                {
                    Log.Information("Uygulama paketlenmemiş geliştirme (Debug) modunda çalışıyor (IsInstalled = false). Güncelleme kontrolü atlandı.");
                    if (!isAutoCheck && _toastService != null)
                    {
                        _toastService.ShowInfo("Geliştirme modundasınız. Güncelleme kontrolü ve yükleme yalnızca kurulu uygulamada çalışır.");
                    }
                    return null;
                }

                _settings.LastCheckTime = DateTime.UtcNow;
                SaveSettingsInternal(_settings);

                var updateInfo = await mgr.CheckForUpdatesAsync();
                if (updateInfo is null)
                {
                    Log.Information("No update available on channel {Channel}.", UpdateChannel);
                    _availableUpdate = null;
                    _velopackUpdateInfo = null;
                    return null;
                }

                _velopackUpdateInfo = updateInfo;
                var targetRelease = updateInfo.TargetFullRelease;
                string targetVer = targetRelease.Version.ToString();
                if (!targetVer.StartsWith('v')) targetVer = $"v{targetVer}";

                long sizeBytes = targetRelease.Size;
                string notes = string.IsNullOrWhiteSpace(targetRelease.NotesMarkdown)
                    ? "Detaylı sürüm notu bulunmuyor."
                    : targetRelease.NotesMarkdown;

                _availableUpdate = new UpdateCheckResult(
                    TargetVersion: targetVer,
                    CurrentVersion: CurrentVersion,
                    ReleaseNotes: notes,
                    DownloadSizeBytes: sizeBytes,
                    ReleaseDate: DateTimeOffset.UtcNow,
                    IsPrerelease: allowPrerelease,
                    VelopackUpdateInfo: updateInfo);

                Log.Information("New update discovered: {TargetVersion} (Size: {Size} bytes, Prerelease: {IsPre})",
                    targetVer, sizeBytes, allowPrerelease);

                UpdateAvailableNotification?.Invoke(_availableUpdate);

                if (_settings.AutoDownloadUpdates && !IsDownloading && !IsUpdateDownloaded)
                {
                    _ = DownloadUpdateAsync(ct: ct);
                }

                return _availableUpdate;
            }
            catch (OperationCanceledException)
            {
                Log.Information("Update check cancelled by user/shutdown.");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check for updates: {ErrorMessage}", ex.Message);
                if (!isAutoCheck && _toastService != null)
                {
                    string errorMsg = MapExceptionToUserMessage(ex);
                    _toastService.ShowWarning(errorMsg);
                }
                return null;
            }
            finally
            {
                IsChecking = false;
                _checkLock.Release();
            }
        }

        public async Task<bool> DownloadUpdateAsync(IProgress<int>? progress = null, CancellationToken ct = default)
        {
            if (_velopackUpdateInfo is null)
            {
                Log.Warning("DownloadUpdateAsync called but no update info is available.");
                return false;
            }

            if (!await _downloadLock.WaitAsync(0, ct))
            {
                Log.Warning("Download requested while another download is in progress.");
                return false;
            }

            IsDownloading = true;
            DownloadProgress = 0;
            try
            {
                Log.Information("Starting update download for version {TargetVersion}...", AvailableUpdate?.TargetVersion);
                bool allowPrerelease = string.Equals(UpdateChannel, "Beta", StringComparison.OrdinalIgnoreCase);
                var source = new GithubSource(RepoUrl, accessToken: null, prerelease: allowPrerelease);
                var mgr = new UpdateManager(source);

                if (!mgr.IsInstalled)
                {
                    Log.Warning("DownloadUpdateAsync called in unpackaged dev mode (IsInstalled = false).");
                    _toastService?.ShowWarning("Geliştirme modundasınız. İndirme işlemi yalnızca kurulu uygulamada çalışır.");
                    return false;
                }

                await mgr.DownloadUpdatesAsync(_velopackUpdateInfo, p =>
                {
                    DownloadProgress = p;
                    progress?.Report(p);
                });

                IsUpdateDownloaded = true;
                _settings.LastDownloadedVersion = AvailableUpdate?.TargetVersion;
                SaveSettingsInternal(_settings);

                Log.Information("Update downloaded successfully: {TargetVersion}", AvailableUpdate?.TargetVersion);
                return true;
            }
            catch (OperationCanceledException)
            {
                Log.Information("Update download cancelled.");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to download update: {ErrorMessage}", ex.Message);
                if (_toastService != null)
                {
                    string errorMsg = MapExceptionToUserMessage(ex);
                    _toastService.ShowError($"Güncelleme indirilemedi: {errorMsg}");
                }
                return false;
            }
            finally
            {
                IsDownloading = false;
                _downloadLock.Release();
            }
        }

        public void ApplyUpdateAndRestart()
        {
            if (_velopackUpdateInfo is null)
            {
                Log.Warning("ApplyUpdateAndRestart called but no Velopack update info exists.");
                return;
            }

            try
            {
                Log.Information("Applying update and restarting application for version {TargetVersion}...", AvailableUpdate?.TargetVersion);
                bool allowPrerelease = string.Equals(UpdateChannel, "Beta", StringComparison.OrdinalIgnoreCase);
                var source = new GithubSource(RepoUrl, accessToken: null, prerelease: allowPrerelease);
                var mgr = new UpdateManager(source);

                if (!mgr.IsInstalled)
                {
                    Log.Warning("App is not running from a Velopack installed package (Unpackaged/Dev mode). Cannot apply Velopack update directly.");
                    _toastService?.ShowWarning("Geliştirme modundasınız. Paket yüklemesi yapılabilmesi için uygulamanın kurulu olması gerekir.");
                    return;
                }

                mgr.ApplyUpdatesAndRestart(_velopackUpdateInfo);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply update and restart: {Message}", ex.Message);
                _toastService?.ShowError($"Güncelleme uygulanamadı: {ex.Message}");
            }
        }

        public void PrepareUpdateOnClose()
        {
            _settings.InstallOnClose = true;
            SaveSettingsInternal(_settings);
            _toastService?.ShowSuccess("Güncelleme program kapatılırken otomatik yüklenecek.");
        }

        public static string MapExceptionToUserMessage(Exception ex)
        {
            string msg = ex.Message.ToLower();
            if (msg.Contains("not installed") || msg.Contains("notinstalledexception"))
                return "Geliştirme (Debug) modundasınız. Güncelleme kontrolü ve yükleme yalnızca kurulu uygulamada çalışır.";
            if (msg.Contains("network") || msg.Contains("socket") || msg.Contains("name resolution") || msg.Contains("host"))
                return "İnternet bağlantısı kurulamadı. Lütfen ağ bağlantınızı kontrol edin.";
            if (msg.Contains("404") || msg.Contains("not found"))
                return "GitHub üzerinde yayınlanmış bir sürüm paketi bulunamadı.";
            if (msg.Contains("403") || msg.Contains("rate limit"))
                return "GitHub erişim sınırı aşıldı. Lütfen daha sonra tekrar deneyin.";
            if (msg.Contains("hash") || msg.Contains("checksum") || msg.Contains("corrupt"))
                return "İndirilen paket doğrulanamadı veya dosya bozulmuş.";
            if (msg.Contains("space") || msg.Contains("disk full"))
                return "Disk alanı yetersiz. Güncelleme indirilemedi.";
            if (msg.Contains("access") || msg.Contains("permission") || msg.Contains("denied"))
                return "Güncelleme dosyaları için yazma yetkisi bulunmuyor.";

            return "Güncelleme servisine ulaşılamadı veya bir ağ hatası oluştu.";
        }

        private static string GetSettingsFilePath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KamatekCRM");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "update_settings.json");
        }

        private static UpdateSettings LoadSettingsInternal()
        {
            try
            {
                string path = GetSettingsFilePath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<UpdateSettings>(json) ?? new UpdateSettings();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not load update settings from file.");
            }
            return new UpdateSettings();
        }

        private static void SaveSettingsInternal(UpdateSettings settings)
        {
            try
            {
                string path = GetSettingsFilePath();
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not save update settings to file.");
            }
        }
    }
}
