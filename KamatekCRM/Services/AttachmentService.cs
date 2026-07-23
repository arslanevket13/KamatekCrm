using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Dosya Eki Yönetim Servisi - Dijital Arşiv
    /// Dosyaları AppData/KamatekArchive'e kaydeder ve yönetir.
    /// </summary>
    public class AttachmentService
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IAuthService _authService;
        private readonly string _archivePath;

        public AttachmentService(IAuthService authService, IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _authService = authService;
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            
            // Arşiv klasörü: %AppData%/KamatekArchive
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _archivePath = Path.Combine(appData, "KamatekArchive");
            
            // Klasör yoksa oluştur
            if (!Directory.Exists(_archivePath))
            {
                Directory.CreateDirectory(_archivePath);
            }
        }

        /// <summary>
        /// Dosya yükle ve kaydet
        /// </summary>
        public Attachment UploadFile(AttachmentEntityType entityType, int entityId, string sourceFilePath, string? description = null)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Kaynak dosya bulunamadı.", sourceFilePath);

            try
            {
                var fileInfo = new FileInfo(sourceFilePath);
                var originalFileName = fileInfo.Name;
                var extension = fileInfo.Extension.ToLowerInvariant();
                
                var guidFileName = $"{Guid.NewGuid()}{extension}";
                var destinationPath = Path.Combine(_archivePath, guidFileName);

                File.Copy(sourceFilePath, destinationPath, overwrite: true);

                var contentType = GetContentType(extension);

                var attachment = new Attachment
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    FilePath = destinationPath,
                    FileName = originalFileName,
                    FileSize = fileInfo.Length,
                    ContentType = contentType,
                    UploadDate = DateTime.Now,
                    UploadedBy = _authService.CurrentUser?.AdSoyad ?? "Sistem",
                    Description = description ?? string.Empty
                };

                using var context = _dbContextFactory.CreateDbContext();
                context.Attachments.Add(attachment);
                context.SaveChanges();

                return attachment;
            }
            catch (IOException ex)
            {
                throw new Exception($"Dosya kopyalama hatası: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Stream'den dosya yükle (drag & drop için)
        /// </summary>
        public Attachment UploadFromStream(AttachmentEntityType entityType, int entityId, Stream stream, string fileName, string? description = null)
        {
            try
            {
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                var guidFileName = $"{Guid.NewGuid()}{extension}";
                var destinationPath = Path.Combine(_archivePath, guidFileName);

                using (var fileStream = File.Create(destinationPath))
                {
                    stream.CopyTo(fileStream);
                }

                var fileInfo = new FileInfo(destinationPath);
                var contentType = GetContentType(extension);

                var attachment = new Attachment
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    FilePath = destinationPath,
                    FileName = fileName,
                    FileSize = fileInfo.Length,
                    ContentType = contentType,
                    UploadDate = DateTime.Now,
                    UploadedBy = _authService.CurrentUser?.AdSoyad ?? "Sistem",
                    Description = description ?? string.Empty
                };

                using var context = _dbContextFactory.CreateDbContext();
                context.Attachments.Add(attachment);
                context.SaveChanges();

                return attachment;
            }
            catch (Exception ex)
            {
                throw new Exception($"Dosya yükleme hatası: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Entity'ye bağlı ekleri listele
        /// </summary>
        public List<Attachment> GetAttachments(AttachmentEntityType entityType, int entityId)
        {
            using var context = _dbContextFactory.CreateDbContext();
            return context.Attachments
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .OrderByDescending(a => a.UploadDate)
                .ToList();
        }

        /// <summary>
        /// Tek ek getir
        /// </summary>
        public Attachment? GetById(int id)
        {
            using var context = _dbContextFactory.CreateDbContext();
            return context.Attachments.Find(id);
        }

        /// <summary>
        /// Eki sil (dosya + veritabanı)
        /// </summary>
        public bool DeleteAttachment(int id)
        {
            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                var attachment = context.Attachments.Find(id);
                if (attachment == null) return false;

                if (File.Exists(attachment.FilePath))
                {
                    File.Delete(attachment.FilePath);
                }

                context.Attachments.Remove(attachment);
                context.SaveChanges();

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Dosyayı aç (varsayılan uygulama ile)
        /// </summary>
        public void OpenFile(Attachment attachment)
        {
            if (!File.Exists(attachment.FilePath))
                throw new FileNotFoundException("Dosya bulunamadı.", attachment.FilePath);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = attachment.FilePath,
                UseShellExecute = true
            });
        }

        private static string GetContentType(string extension)
        {
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".txt" => "text/plain",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
        }
    }
}
