using System;
using System.Threading;
using System.Threading.Tasks;

namespace KamatekCrm.Services.Update
{
    public record UpdateCheckResult(
        string TargetVersion,
        string CurrentVersion,
        string ReleaseNotes,
        long DownloadSizeBytes,
        DateTimeOffset? ReleaseDate,
        bool IsPrerelease,
        object? VelopackUpdateInfo);

    public interface IUpdateService
    {
        string CurrentVersion { get; }
        string UpdateChannel { get; }
        bool IsChecking { get; }
        bool IsDownloading { get; }
        int DownloadProgress { get; }
        bool IsUpdateDownloaded { get; }
        UpdateCheckResult? AvailableUpdate { get; }

        event Action<int>? DownloadProgressChanged;
        event Action<UpdateCheckResult>? UpdateAvailableNotification;

        Task<UpdateCheckResult?> CheckForUpdatesAsync(bool isAutoCheck, CancellationToken ct = default);
        Task<bool> DownloadUpdateAsync(IProgress<int>? progress = null, CancellationToken ct = default);
        void ApplyUpdateAndRestart();
        void PrepareUpdateOnClose();
        UpdateSettings GetSettings();
        void SaveSettings(UpdateSettings settings);
    }
}
