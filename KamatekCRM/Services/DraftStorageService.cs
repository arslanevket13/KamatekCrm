using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Çevrimdışı otomatik taslak (Auto-Save Draft) saklama ve kurtarma servisi.
    /// %LocalAppData%\KamatekCrm\Drafts\ altında güvenli JSON formatında taslak tutar.
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
        private static readonly string DraftsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KamatekCrm",
            "Drafts");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public DraftStorageService()
        {
            try
            {
                if (!Directory.Exists(DraftsDirectory))
                {
                    Directory.CreateDirectory(DraftsDirectory);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Drafts dizini oluşturulamadı: {Directory}", DraftsDirectory);
            }
        }

        private static string GetFilePath(string draftKey)
        {
            var safeName = string.Join("_", draftKey.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(DraftsDirectory, $"{safeName}.json");
        }

        public async Task SaveDraftAsync<T>(string draftKey, T data)
        {
            if (string.IsNullOrWhiteSpace(draftKey) || data == null) return;

            try
            {
                if (!Directory.Exists(DraftsDirectory))
                {
                    Directory.CreateDirectory(DraftsDirectory);
                }

                var targetPath = GetFilePath(draftKey);
                var tempPath = targetPath + ".tmp";

                var json = JsonSerializer.Serialize(data, JsonOptions);
                await File.WriteAllTextAsync(tempPath, json);

                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
                File.Move(tempPath, targetPath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Taslak kaydedilemedi: {DraftKey}", draftKey);
            }
        }

        public async Task<T?> LoadDraftAsync<T>(string draftKey)
        {
            if (string.IsNullOrWhiteSpace(draftKey)) return default;

            var targetPath = GetFilePath(draftKey);
            if (!File.Exists(targetPath)) return default;

            try
            {
                var json = await File.ReadAllTextAsync(targetPath);
                if (string.IsNullOrWhiteSpace(json)) return default;

                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Taslak yüklenemedi: {DraftKey}", draftKey);
                return default;
            }
        }

        public Task ClearDraftAsync(string draftKey)
        {
            if (string.IsNullOrWhiteSpace(draftKey)) return Task.CompletedTask;

            try
            {
                var targetPath = GetFilePath(draftKey);
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                var tempPath = targetPath + ".tmp";
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Taslak silinemedi: {DraftKey}", draftKey);
            }

            return Task.CompletedTask;
        }

        public bool HasDraft(string draftKey)
        {
            if (string.IsNullOrWhiteSpace(draftKey)) return false;
            return File.Exists(GetFilePath(draftKey));
        }
    }
}
