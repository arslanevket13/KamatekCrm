using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private static readonly string PostgresToolsDirectory =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PostgresTools");
    private static readonly string PgDumpPath = Path.Combine(PostgresToolsDirectory, "pg_dump.exe");
    private static readonly string PgRestorePath = Path.Combine(PostgresToolsDirectory, "pg_restore.exe");

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
        EnsureToolExists(PgDumpPath, "pg_dump.exe");
        EnsureToolExists(PgRestorePath, "pg_restore.exe");

        var connection = ParseConnectionString();
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
                PgDumpPath,
                [
                    "--host", connection.Host!,
                    "--port", connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
            ValidateArchiveStructure(backupPath, connection.Password);
            _integrityService.CreateManifest(backupPath, connection.Database);

            var validation = ValidateBackup(backupPath);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.Message);

            Log.Information(
                "Database backup verified. Path: {BackupPath}, SHA256: {Sha256}",
                backupPath,
                validation.Manifest?.Sha256);
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
        var integrity = _integrityService.Validate(backupPath);
        if (!integrity.IsValid) return integrity;

        if (!string.Equals(Path.GetExtension(backupPath), ".backup", StringComparison.OrdinalIgnoreCase))
            return BackupValidationResult.Invalid("Desteklenmeyen yedek biçimi. PostgreSQL custom .backup arşivi gereklidir.");

        try
        {
            EnsureToolExists(PgRestorePath, "pg_restore.exe");
            var connection = ParseConnectionString();
            ValidateArchiveStructure(backupPath, connection.Password);
            return integrity;
        }
        catch (Exception ex)
        {
            return BackupValidationResult.Invalid($"PostgreSQL arşiv doğrulaması başarısız: {ex.Message}");
        }
    }

    public string RestoreDatabase(string backupPath)
    {
        var validation = ValidateBackup(backupPath);
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.Message);

        var connection = ParseConnectionString();
        var recoveryDirectory = Path.Combine(DefaultBackupDirectory, "Recovery");
        var recoveryBackup = BackupDatabase(recoveryDirectory, "pre_restore");

        try
        {
            RestoreArchive(backupPath, connection, "Geri yükleme");
        }
        catch (Exception restoreException)
        {
            Log.Error(
                restoreException,
                "Restore failed for {BackupPath}; automatic recovery is starting from {RecoveryBackup}",
                backupPath,
                recoveryBackup);

            try
            {
                RestoreArchive(recoveryBackup, connection, "Otomatik geri alma");
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
            backupPath,
            recoveryBackup);
        return recoveryBackup;
    }

    private static void RestoreArchive(
        string backupPath,
        NpgsqlConnectionStringBuilder connection,
        string operation)
    {
        var result = RunPostgresTool(
            PgRestorePath,
            [
                "--host", connection.Host!,
                "--port", connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
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

    private void ValidateArchiveStructure(string backupPath, string password)
    {
        var result = RunPostgresTool(PgRestorePath, ["--list", backupPath], password);
        EnsureSuccessful(result, "Yedek arşivi doğrulama");
        if (string.IsNullOrWhiteSpace(result.StandardOutput) ||
            !result.StandardOutput.Contains("Archive", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("pg_restore arşiv içeriğini okuyamadı.");
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
            WorkingDirectory = PostgresToolsDirectory
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        startInfo.Environment["PGPASSWORD"] = password ?? string.Empty;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"{Path.GetFileName(executable)} başlatılamadı.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)ProcessTimeout.TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
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

    private static void EnsureToolExists(string path, string toolName)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"{toolName} bulunamadı. PostgresTools paketini kontrol edin.",
                path);
    }

    private static string SanitizeLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return string.Empty;
        var safe = new string(label.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_').ToArray());
        return safe[..Math.Min(safe.Length, 24)];
    }

    private static void TryDeletePartialBackup(string backupPath)
    {
        try { if (File.Exists(backupPath)) File.Delete(backupPath); } catch { }
        try
        {
            var manifest = BackupIntegrityService.GetManifestPath(backupPath);
            if (File.Exists(manifest)) File.Delete(manifest);
        }
        catch { }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
