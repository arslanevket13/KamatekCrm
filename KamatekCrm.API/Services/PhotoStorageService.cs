using SkiaSharp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KamatekCrm.API.Services
{
    public interface IPhotoStorageService
    {
        Task<(string FileName, string FilePath, string ThumbnailPath)> SavePhotoAsync(IFormFile file, string subFolder);
        void DeletePhoto(string filePath);
    }

    public class PhotoStorageService : IPhotoStorageService
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private readonly IWebHostEnvironment _env;
        private readonly string _uploadsFolder;

        public PhotoStorageService(IWebHostEnvironment env, IConfiguration config)
        {
            _env = env;
            _uploadsFolder = config["Storage:UploadsFolder"] ?? "uploads";
        }

        private string GetBaseUploadsDirectory()
        {
            var root = _env.WebRootPath ?? _env.ContentRootPath;
            return Path.GetFullPath(Path.Combine(root, _uploadsFolder));
        }

        public async Task<(string FileName, string FilePath, string ThumbnailPath)> SavePhotoAsync(IFormFile file, string subFolder)
        {
            var ext = (Path.GetExtension(file.FileName) ?? string.Empty).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                throw new InvalidOperationException("Desteklenmeyen dosya uzantısı. Yalnızca JPG, PNG ve WEBP kabul edilir.");
            }

            var baseUploadsDir = GetBaseUploadsDirectory();
            var safeSubFolder = (subFolder ?? string.Empty).TrimStart('/', '\\');
            var absolutePath = Path.GetFullPath(Path.Combine(baseUploadsDir, safeSubFolder));

            if (!absolutePath.StartsWith(baseUploadsDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(absolutePath, baseUploadsDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Geçersiz alt klasör yolu.");
            }

            // Önce bellek akışı üzerinde gerçek görsel imzasını ve içeriğini doğrula (diske yazmadan önce!)
            await using var inputStream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await inputStream.CopyToAsync(ms);
            ms.Position = 0;

            using var originalBitmap = SKBitmap.Decode(ms)
                ?? throw new InvalidOperationException("Geçersiz veya bozuk görsel dosyası.");

            if (!Directory.Exists(absolutePath))
            {
                Directory.CreateDirectory(absolutePath);
            }

            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(absolutePath, fileName);

            var format = ext switch
            {
                ".png" => SKEncodedImageFormat.Png,
                ".webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Jpeg
            };

            using (var fullImage = SKImage.FromBitmap(originalBitmap))
            using (var fullData = fullImage.Encode(format, 90))
            using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fullData.SaveTo(fs);
            }

            var thumbnailName = $"thumb_{fileName}";
            var thumbnailPath = Path.Combine(absolutePath, thumbnailName);

            double ratio = Math.Min(200.0 / originalBitmap.Width, 200.0 / originalBitmap.Height);
            if (ratio > 1.0) ratio = 1.0;
            int thumbWidth = Math.Max(1, (int)Math.Round(originalBitmap.Width * ratio));
            int thumbHeight = Math.Max(1, (int)Math.Round(originalBitmap.Height * ratio));

            SKBitmap? resizedThumb = originalBitmap.Resize(new SKImageInfo(thumbWidth, thumbHeight), SKFilterQuality.High);
            try
            {
                using var thumbImage = SKImage.FromBitmap(resizedThumb ?? originalBitmap);
                using var thumbData = thumbImage.Encode(format, 85);
                using var tfs = new FileStream(thumbnailPath, FileMode.Create, FileAccess.Write, FileShare.None);
                thumbData.SaveTo(tfs);
            }
            finally
            {
                resizedThumb?.Dispose();
            }

            var relativePath = Path.Combine(_uploadsFolder, safeSubFolder);
            var webRelativePath = Path.Combine(relativePath, fileName).Replace("\\", "/");
            var webThumbnailPath = Path.Combine(relativePath, thumbnailName).Replace("\\", "/");

            return (fileName, webRelativePath, webThumbnailPath);
        }

        public void DeletePhoto(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            var root = Path.GetFullPath(_env.WebRootPath ?? _env.ContentRootPath);
            var baseUploadsDir = GetBaseUploadsDirectory();
            var absolutePath = Path.GetFullPath(Path.Combine(root, filePath.TrimStart('/', '\\')));

            if (!absolutePath.StartsWith(baseUploadsDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Yükleme dizini dışındaki dosyalar silinemez.");
            }

            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }

            var dir = Path.GetDirectoryName(absolutePath);
            var file = Path.GetFileName(absolutePath);
            if (dir != null)
            {
                var thumbPath = Path.GetFullPath(Path.Combine(dir, $"thumb_{file}"));
                if (thumbPath.StartsWith(baseUploadsDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(thumbPath))
                {
                    File.Delete(thumbPath);
                }
            }
        }
    }
}
