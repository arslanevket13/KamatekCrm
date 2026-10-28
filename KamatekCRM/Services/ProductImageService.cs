using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace KamatekCrm.Services
{
    /// <summary>
    /// WPF-side product image service: compress, save to local directory, delete.
    /// Never stores raw bytes in DB — only file paths.
    /// </summary>
    public interface IProductImageService
    {
        /// <summary>
        /// Compress and save a product image. Returns relative path.
        /// </summary>
        Task<string> SaveProductImageAsync(string sourceFilePath);

        /// <summary>
        /// Resolve absolute path from relative path.
        /// </summary>
        string GetAbsolutePath(string relativePath);

        /// <summary>
        /// Delete a product image from disk.
        /// </summary>
        void DeleteProductImage(string? relativePath);
    }

    public class ProductImageService : IProductImageService
    {
        private readonly string _baseDirectory;
        private const string SubFolder = "uploads/products";
        private const int MaxFileSizeBytes = 200 * 1024; // 200KB
        private const int MaxDimension = 800;

        public ProductImageService()
        {
            // Base directory = application root
            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        public async Task<string> SaveProductImageAsync(string sourceFilePath)
        {
            if (string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
                throw new FileNotFoundException("Kaynak dosya bulunamadı.", sourceFilePath);

            // Güvenlik doğrulaması: Magic Bytes ve maksimum 10MB boyut kontrolü
            FileSecurityValidator.ValidateImage(sourceFilePath);

            var absoluteDir = Path.Combine(_baseDirectory, SubFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(absoluteDir))
                Directory.CreateDirectory(absoluteDir);

            var fileName = $"{Guid.NewGuid()}.webp";
            var absolutePath = Path.Combine(absoluteDir, fileName);

            using (var image = await Image.LoadAsync(sourceFilePath))
            {
                // Resize if larger than max dimension (preserve aspect ratio)
                if (image.Width > MaxDimension || image.Height > MaxDimension)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(MaxDimension, MaxDimension),
                        Mode = ResizeMode.Max
                    }));
                }

                // Save as WebP with quality adjustment to stay under 200KB
                int quality = 80;
                while (quality >= 20)
                {
                    var encoder = new WebpEncoder { Quality = quality };
                    using (var ms = new MemoryStream())
                    {
                        await image.SaveAsync(ms, encoder);
                        if (ms.Length <= MaxFileSizeBytes || quality <= 20)
                        {
                            // Write final file
                            ms.Position = 0;
                            using var fs = new FileStream(absolutePath, FileMode.Create);
                            await ms.CopyToAsync(fs);
                            break;
                        }
                    }
                    quality -= 10;
                }
            }

            // Return relative path (forward slashes for DB storage)
            return $"{SubFolder}/{fileName}";
        }

        public string GetAbsolutePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return string.Empty;

            try
            {
                if (Path.IsPathRooted(relativePath))
                {
                    return Path.GetFullPath(relativePath);
                }

                var normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                return Path.GetFullPath(Path.Combine(_baseDirectory, normalizedRelative));
            }
            catch
            {
                return string.Empty;
            }
        }

        public void DeleteProductImage(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return;

            try
            {
                var absolutePath = GetAbsolutePath(relativePath);
                if (!string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath))
                {
                    File.Delete(absolutePath);
                }
            }
            catch (Exception)
            {
                // Silme işlemi kilit veya yetki durumunda akışı engellemez
            }
        }
    }
}
