using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using MediatR;
using KamatekCrm.Services;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;
using System.Security.Claims;
using System;
using System.Threading.Tasks;

namespace KamatekCrm.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PhotoController : ControllerBase
    {
        private readonly IPhotoStorageService _photoStorage;
        private readonly AppDbContext _context;

        public PhotoController(IPhotoStorageService photoStorage, AppDbContext context)
        {
            _photoStorage = photoStorage;
            _context = context;
        }

        [HttpPost("upload/{taskId}")]
        public async Task<IActionResult> Upload(int taskId, IFormFile file, [FromForm] string? description)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Dosya seçilmedi");

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

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
