using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace KamatekCrm.Services;

public sealed record BackupManifest(
    int FormatVersion,
    string FileName,
    long FileLength,
    string Sha256,
    DateTime CreatedAtUtc,
    string DatabaseName,
    string ArchiveFormat);

public sealed record BackupValidationResult(
    bool IsValid,
    string Message,
    BackupManifest? Manifest = null)
{
    public static BackupValidationResult Valid(BackupManifest manifest) =>
        new(true, "Yedek bütünlüğü doğrulandı.", manifest);

    public static BackupValidationResult Invalid(string message) =>
        new(false, message);
}

public interface IBackupIntegrityService
{
    BackupManifest CreateManifest(string backupPath, string databaseName);
    BackupValidationResult Validate(string backupPath);
}

public sealed class BackupIntegrityService : IBackupIntegrityService
{
    public const int CurrentFormatVersion = 1;

    public BackupManifest CreateManifest(string backupPath, string databaseName)
    {
        var file = new FileInfo(backupPath);
        if (!file.Exists || file.Length == 0)
            throw new InvalidOperationException("Boş veya bulunamayan yedek için manifest oluşturulamaz.");

        var manifest = new BackupManifest(
            CurrentFormatVersion,
            file.Name,
            file.Length,
            ComputeSha256(backupPath),
            DateTime.UtcNow,
            databaseName,
            "PostgreSQL-Custom");

        File.WriteAllText(
            GetManifestPath(backupPath),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        return manifest;
    }

    public BackupValidationResult Validate(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
            return BackupValidationResult.Invalid("Yedek dosyası bulunamadı.");

        var file = new FileInfo(backupPath);
        if (file.Length == 0)
            return BackupValidationResult.Invalid("Yedek dosyası boş.");

        var manifestPath = GetManifestPath(backupPath);
        if (!File.Exists(manifestPath))
            return BackupValidationResult.Invalid("Yedek manifesti bulunamadı; dosyanın kaynağı ve bütünlüğü doğrulanamıyor.");

        try
        {
            var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath));
            if (manifest is null || manifest.FormatVersion != CurrentFormatVersion)
                return BackupValidationResult.Invalid("Yedek manifest sürümü desteklenmiyor.");
            if (!string.Equals(manifest.FileName, file.Name, StringComparison.Ordinal))
                return BackupValidationResult.Invalid("Manifest başka bir yedek dosyasına ait.");
            if (manifest.FileLength != file.Length)
                return BackupValidationResult.Invalid("Yedek dosyasının boyutu manifest ile eşleşmiyor.");

            var actualHash = Convert.FromHexString(ComputeSha256(backupPath));
            var expectedHash = Convert.FromHexString(manifest.Sha256);
            if (actualHash.Length != expectedHash.Length ||
                !CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
                return BackupValidationResult.Invalid("Yedek dosyasının SHA-256 özeti eşleşmiyor; dosya değiştirilmiş veya bozulmuş.");

            return BackupValidationResult.Valid(manifest);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or IOException)
        {
            return BackupValidationResult.Invalid($"Yedek manifesti okunamadı: {ex.Message}");
        }
    }

    public static string GetManifestPath(string backupPath) => backupPath + ".manifest.json";

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
