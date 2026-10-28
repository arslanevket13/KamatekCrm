using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Çevrimdışı otomatik taslak (Auto-Save Draft) saklama ve kurtarma servisi.
    /// %LocalAppData%\KamatekCrm\Drafts\ altında güvenli JSON formatında taslak tutar.
    /// Thread-safe ve atomik dosya yazma desteği içerir.
    /// </summary>
    public interface IDraftStorageService
    {
        Task SaveDraftAsync<T>(string draftKey, T data);
        Task<T?> LoadDraftAsync<T>(string draftKey);
        Task ClearDraftAsync(string draftKey);
        bool HasDraft(string draftKey);
    }

    public class DraftStorageService : IDraftStorageService
    {
        private readonly string _draftsDirectory;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

        public DraftStorageService(string? customDraftsDirectory = null)
        {
            _draftsDirectory = !string.IsNullOrWhiteSpace(customDraftsDirectory)
                ? customDraftsDirectory
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "KamatekCrm",
                    "Drafts");

            try
            {
                if (!Directory.Exists(_draftsDirectory))
                {
                    Directory.CreateDirectory(_draftsDirectory);
                }
                else
                {
                    var oldTemps = Directory.GetFiles(_draftsDirectory, "*.tmp");
                    foreach (var temp in oldTemps)
                    {
                        try { File.Delete(temp); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Drafts dizini hazırlanırken uyarı: {Directory}", _draftsDirectory);
            }
        }

        private SemaphoreSlim GetGate(string draftKey) =>
            _locks.GetOrAdd(draftKey, _ => new SemaphoreSlim(1, 1));

        private string GetFilePath(string draftKey)
        {
            var safeName = string.Join("_", draftKey.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_draftsDirectory, $"{safeName}.json");
        }

        public async Task SaveDraftAsync<T>(string draftKey, T data)
        {
            if (string.IsNullOrWhiteSpace(draftKey) || data == null) return;

            var gate = GetGate(draftKey);
            await gate.WaitAsync();

            string? tempPath = null;
            try
            {
                if (!Directory.Exists(_draftsDirectory))
                {
                    Directory.CreateDirectory(_draftsDirectory);
                }

                var targetPath = GetFilePath(draftKey);
                var safeName = Path.GetFileNameWithoutExtension(targetPath);
                tempPath = Path.Combine(_draftsDirectory, $"{safeName}_{Guid.NewGuid():N}.tmp");

                var json = JsonSerializer.Serialize(data, JsonOptions);
                await File.WriteAllTextAsync(tempPath, json);

                // .NET 9 atomik ve üzerine yazılabilir dosya taşıma
                File.Move(tempPath, targetPath, overwrite: true);
                tempPath = null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Taslak kaydedilemedi: {DraftKey}", draftKey);
            }
            finally
            {
                if (tempPath != null && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
                gate.Release();
            }
        }

        public async Task<T?> LoadDraftAsync<T>(string draftKey)
        {
            if (string.IsNullOrWhiteSpace(draftKey)) return default;

            var targetPath = GetFilePath(draftKey);
            if (!File.Exists(targetPath)) return default;

            var gate = GetGate(draftKey);
            await gate.WaitAsync();

            try
            {
                if (!File.Exists(targetPath)) return default;

                var json = await File.ReadAllTextAsync(targetPath);
                if (string.IsNullOrWhiteSpace(json)) return default;

                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Taslak yüklenemedi: {DraftKey}", draftKey);
                return default;
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task ClearDraftAsync(string draftKey)
        {
            if (string.IsNullOrWhiteSpace(draftKey)) return;

            var gate = GetGate(draftKey);
            await gate.WaitAsync();

            try
            {
                var targetPath = GetFilePath(draftKey);
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                var safeName = Path.GetFileNameWithoutExtension(targetPath);
                if (Directory.Exists(_draftsDirectory))
                {
                    var strayTemps = Directory.GetFiles(_draftsDirectory, $"{safeName}*.tmp");
                    foreach (var stray in strayTemps)
                    {
                        try { File.Delete(stray); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Taslak silinemedi: {DraftKey}", draftKey);
            }
            finally
            {
                gate.Release();
            }
        }

        public bool HasDraft(string draftKey)
        {
            if (string.IsNullOrWhiteSpace(draftKey)) return false;
            return File.Exists(GetFilePath(draftKey));
        }
    }
}
