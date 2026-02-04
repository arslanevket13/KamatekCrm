using System.Security.Claims;
using KamatekCrm.API.Data;
using KamatekCrm.Shared.DTOs;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.API.Controllers
{
    [Route("api/jobs")]
    [ApiController]
    [Authorize] // Sadece giriş yapmış kullanıcılar erişebilir
    public class TechnicianJobsController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public TechnicianJobsController(ApiDbContext context)
        {
            _context = context;
        }

        // GET: api/jobs/my-jobs
        [HttpGet("my-jobs")]
        public async Task<ActionResult<IEnumerable<TechnicianJobDto>>> GetMyJobs()
        {
            var userIdStr = User.FindFirst("id")?.Value;
            if (!int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized();
            }

            // Teknisyene atanmış ve tamamlanmamış işleri getir
            var jobEntities = await _context.ServiceJobs
                .Include(j => j.Customer)
                .Where(j => j.AssignedUserId == userId 
                            && j.Status != JobStatus.Completed 
                            && j.Status != JobStatus.Cancelled)
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.CreatedDate)
                .ToListAsync();

            var jobDtos = jobEntities.Select(j => new TechnicianJobDto
            {
                Id = j.Id,
                Title = $"{j.Customer.FullName} - {j.WorkOrderType}",
                CustomerName = j.Customer.FullName,
                CustomerPhone = j.Customer.PhoneNumber,
                Address = j.Customer.FullAddress,
                Priority = j.Priority.ToString(),
                Status = j.Status.ToString(),
                Latitude = j.Customer.Latitude,
                Longitude = j.Customer.Longitude,
                Description = j.Description,
                Date = j.ScheduledDate ?? j.CreatedDate
            }).ToList();

            return Ok(jobDtos);
        }

        // GET: api/jobs/detail/{id}
        [HttpGet("detail/{id}")]
        public async Task<ActionResult<TechnicianJobDetailDto>> GetJobDetail(int id)
        {
            var job = await _context.ServiceJobs
                .Include(j => j.Customer)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
            {
                return NotFound("İş bulunamadı.");
            }

            // Tarihçe
            var historyEntities = await _context.ServiceJobHistories
                .Where(h => h.ServiceJobId == id)
                .OrderByDescending(h => h.Date)
                .ToListAsync();

            var history = historyEntities.Select(h => new ServiceJobHistoryDto
                {
                    Date = h.Date,
                    Status = h.JobStatusChange.HasValue ? h.JobStatusChange.ToString()! : (h.StatusChange.HasValue ? h.StatusChange.ToString()! : "-"),
                    Note = h.TechnicianNote,
                    User = h.UserId ?? "Sistem"
                })
                .ToList();

            var detailDto = new TechnicianJobDetailDto
            {
                Id = job.Id,
                Title = $"{job.Customer.FullName} - {job.WorkOrderType}",
                CustomerName = job.Customer.FullName,
                CustomerPhone = job.Customer.PhoneNumber,
                Address = job.Customer.FullAddress,
                Priority = job.Priority.ToString(),
                Status = job.Status.ToString(),
                Latitude = job.Customer.Latitude,
                Longitude = job.Customer.Longitude,
                Description = job.Description,
                Date = job.ScheduledDate ?? job.CreatedDate,
                
                History = history
            };

            return Ok(detailDto);
        }

        // POST: api/jobs/update-status
        [HttpPost("update-status")]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusRequest request)
        {
            var userIdStr = User.FindFirst("id")?.Value;
            var username = User.Identity?.Name ?? "Unknown";
            
            var job = await _context.ServiceJobs.FindAsync(request.JobId);
            if (job == null)
            {
                return NotFound("İş bulunamadı.");
            }

            var oldStatus = job.Status;
            var newStatusEnum = (JobStatus)request.NewStatus;

            job.Status = newStatusEnum;

            if (newStatusEnum == JobStatus.Completed && oldStatus != JobStatus.Completed)
            {
                job.CompletedDate = DateTime.Now;
            }

            var history = new ServiceJobHistory
            {
                ServiceJobId = job.Id,
                Date = DateTime.Now,
                TechnicianNote = request.TechnicianNote,
                JobStatusChange = newStatusEnum,
                UserId = username,
                Latitude = request.CurrentLatitude,
                Longitude = request.CurrentLongitude
            };

            _context.ServiceJobHistories.Add(history);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Durum güncellendi." });
        }
    }
}
