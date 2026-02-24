using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using KamatekCrm.API.Services;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;
using System.Security.Claims;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace KamatekCrm.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PhotoController : ControllerBase
    {
        private readonly IPhotoStorageService _photoStorage;
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly string[] _allowedExtensions;
        private readonly string[] _allowedMimeTypes;
        private readonly long _maxFileSize;

        public PhotoController(
            IPhotoStorageService photoStorage, 
            AppDbContext context,
            IConfiguration configuration)
        {
            _photoStorage = photoStorage;
            _context = context;
            _configuration = configuration;
            
            _allowedExtensions = _configuration.GetSection("FileUpload:AllowedExtensions").Get<string[]>() 
                ?? new[] { ".jpg", ".jpeg", ".png", ".gif" };
            _allowedMimeTypes = _configuration.GetSection("FileUpload:AllowedMimeTypes").Get<string[]>()
                ?? new[] { "image/jpeg", "image/png", "image/gif" };
            _maxFileSize = (_configuration.GetValue<int>("FileUpload:MaxSizeMB", 10)) * 1024 * 1024;
        }
        
        private bool IsValidFile(IFormFile file, out string errorMessage)
        {
            errorMessage = string.Empty;
            
            // Dosya boyutu kontrolü
            if (file.Length > _maxFileSize)
            {
                errorMessage = $"Dosya boyutu çok büyük. Maksimum: {_maxFileSize / 1024 / 1024}MB";
                return false;
            }
            
            // Dosya uzantısı kontrolü
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                errorMessage = $"Geçersiz dosya uzantısı. İzin verilenler: {string.Join(", ", _allowedExtensions)}";
                return false;
            }
            
            // MIME type kontrolü
            if (!_allowedMimeTypes.Contains(file.ContentType))
            {
                errorMessage = $"Geçersiz dosya tipi. İzin verilenler: {string.Join(", ", _allowedMimeTypes)}";
                return false;
            }
            
            return true;
        }

        [HttpPost("upload/{taskId}")]
        public async Task<IActionResult> Upload(int taskId, IFormFile file, [FromForm] string? description)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { Success = false, Message = "Dosya seçilmedi" });
            
            // Dosya validasyonu
            if (!IsValidFile(file, out var errorMessage))
                return BadRequest(new { Success = false, Message = errorMessage });

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { Success = false, Message = "Geçersiz kullanıcı" });

            try
            {
                var (fileName, filePath, thumbnailPath) = await _photoStorage.SavePhotoAsync(file, "task_photos");

                var photo = new TaskPhoto
                {
                    TaskId = taskId,
                    FileName = fileName,
                    FilePath = filePath,
                    ThumbnailPath = thumbnailPath,
                    FileSize = file.Length,
                    MimeType = file.ContentType,
                    Description = description,
                    UploadedBy = userId,
                    UploadedAt = DateTime.Now
                };

                _context.TaskPhotos.Add(photo);
                await _context.SaveChangesAsync();

                return Ok(new { Success = true, Data = photo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var photo = await _context.TaskPhotos.FindAsync(id);
            if (photo == null) return NotFound();

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            // Authorization check: only uploader or admin/manager can delete
            if (photo.UploadedBy != userId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            // Soft delete record
            photo.IsDeleted = true;
            photo.DeletedAt = DateTime.Now;
            photo.DeletedBy = userId.ToString(); // Assuming DeletedBy is string in BaseEntity

            // Optionally delete physical file immediately or keep it
            // _photoStorage.DeletePhoto(photo.FilePath); 

            await _context.SaveChangesAsync();

            return Ok(new { Success = true });
        }
    }
}
