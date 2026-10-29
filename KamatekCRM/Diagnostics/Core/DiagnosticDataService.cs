using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using KamatekCrm.Diagnostics.Authorization;

namespace KamatekCrm.Diagnostics.Core
{
    public sealed record DiagnosticStoreStatistics(
        bool IsEnabled,
        int Count,
        int Capacity,
        long DroppedCount,
        string LevelName);

    public sealed record DiagnosticEnvironmentSnapshot(
        string AppVersion,
        string MachineName,
        string OperatingSystem,
        string RuntimeVersion,
        int ProcessId,
        int ThreadCount,
        long WorkingSetBytes,
        double UptimeMinutes,
        string LogDirectory,
        string DiagnosticsDirectory,
        int ConfiguredDeveloperAccounts);

    public sealed record CrashArtifactInfo(
        string FileName,
        DateTimeOffset CreatedAtUtc,
        long SizeBytes);

    /// <summary>
    /// Dev Center sekmelerinin veri erişim kapısı.
    /// <para>
    /// ViewModel'ler ham <see cref="IDiagnosticStore"/> yerine bu servisi kullanır; böylece
    /// yetki denetimi yalnızca menüde değil, <b>her veri çağrısında</b> uygulanır.
    /// </para>
    /// </summary>
    public sealed class DiagnosticDataService
    {
        private readonly IDiagnosticStore _store;
        private readonly DiagnosticAccessGuard _guard;
        private readonly DiagnosticPackageService _packages;

        public DiagnosticDataService(
            IDiagnosticStore store,
            DiagnosticAccessGuard guard,
            DiagnosticPackageService? packages = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _guard = guard ?? throw new ArgumentNullException(nameof(guard));
            _packages = packages ?? new DiagnosticPackageService();
        }

        public void EnsureAccess() => _guard.EnsureAuthorized();

        public string ExportsDirectory => _packages.ExportsDirectory;

        /// <summary>Seçili logları/çökme izlerini redakte ederek zip paketi oluşturur.</summary>
        public DiagnosticPackageResult CreatePackage(DiagnosticPackageRequest? request = null)
        {
            _guard.EnsureAuthorized();
            return _packages.CreatePackage(request);
        }

        public IReadOnlyList<DiagnosticPackageInfo> GetPackages(int limit = 20)
        {
            _guard.EnsureAuthorized();
            return _packages.GetPackages(limit);
        }

        public bool DeletePackage(string? fileName)
        {
            _guard.EnsureAuthorized();
            return _packages.DeletePackage(fileName);
        }

        public DiagnosticStoreStatistics GetStatistics()
        {
            _guard.EnsureAuthorized();
            return new DiagnosticStoreStatistics(
                _guard.IsEnabled,
                _store.Count,
                _store.Capacity,
                _store.DroppedCount,
                DiagnosticRuntime.LevelSwitch?.CurrentLevelName ?? "bilinmiyor");
        }

        public IReadOnlyList<DiagnosticEvent> Snapshot(int limit = 500)
        {
            _guard.EnsureAuthorized();
            return _store.GetSnapshot(limit);
        }

        public IReadOnlyList<DiagnosticEvent> Query(DiagnosticQuery query)
        {
            _guard.EnsureAuthorized();
            return _store.Query(query);
        }

        public DiagnosticEnvironmentSnapshot GetEnvironmentSnapshot()
        {
            _guard.EnsureAuthorized();

            using var process = Process.GetCurrentProcess();
            var appVersion = typeof(DiagnosticDataService).Assembly.GetName().Version?.ToString() ?? "bilinmiyor";
            var uptime = DateTime.Now - process.StartTime;

            return new DiagnosticEnvironmentSnapshot(
                appVersion,
                Environment.MachineName,
                Environment.OSVersion.VersionString,
                Environment.Version.ToString(),
                Environment.ProcessId,
                process.Threads.Count,
                process.WorkingSet64,
                Math.Max(0, uptime.TotalMinutes),
                DiagnosticPaths.Logs,
                DiagnosticPaths.Diagnostics,
                DiagnosticRuntime.Options.DeveloperAccounts.Count);
        }

        /// <summary>Çökme izlerini (dosya adı/tarih/boyut) listeler. Dizin yoksa boş liste döner.</summary>
        public IReadOnlyList<CrashArtifactInfo> GetCrashArtifacts(int limit = 50)
        {
            _guard.EnsureAuthorized();

            try
            {
                var directory = new DirectoryInfo(DiagnosticPaths.Crashes);
                if (!directory.Exists)
                {
                    return Array.Empty<CrashArtifactInfo>();
                }

                return directory
                    .GetFiles("crash-*.json")
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Take(limit)
                    .Select(file => new CrashArtifactInfo(
                        file.Name,
                        new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
                        file.Length))
                    .ToList();
            }
            catch
            {
                return Array.Empty<CrashArtifactInfo>();
            }
        }
    }
}
