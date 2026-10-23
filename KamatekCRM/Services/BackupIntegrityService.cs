using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

    private static readonly string[] AllowedExtensions = { ".backup", ".sql", ".dump" };
    private static readonly byte[] PgDumpMagic = { 0x50, 0x47, 0x44, 0x4D, 0x50 }; // "PGDMP"

    public BackupManifest CreateManifest(string backupPath, string databaseName)
    {
        var validatedPath = ValidateAndNormalizePath(backupPath);
        var file = new FileInfo(validatedPath);
        if (!file.Exists || file.Length == 0)
            throw new InvalidOperationException("Boş veya bulunamayan yedek için manifest oluşturulamaz.");

        var isCustom = IsCustomArchive(validatedPath);
        var manifest = new BackupManifest(
            CurrentFormatVersion,
            file.Name,
            file.Length,
            ComputeSha256(validatedPath),
            DateTime.UtcNow,
            databaseName,
            isCustom ? "PostgreSQL-Custom" : "PostgreSQL-PlainSQL");

        File.WriteAllText(
            GetManifestPath(validatedPath),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        return manifest;
    }

    public BackupValidationResult Validate(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
            return BackupValidationResult.Invalid("Yedek dosyası yolu belirtilmedi.");

        string validatedPath;
        try
        {
            validatedPath = ValidateAndNormalizePath(backupPath);
        }
        catch (Exception ex)
        {
            return BackupValidationResult.Invalid($"Geçersiz dosya yolu: {ex.Message}");
        }

        var file = new FileInfo(validatedPath);
        if (!file.Exists)
            return BackupValidationResult.Invalid("Yedek dosyası bulunamadı.");

        if (file.Length == 0)
            return BackupValidationResult.Invalid("Yedek dosyası boş.");

        var ext = file.Extension;
        if (!AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return BackupValidationResult.Invalid("Desteklenmeyen yedek uzantısı. Yalnızca .backup, .sql ve .dump dosyaları desteklenir.");

        // Manifest kontrolü (Manifest bütünlük ve kaynak doğrulaması için zorunludur)
        var manifestPath = GetManifestPath(validatedPath);
        if (!File.Exists(manifestPath))
            return BackupValidationResult.Invalid("Yedek manifesti bulunamadı; dosyanın kaynağı ve bütünlüğü doğrulanamıyor.");

        BackupManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath))!;
            if (manifest is null || manifest.FormatVersion != CurrentFormatVersion)
                return BackupValidationResult.Invalid("Yedek manifest sürümü desteklenmiyor.");
            if (!string.Equals(manifest.FileName, file.Name, StringComparison.Ordinal))
                return BackupValidationResult.Invalid("Manifest başka bir yedek dosyasına ait.");
            if (manifest.FileLength != file.Length)
                return BackupValidationResult.Invalid("Yedek dosyasının boyutu manifest ile eşleşmiyor.");

            var actualHash = Convert.FromHexString(ComputeSha256(validatedPath));
            var expectedHash = Convert.FromHexString(manifest.Sha256);
            if (actualHash.Length != expectedHash.Length ||
                !CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
                return BackupValidationResult.Invalid("Yedek dosyasının SHA-256 özeti eşleşmiyor; dosya değiştirilmiş veya bozulmuş.");
        }
        catch (Exception ex) when (ex is JsonException or FormatException or IOException)
        {
            return BackupValidationResult.Invalid($"Yedek manifesti okunamadı: {ex.Message}");
        }

        // Derin format doğrulaması (Custom format için pg_restore --list, plain SQL için işaretleyiciler)
        bool isCustom = IsCustomArchive(validatedPath);
        if (isCustom)
        {
            var customCheck = ValidateCustomArchive(validatedPath);
            if (!customCheck.IsValid)
                return customCheck;
        }
        else if (string.Equals(ext, ".sql", StringComparison.OrdinalIgnoreCase))
        {
            var sqlCheck = ValidateSqlArchive(validatedPath);
            if (!sqlCheck.IsValid)
                return sqlCheck;
        }

        return BackupValidationResult.Valid(manifest);
    }

    private static BackupValidationResult ValidateCustomArchive(string path)
    {
        // pg_restore --list aracı ile arşiv tablosunun sağlamlığını kontrol et
        var pgRestorePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PostgresTools", "pg_restore.exe");
        if (File.Exists(pgRestorePath))
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pgRestorePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.ArgumentList.Add("--list");
                psi.ArgumentList.Add(path);

                using var process = Process.Start(psi);
                if (process != null)
                {
                    var stdoutTask = process.StandardOutput.ReadToEndAsync();
                    var stderrTask = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(15000))
                    {
                        try { process.Kill(entireProcessTree: true); } catch { }
                        return BackupValidationResult.Invalid("pg_restore --list doğrulama işlemi zaman aşımına uğradı.");
                    }

                    stdoutTask.Wait();
                    stderrTask.Wait();

                    if (process.ExitCode != 0)
                    {
                        var err = stderrTask.Result.Trim();
                        return BackupValidationResult.Invalid($"pg_restore arşiv doğrulama hatası (kod {process.ExitCode}): {err}");
                    }
                }
            }
            catch (Exception ex)
            {
                return BackupValidationResult.Invalid($"pg_restore çalıştırılırken hata: {ex.Message}");
            }
        }

        return BackupValidationResult.Valid(null!);
    }

    private static BackupValidationResult ValidateSqlArchive(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fs.Length < 10)
                return BackupValidationResult.Invalid("SQL yedek dosyası içeriği çok kısa veya geçersiz.");

            // İlk 4KB oku
            var headBuffer = new byte[Math.Min(4096, fs.Length)];
            fs.ReadExactly(headBuffer, 0, headBuffer.Length);
            var headText = Encoding.UTF8.GetString(headBuffer);

            bool hasSqlMarkers = headText.Contains("PostgreSQL database dump", StringComparison.OrdinalIgnoreCase)
                                 || headText.Contains("CREATE ", StringComparison.OrdinalIgnoreCase)
                                 || headText.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase)
                                 || headText.Contains("SET ", StringComparison.OrdinalIgnoreCase)
                                 || headText.Contains("--", StringComparison.OrdinalIgnoreCase);

            if (!hasSqlMarkers)
            {
                return BackupValidationResult.Invalid("SQL yedek dosyası geçerli PostgreSQL dump işaretleri içermiyor.");
            }

            // Son 4KB oku (varsa)
            if (fs.Length > 4096)
            {
                long tailOffset = Math.Max(0, fs.Length - 4096);
                fs.Seek(tailOffset, SeekOrigin.Begin);
                var tailBuffer = new byte[(int)(fs.Length - tailOffset)];
                fs.ReadExactly(tailBuffer, 0, tailBuffer.Length);
                var tailText = Encoding.UTF8.GetString(tailBuffer);

                // Normal bitiş işaretleri veya SQL satırları
                bool hasTailMarkers = tailText.Contains("PostgreSQL database dump complete", StringComparison.OrdinalIgnoreCase)
                                      || tailText.Contains(";\n")
                                      || tailText.Contains(";\r\n")
                                      || tailText.TrimEnd().EndsWith(";")
                                      || tailText.Contains("--");

                if (!hasTailMarkers)
                {
                    return BackupValidationResult.Invalid("SQL yedek dosyası eksik veya kesintiye uğramış görünüyor (dosya sonu işareti eksik).");
                }
            }

            return BackupValidationResult.Valid(null!);
        }
        catch (Exception ex)
        {
            return BackupValidationResult.Invalid($"SQL yedeği doğrulanırken hata oluştu: {ex.Message}");
        }
    }

    private static bool IsCustomArchive(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var header = new byte[5];
            var read = fs.Read(header, 0, header.Length);
            return read == 5 && header.SequenceEqual(PgDumpMagic);
        }
        catch
        {
            return false;
        }
    }

    private static string ValidateAndNormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Yol boş olamaz.", nameof(path));

        var fullPath = Path.GetFullPath(path);

        // Path traversal koruması: yasaklı karakterler veya tehlikeli alt dizin tırmanışları
        if (fullPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new ArgumentException("Yol geçersiz karakterler içeriyor.", nameof(path));

        return fullPath;
    }

    public static string GetManifestPath(string backupPath) => backupPath + ".manifest.json";

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

