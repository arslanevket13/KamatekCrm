using System;
using System.Threading;
using System.Threading.Tasks;

namespace KamatekCrm.Services
{
    public sealed record ManagedPostgresResult(
        bool Success,
        string ConnectionString,
        int Port,
        string DataDirectory,
        string? ErrorMessage = null,
        bool WasAlreadyRunning = false,
        bool UsedFallbackDataDirectory = false);

    /// <summary>
    /// Son kullanıcının PostgreSQL ile muhatap olmadan yerel veritabanını otomatik kurmasını,
    /// başlatmasını, elektrik kesintisi sonrası toparlamasını ve kapatmasını yönetir.
    /// </summary>
    public interface IManagedPostgresService
    {
        string ResolveDataDirectory(out bool usedFallback);

        string? ResolveBinDirectory();

        bool IsRuntimeAvailable();

        bool IsRunningAsAdministrator();

        Task<ManagedPostgresResult> EnsureProvisionedAndRunningAsync(
            bool allowLanAccess,
            IProgress<string>? progress = null,
            CancellationToken ct = default);

        Task StopIfSinglePcModeAsync(CancellationToken ct = default);
    }
}
