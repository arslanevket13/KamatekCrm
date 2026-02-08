using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace KamatekCrm.Services
{
    public interface IPhotoStorageService
    {
        Task<(string FileName, string FilePath, string ThumbnailPath)> SavePhotoAsync(IFormFile file, string subFolder);
        void DeletePhoto(string filePath);
    }

    public class PhotoStorageService : IPhotoStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly string _uploadsFolder;

        public PhotoStorageService(IWebHostEnvironment env, IConfiguration config)
        {
            _env = env;
            _uploadsFolder = config["Storage:UploadsFolder"] ?? "uploads";
        }

        public async Task<(string FileName, string FilePath, string ThumbnailPath)> SavePhotoAsync(IFormFile file, string subFolder)
        {
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var relativePath = Path.Combine(_uploadsFolder, subFolder);
            var absolutePath = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, relativePath);

            if (!Directory.Exists(absolutePath))
            {
                Directory.CreateDirectory(absolutePath);
            }

            var fullPath = Path.Combine(absolutePath, fileName);
            
            // Save original
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Create thumbnail
            var thumbnailName = $"thumb_{fileName}";
            var thumbnailPath = Path.Combine(absolutePath, thumbnailName);

            using (var image = await Image.LoadAsync(fullPath))
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(200, 200),
                    Mode = ResizeMode.Max
                }));

                await image.SaveAsync(thumbnailPath);
            }

            // Return web-relative paths (using forward slashes)
            var webRelativePath = Path.Combine(relativePath, fileName).Replace("\\", "/");
            var webThumbnailPath = Path.Combine(relativePath, thumbnailName).Replace("\\", "/");

            return (fileName, webRelativePath, webThumbnailPath);
        }

        public void DeletePhoto(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            var absolutePath = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, filePath.TrimStart('/', '\\'));
            
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }

            // Try delete thumbnail
            var dir = Path.GetDirectoryName(absolutePath);
            var file = Path.GetFileName(absolutePath);
            if (dir != null)
            {
                var thumbPath = Path.Combine(dir, $"thumb_{file}");
                if (File.Exists(thumbPath))
                {
                    File.Delete(thumbPath);
                }
            }
        }
    }
}
