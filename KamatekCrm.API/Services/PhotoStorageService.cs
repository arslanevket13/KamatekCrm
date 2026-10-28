using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
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
            using var image = await Image.LoadAsync(inputStream);

            if (!Directory.Exists(absolutePath))
            {
                Directory.CreateDirectory(absolutePath);
            }

            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(absolutePath, fileName);

            await image.SaveAsync(fullPath);

            var thumbnailName = $"thumb_{fileName}";
            var thumbnailPath = Path.Combine(absolutePath, thumbnailName);

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(200, 200),
                Mode = ResizeMode.Max
            }));

            await image.SaveAsync(thumbnailPath);

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
