using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KamatekCrm.Settings;
using Npgsql;
using KamatekCrm.Shared.Services;
using Serilog;

namespace KamatekCrm.Services;

/// <summary>
/// PostgreSQL custom-format arşiv üretir, manifest/checksum ve pg_restore liste
/// provasıyla doğrular; geri yüklemeden hemen önce otomatik kurtarma yedeği alır.
/// </summary>
public sealed class BackupService : IBackupService
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan VersionProbeTimeout = TimeSpan.FromSeconds(5);
    private static readonly Regex ToolVersionRegex = new(@"(\d+)(?:\.\d+)*\s*$", RegexOptions.Compiled);
    private static readonly string PostgresToolsDirectory =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PostgresTools");
    private static readonly string ManagedPostgresBinDirectory =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PostgresRuntime", "bin");

    private readonly IDatabaseConnectionProvider _connectionProvider;
    private readonly IBackupIntegrityService _integrityService;

    public BackupService(
        IDatabaseConnectionProvider connectionProvider,
        IBackupIntegrityService integrityService)
    {
        _connectionProvider = connectionProvider;
        _integrityService = integrityService;
    }

    public string DefaultBackupDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "KamatekCRM",
        "Backups");

    public string BackupFilePattern => "*.backup";

    public string BackupDatabase(string? destinationDirectory = null, string? label = null)
    {
        var connection = ParseConnectionString();
        var serverMajor = QueryServerMajorVersion(connection);
        var pgDumpPath = ResolvePostgresTool("pg_dump.exe", serverMajor);
        var pgRestorePath = ResolvePostgresTool("pg_restore.exe", serverMajor);

        var targetDirectory = string.IsNullOrWhiteSpace(destinationDirectory)
            ? DefaultBackupDirectory
            : Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(targetDirectory);

        var safeLabel = SanitizeLabel(label);
        var suffix = string.IsNullOrEmpty(safeLabel) ? string.Empty : $"_{safeLabel}";
        var baseFileName = $"KamatekCRM_{DateTime.UtcNow:yyyyMMdd_HHmmss}{suffix}_{Guid.NewGuid():N}";
        if (baseFileName.Length > 80) baseFileName = baseFileName[..80];
        var fileName = baseFileName + ".backup";
        var backupPath = Path.Combine(targetDirectory, fileName);

        try
        {
            var result = RunPostgresTool(
                pgDumpPath,
                [
                    "--host", connection.Host!,
                    "--port", connection.Port.ToString(CultureInfo.InvariantCulture),
                    "--username", connection.Username!,
                    "--dbname", connection.Database!,
                    "--format", "custom",
                    "--compress", "6",
                    "--no-owner",
                    "--no-privileges",
                    "--file", backupPath
                ],
                connection.Password);

            EnsureSuccessful(result, "Yedekleme");
            ValidateArchiveStructure(pgRestorePath, backupPath, connection.Password ?? string.Empty);
            _integrityService.CreateManifest(backupPath, connection.Database ?? string.Empty);

            var validation = ValidateBackup(backupPath);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.Message);

            Log.Information(
                "Database backup verified. Path: {BackupPath}, SHA256: {Sha256}, PgDump: {PgDumpPath}, ServerMajor: {ServerMajor}",
                backupPath,
                validation.Manifest?.Sha256,
                pgDumpPath,
                serverMajor);
            return backupPath;
        }
        catch
        {
            TryDeletePartialBackup(backupPath);
            throw;
        }
    }

    public BackupValidationResult ValidateBackup(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
            return BackupValidationResult.Invalid("Yedek dosyası yolu belirtilmedi.");

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(backupPath);
        }
        catch (Exception ex)
        {
            return BackupValidationResult.Invalid($"Geçersiz dosya yolu: {ex.Message}");
        }

        var ext = Path.GetExtension(fullPath);
        if (!string.Equals(ext, ".backup", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ext, ".sql", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ext, ".dump", StringComparison.OrdinalIgnoreCase))
        {
            return BackupValidationResult.Invalid("Desteklenmeyen yedek biçimi. Yalnızca .backup, .sql ve .dump dosyaları desteklenir.");
        }

        var integrity = _integrityService.Validate(fullPath);
        if (!integrity.IsValid) return integrity;

        try
        {
            var connection = ParseConnectionString();
            var serverMajor = QueryServerMajorVersion(connection);
            var pgRestorePath = ResolvePostgresTool("pg_restore.exe", serverMajor);
            if (string.Equals(ext, ".backup", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".dump", StringComparison.OrdinalIgnoreCase))
            {
                ValidateArchiveStructure(pgRestorePath, fullPath, connection.Password ?? string.Empty);
            }
            return integrity;
        }
        catch (Exception ex)
        {
            return BackupValidationResult.Invalid($"PostgreSQL arşiv doğrulaması başarısız: {ex.Message}");
        }
    }

    public string RestoreDatabase(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
            throw new ArgumentException("Yedek yolu boş olamaz.", nameof(backupPath));

        var fullPath = Path.GetFullPath(backupPath);
        var validation = ValidateBackup(fullPath);
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.Message);

        var connection = ParseConnectionString();
        var serverMajor = QueryServerMajorVersion(connection);
        var pgRestorePath = ResolvePostgresTool("pg_restore.exe", serverMajor);
        var recoveryDirectory = Path.Combine(DefaultBackupDirectory, "Recovery");
        var recoveryBackup = BackupDatabase(recoveryDirectory, "pre_restore");

        try
        {
            RestoreArchive(pgRestorePath, fullPath, connection, "Geri yükleme");
        }
        catch (Exception restoreException)
        {
            Log.Error(
                restoreException,
                "Restore failed for {BackupPath}; automatic recovery is starting from {RecoveryBackup}",
                fullPath,
                recoveryBackup);

            try
            {
                RestoreArchive(pgRestorePath, recoveryBackup, connection, "Otomatik geri alma");
            }
            catch (Exception recoveryException)
            {
                Log.Fatal(
                    recoveryException,
                    "Automatic recovery failed after restore error. Recovery backup: {RecoveryBackup}",
                    recoveryBackup);
                throw new AggregateException(
                    $"Geri yükleme ve otomatik geri alma başarısız oldu. Kurtarma yedeğini koruyun: {recoveryBackup}",
                    restoreException,
                    recoveryException);
            }

            throw new InvalidOperationException(
                $"Seçilen yedek geri yüklenemedi; veritabanı işlem öncesi kurtarma yedeğine döndürüldü: {recoveryBackup}",
                restoreException);
        }

        Log.Warning(
            "Database restored from {BackupPath}. Automatic recovery point: {RecoveryBackup}",
            fullPath,
            recoveryBackup);
        return recoveryBackup;
    }

    private static void RestoreArchive(
        string pgRestorePath,
        string backupPath,
        NpgsqlConnectionStringBuilder connection,
        string operation)
    {
        var result = RunPostgresTool(
            pgRestorePath,
            [
                "--host", connection.Host!,
                "--port", connection.Port.ToString(CultureInfo.InvariantCulture),
                "--username", connection.Username!,
                "--dbname", connection.Database!,
                "--clean",
                "--if-exists",
                "--exit-on-error",
                "--no-owner",
                "--no-privileges",
                backupPath
            ],
            connection.Password);
        EnsureSuccessful(result, operation);
    }

    private static void ValidateArchiveStructure(string pgRestorePath, string backupPath, string password)
    {
        var result = RunPostgresTool(pgRestorePath, ["--list", backupPath], password);
        EnsureSuccessful(result, "Yedek arşivi doğrulama");
        if (string.IsNullOrWhiteSpace(result.StandardOutput) ||
            !result.StandardOutput.Contains("Archive", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("pg_restore arşiv içeriğini okuyamadı.");
    }

    public static string ResolvePsqlPath(int serverMajor = 0) => ResolvePostgresTool("psql.exe", serverMajor);

    private static int QueryServerMajorVersion(NpgsqlConnectionStringBuilder connection)
    {
        try
        {
            var probeBuilder = new NpgsqlConnectionStringBuilder(connection.ConnectionString)
            {
                Timeout = 10,
                CommandTimeout = 10,
                Pooling = false
            };

            using var conn = new NpgsqlConnection(probeBuilder.ConnectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT current_setting('server_version_num')::int / 10000;", conn);
            var scalar = cmd.ExecuteScalar();
            return scalar is not null && int.TryParse(Convert.ToString(scalar, CultureInfo.InvariantCulture), out var major)
                ? major
                : 0;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PostgreSQL sunucu sürümü sorgulanamadı; varsayılan araç çözümlemesi kullanılacak.");
            return 0;
        }
    }

    private static string ResolvePostgresTool(string toolName, int serverMajor)
    {
        var candidates = new List<(string Path, int Priority)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string path, int priority)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                var full = Path.GetFullPath(path);
                if (File.Exists(full) && seen.Add(full))
                    candidates.Add((full, priority));
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Geçersiz PostgreSQL araç yolu atlandı: {Path}", path);
            }
        }

        // 1. Uygulama ile gelen yönetilen PostgresRuntime\bin ve PostgresTools
        var managedPath = Path.Combine(ManagedPostgresBinDirectory, toolName);
        AddCandidate(managedPath, 0);
        AddCandidate(Path.Combine(PostgresToolsDirectory, toolName), 1);

        // 2. Sistemde kurulu PostgreSQL sürümleri (Program Files)
        foreach (var programFiles in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     @"C:\Program Files"
                 }.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var pgRoot = Path.Combine(programFiles, "PostgreSQL");
            if (!Directory.Exists(pgRoot)) continue;
            try
            {
                foreach (var versionDir in Directory.EnumerateDirectories(pgRoot))
                {
                    AddCandidate(Path.Combine(versionDir, "bin", toolName), 2);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "PostgreSQL dizini taranamadı: {PgRoot}", pgRoot);
            }
        }

        // 3. PATH ortam değişkeni
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AddCandidate(Path.Combine(dir, toolName), 3);
            }
        }

        if (candidates.Count == 0)
        {
            throw new FileNotFoundException(
                serverMajor > 0
                    ? $"PostgreSQL {serverMajor} ile uyumlu {toolName} bulunamadı. Lütfen PostgreSQL {serverMajor} istemci araçlarını kurun."
                    : $"{toolName} bulunamadı. PostgresRuntime veya PostgreSQL istemci araçlarını kontrol edin.");
        }

        // Yönetilen yerel PostgreSQL aktifse ve bundled PostgresRuntime\bin sürümü sunucuyla uyumluysa önceliklendir
        if (AppSettings.IsManagedLocalPostgres && File.Exists(managedPath))
        {
            var managedMajor = ProbeToolMajorVersion(managedPath);
            if (serverMajor <= 0 || (managedMajor > 0 && managedMajor >= serverMajor))
            {
                return Path.GetFullPath(managedPath);
            }
        }

        var inspected = candidates
            .Select(c => (c.Path, c.Priority, Major: ProbeToolMajorVersion(c.Path)))
            .ToList();

        if (serverMajor > 0)
        {
            var compatible = inspected
                .Where(x => x.Major >= serverMajor)
                .OrderBy(x => x.Major)
                .ThenBy(x => x.Priority)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(compatible.Path))
                return compatible.Path;

            var maxFound = inspected.Select(x => x.Major).DefaultIfEmpty(0).Max();
            var foundText = maxFound > 0 ? $"bulunan en yüksek sürüm: {maxFound}" : "uyumlu sürüm algılanamadı";
            throw new InvalidOperationException(
                $"Aktif veritabanı sunucusu PostgreSQL {serverMajor} sürümünde çalışıyor, ancak bilgisayarda uyumlu {toolName} (>= {serverMajor}) bulunamadı ({foundText}). " +
                $"Lütfen PostgreSQL {serverMajor} (veya üzeri) istemci araçlarını kurun.");
        }

        var bestFallback = inspected
            .OrderByDescending(x => x.Major)
            .ThenBy(x => x.Priority)
            .First();
        return bestFallback.Path;
    }

    private static int ProbeToolMajorVersion(string executablePath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? PostgresToolsDirectory
            };
            startInfo.ArgumentList.Add("--version");

            using var process = Process.Start(startInfo);
            if (process is null) return 0;

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit((int)VersionProbeTimeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception ex) { Log.Debug(ex, "Sürüm kontrol süreci sonlandırılamadı: {Path}", executablePath); }
                return 0;
            }

            Task.WaitAll(stdout, stderr);
            var output = (stdout.Result ?? string.Empty).Trim();
            var match = ToolVersionRegex.Match(output);
            return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var major)
                ? major
                : 0;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PostgreSQL araç sürümü okunamadı: {Path}", executablePath);
            return 0;
        }
    }

    private NpgsqlConnectionStringBuilder ParseConnectionString()
    {
        var connectionString = _connectionProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("PostgreSQL bağlantı dizesi bulunamadı.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Host) ||
            string.IsNullOrWhiteSpace(builder.Database) ||
            string.IsNullOrWhiteSpace(builder.Username))
            throw new InvalidOperationException("PostgreSQL bağlantı dizesinde host, veritabanı veya kullanıcı eksik.");

        return builder;
    }

    private static ProcessResult RunPostgresTool(
        string executable,
        IReadOnlyCollection<string> arguments,
        string? password)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? PostgresToolsDirectory
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        startInfo.Environment["PGPASSWORD"] = password ?? string.Empty;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"{Path.GetFileName(executable)} başlatılamadı.");
        KamatekCrm.Helpers.ProcessManager.TrackProcess(process);
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)ProcessTimeout.TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); } catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "BackupService.cs"); }
            throw new TimeoutException($"{Path.GetFileName(executable)} zaman aşımına uğradı.");
        }

        Task.WaitAll(standardOutput, standardError);
        return new ProcessResult(process.ExitCode, standardOutput.Result, standardError.Result);
    }

    private static void EnsureSuccessful(ProcessResult result, string operation)
    {
        if (result.ExitCode == 0) return;
        var error = string.IsNullOrWhiteSpace(result.StandardError)
            ? "PostgreSQL aracı ayrıntılı hata döndürmedi."
            : result.StandardError.Trim();
        throw new InvalidOperationException($"{operation} başarısız (kod {result.ExitCode}): {error}");
    }

    private static string SanitizeLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return string.Empty;
        var safe = new string(label.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_').ToArray());
        return safe[..Math.Min(safe.Length, 24)];
    }

    private static void TryDeletePartialBackup(string backupPath)
    {
        try { if (File.Exists(backupPath)) File.Delete(backupPath); } catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "BackupService.cs"); }
        try
        {
            var manifest = BackupIntegrityService.GetManifestPath(backupPath);
            if (File.Exists(manifest)) File.Delete(manifest);
        }
        catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "BackupService.cs"); }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
