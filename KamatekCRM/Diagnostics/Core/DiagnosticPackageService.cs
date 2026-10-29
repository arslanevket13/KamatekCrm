using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace KamatekCrm.Diagnostics.Core
{
    /// <summary>Pakete hangi kaynakların dahil edileceği.</summary>
    public sealed record DiagnosticPackageRequest(bool IncludeLogs = true, bool IncludeCrashes = true);

    /// <summary>Dışa aktarılan paketin sonucu. <see cref="ErrorMessage"/> doluysa paket oluşmamıştır.</summary>
    public sealed record DiagnosticPackageResult(
        string? FilePath,
        long SizeBytes,
        int EntryCount,
        IReadOnlyList<string> Warnings,
        string? ErrorMessage)
    {
        public bool Succeeded => FilePath is not null && ErrorMessage is null;

        public string FileName => FilePath is null ? string.Empty : Path.GetFileName(FilePath);
    }

    /// <summary>Diskte duran mevcut tanılama paketi.</summary>
    public sealed record DiagnosticPackageInfo(string FileName, long SizeBytes, DateTimeOffset CreatedAtUtc)
    {
        public string SizeText => $"{SizeBytes / 1024d:N0} KB";
        public string CreatedAtText => CreatedAtUtc.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
    }

    /// <summary>
    /// Seçili log ve çökme izlerini <b>redakte ederek</b> tek bir zip paketine yazar.
    /// <para>
    /// Güvenlik kuralları:
    /// <list type="bullet">
    /// <item>Okunacak dosyalar yalnızca <c>Logs</c> ve <c>Crashes</c> kökleri altından seçilir; kullanıcı yol bildirmez.</item>
    /// <item>Çıktı adı servis tarafından üretilir; dışa aktarma dizini sabittir (path traversal yok).</item>
    /// <item>Her satır <see cref="LogRedactor"/> üzerinden geçirilir (parola/token/e-posta/telefon maskelenir).</item>
    /// <item>Dosya başına ve paket toplamı için boyut sınırı vardır; eski paketler saklama sayısına göre silinir.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class DiagnosticPackageService
    {
        public const long DefaultMaxPackageBytes = 20L * 1024 * 1024;
        public const long DefaultMaxBytesPerFile = 2L * 1024 * 1024;
        public const int DefaultMaxLogFiles = 10;
        public const int DefaultMaxCrashFiles = 20;
        public const int DefaultRetentionCount = 10;

        private const string PackagePrefix = "tanilama-";

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private readonly string _logsDirectory;
        private readonly string _crashesDirectory;
        private readonly string _exportsDirectory;
        private readonly long _maxPackageBytes;
        private readonly long _maxBytesPerFile;
        private readonly int _maxLogFiles;
        private readonly int _maxCrashFiles;
        private readonly int _retentionCount;

        public DiagnosticPackageService(
            string? logsDirectory = null,
            string? crashesDirectory = null,
            string? exportsDirectory = null,
            long? maxPackageBytes = null,
            long? maxBytesPerFile = null,
            int? maxLogFiles = null,
            int? maxCrashFiles = null,
            int? retentionCount = null)
        {
            _logsDirectory = logsDirectory ?? DiagnosticPaths.Logs;
            _crashesDirectory = crashesDirectory ?? DiagnosticPaths.Crashes;
            _exportsDirectory = exportsDirectory ?? DiagnosticPaths.Exports;
            _maxPackageBytes = Math.Max(1024, maxPackageBytes ?? DefaultMaxPackageBytes);
            _maxBytesPerFile = Math.Max(1024, maxBytesPerFile ?? DefaultMaxBytesPerFile);
            _maxLogFiles = Math.Max(1, maxLogFiles ?? DefaultMaxLogFiles);
            _maxCrashFiles = Math.Max(1, maxCrashFiles ?? DefaultMaxCrashFiles);
            _retentionCount = Math.Max(1, retentionCount ?? DefaultRetentionCount);
        }

        public string ExportsDirectory => _exportsDirectory;

        public long MaxPackageBytes => _maxPackageBytes;

        public long MaxBytesPerFile => _maxBytesPerFile;

        /// <summary>Paketi oluşturur. Çağıran akışı istisna ile bozmaz.</summary>
        public DiagnosticPackageResult CreatePackage(DiagnosticPackageRequest? request = null)
        {
            request ??= new DiagnosticPackageRequest();
            var warnings = new List<string>();

            string? tempPath = null;
            try
            {
                var directory = DiagnosticPaths.EnsureDirectory(_exportsDirectory);
                var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                var suffix = Guid.NewGuid().ToString("N")[..8];
                var finalPath = Path.Combine(directory, $"{PackagePrefix}{stamp}-{suffix}.zip");
                tempPath = finalPath + ".tmp";

                var entries = 0;
                long written = 0;

                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    if (request.IncludeLogs)
                    {
                        written += AddTextFiles(
                            archive, _logsDirectory, "logs", "*.log", _maxLogFiles,
                            _maxPackageBytes - written, warnings, ref entries);
                    }

                    if (request.IncludeCrashes)
                    {
                        written += AddTextFiles(
                            archive, _crashesDirectory, "crashes", "crash-*.json", _maxCrashFiles,
                            _maxPackageBytes - written, warnings, ref entries);
                    }

                    WriteManifest(archive, request, entries, written, warnings);
                    entries++;
                }

                File.Move(tempPath, finalPath, overwrite: false);
                tempPath = null;

                var packageSize = new FileInfo(finalPath).Length;
                PruneOldPackages(directory, finalPath, warnings);

                return new DiagnosticPackageResult(
                    finalPath,
                    packageSize,
                    entries,
                    warnings,
                    null);
            }
            catch (Exception ex)
            {
                // Yol/izin ayrıntısı sızdırmamak için yalnızca tip adı raporlanır.
                return new DiagnosticPackageResult(
                    null,
                    0,
                    0,
                    warnings,
                    $"Paket oluşturulamadı: {ex.GetType().Name}");
            }
            finally
            {
                TryDelete(tempPath);
            }
        }

        /// <summary>Dışa aktarma dizinindeki paketleri (yeniden eskiye) listeler.</summary>
        public IReadOnlyList<DiagnosticPackageInfo> GetPackages(int limit = 20)
        {
            try
            {
                var directory = new DirectoryInfo(_exportsDirectory);
                if (!directory.Exists)
                {
                    return Array.Empty<DiagnosticPackageInfo>();
                }

                return directory
                    .GetFiles($"{PackagePrefix}*.zip")
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Take(Math.Max(1, limit))
                    .Select(file => new DiagnosticPackageInfo(
                        file.Name,
                        file.Length,
                        new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero)))
                    .ToList();
            }
            catch
            {
                return Array.Empty<DiagnosticPackageInfo>();
            }
        }

        /// <summary>
        /// Paketi siler. Yalnızca dışa aktarma dizinindeki <c>*.zip</c> dosyaları silinebilir;
        /// dosya adı yol içeremez (path traversal engeli).
        /// </summary>
        public bool DeletePackage(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) ||
                !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal) ||
                !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                var path = Path.Combine(_exportsDirectory, fileName);
                var fullPath = Path.GetFullPath(path);
                var root = Path.GetFullPath(_exportsDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                File.Delete(fullPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private long AddTextFiles(
            ZipArchive archive,
            string sourceDirectory,
            string entryPrefix,
            string pattern,
            int maxFiles,
            long remainingBudget,
            List<string> warnings,
            ref int entries)
        {
            if (remainingBudget <= 0)
            {
                warnings.Add("Paket boyut sınırına ulaşıldı; bazı dosyalar eklenmedi.");
                return 0;
            }

            FileInfo[] files;
            try
            {
                var directory = new DirectoryInfo(sourceDirectory);
                if (!directory.Exists)
                {
                    warnings.Add($"{entryPrefix}: kaynak dizin bulunamadı.");
                    return 0;
                }

                files = directory.GetFiles(pattern)
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Take(maxFiles)
                    .ToArray();
            }
            catch
            {
                warnings.Add($"{entryPrefix}: dosyalar listelenemedi.");
                return 0;
            }

            long total = 0;
            foreach (var file in files)
            {
                if (remainingBudget - total <= 0)
                {
                    warnings.Add("Paket boyut sınırına ulaşıldı; kalan dosyalar eklenmedi.");
                    break;
                }

                var content = TryReadRedacted(file, out var truncated);
                if (content is null)
                {
                    warnings.Add($"{file.Name}: okunamadı, atlandı.");
                    continue;
                }

                if (truncated)
                {
                    warnings.Add($"{file.Name}: {_maxBytesPerFile / 1024} KB sınırında kesildi.");
                }

                var bytes = Encoding.UTF8.GetByteCount(content);
                if (bytes > remainingBudget - total)
                {
                    warnings.Add($"{file.Name}: paket boyut sınırı nedeniyle eklenmedi.");
                    break;
                }

                try
                {
                    var entry = archive.CreateEntry($"{entryPrefix}/{file.Name}", CompressionLevel.Optimal);
                    using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                    writer.Write(content);
                    entries++;
                    total += bytes;
                }
                catch
                {
                    warnings.Add($"{file.Name}: pakete yazılamadı.");
                }
            }

            return total;
        }

        /// <summary>
        /// Dosyayı en fazla <see cref="MaxBytesPerFile"/> okuyup her satırı redakte eder.
        /// Yazma sürerken okunabilmesi için <see cref="FileShare.ReadWrite"/> kullanılır.
        /// </summary>
        private string? TryReadRedacted(FileInfo file, out bool truncated)
        {
            truncated = false;
            try
            {
                using var stream = new FileStream(
                    file.FullName, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

                var builder = new StringBuilder();
                var bytes = 0;
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    var redacted = LogRedactor.Redact(line);
                    bytes += Encoding.UTF8.GetByteCount(redacted) + 2;

                    if (bytes > _maxBytesPerFile)
                    {
                        truncated = true;
                        builder.AppendLine($"... (dosya {_maxBytesPerFile / 1024} KB sınırında kesildi)");
                        break;
                    }

                    builder.AppendLine(redacted);
                }

                return builder.ToString();
            }
            catch
            {
                return null;
            }
        }

        private void WriteManifest(
            ZipArchive archive,
            DiagnosticPackageRequest request,
            int entryCount,
            long contentBytes,
            IReadOnlyList<string> warnings)
        {
            var payload = new
            {
                olusturulmaUtc = DateTimeOffset.UtcNow,
                uygulamaSurumu = typeof(DiagnosticPackageService).Assembly.GetName().Version?.ToString(),
                makine = Environment.MachineName,
                isletimSistemi = Environment.OSVersion.VersionString,
                dotnetSurumu = Environment.Version.ToString(),
                surecKimligi = Environment.ProcessId,
                logDahil = request.IncludeLogs,
                cokmeDahil = request.IncludeCrashes,
                dosyaSayisi = entryCount,
                icerikBayt = contentBytes,
                redaksiyon = "Her satır LogRedactor üzerinden geçirildi: parola, token, e-posta, telefon, TCKN, IBAN, kart numarası maskelenir.",
                uyarilar = warnings
            };

            var entry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(JsonSerializer.Serialize(payload, JsonOptions));
        }

        private void PruneOldPackages(string directory, string? currentPackagePath, List<string> warnings)
        {
            try
            {
                var files = new DirectoryInfo(directory)
                    .GetFiles($"{PackagePrefix}*.zip");

                var stale = files
                    .Where(file => currentPackagePath == null ||
                                   !string.Equals(file.FullName, currentPackagePath, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(file => file.CreationTimeUtc)
                    .ThenByDescending(file => file.LastWriteTimeUtc)
                    .ThenByDescending(file => file.Name)
                    .Skip(Math.Max(0, _retentionCount - (currentPackagePath != null ? 1 : 0)))
                    .ToList();

                foreach (var file in stale)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                        // Tek dosya silinemezse diğerleri etkilenmesin.
                    }
                }
            }
            catch
            {
                warnings.Add("Eski paketler temizlenemedi.");
            }
        }

        private static void TryDelete(string? path)
        {
            if (path is null)
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Geçici dosya silinemezse asıl sonuç etkilenmesin.
            }
        }
    }
}
