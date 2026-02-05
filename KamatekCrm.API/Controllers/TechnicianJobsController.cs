using KamatekCrm.API.Data;
using KamatekCrm.Shared.DTOs;
using KamatekCrm.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace KamatekCrm.API.Controllers
{
    [Route("api/technician")]
    [ApiController]
    [Authorize] // Require Login
    public class TechnicianJobsController : ControllerBase
    {
        private readonly ApiDbContext _context;

        public TechnicianJobsController(ApiDbContext context)
        {
            _context = context;
        }

        [HttpGet("my-jobs")]
        public async Task<ActionResult<List<TechnicianJobDto>>> GetMyJobs()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized("User ID not found in token");
            
            if (!int.TryParse(userIdClaim.Value, out int userId))
                 return Unauthorized("Invalid User ID");

            var jobs = await _context.ServiceJobs
                .Include(j => j.Customer)
                .Where(j => j.AssignedUserId == userId)
                .OrderBy(j => j.ScheduledDate)
                .Select(j => new TechnicianJobDto
                {
                    Id = j.Id,
                    Title = $"{j.JobCategory} - {j.ServiceJobType}",
                    CustomerName = j.Customer.FullName,
                    CustomerPhone = j.Customer.PhoneNumber,
                    Address = j.Customer.FullAddress,
                    Priority = j.Priority.ToString(),
                    Status = j.Status.ToString(),
                    Latitude = j.Customer.Latitude,
                    Longitude = j.Customer.Longitude,
                    Description = j.Description,
                    Date = j.ScheduledDate ?? j.CreatedDate
                })
                .ToListAsync();

            return Ok(jobs);
        }

        [HttpPost("update-status")]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusRequest request)
        {
             var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
             if (userIdClaim == null) return Unauthorized();
             int userId = int.Parse(userIdClaim.Value);

             var job = await _context.ServiceJobs.FindAsync(request.JobId);
             if (job == null) return NotFound();
             
             if (job.AssignedUserId != userId) return Forbid();

             job.Status = (JobStatus)request.NewStatus;
             
             // Log history (Minimal implementation)
             _context.ServiceJobHistories.Add(new KamatekCrm.Shared.Models.ServiceJobHistory
             {
                 ServiceJobId = job.Id,
                 Date = DateTime.Now,
                 JobStatusChange = job.Status,
                 TechnicianNote = request.TechnicianNote,
                 UserId = userId.ToString()
             });

             await _context.SaveChangesAsync();
             return Ok();
        }
    }
}
