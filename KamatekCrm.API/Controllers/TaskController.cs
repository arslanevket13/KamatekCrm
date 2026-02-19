using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using KamatekCrm.API.Application.Queries.Tasks;
using KamatekCrm.API.Application.Commands.Tasks;
using System.Security.Claims;
using KamatekCrm.Shared.Enums;
using KamatekCrm.API.Application.Common;

namespace KamatekCrm.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TaskController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TaskController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("technician/tasks")]
        public async Task<IActionResult> GetMyTasks(
            [FromQuery] JobStatus? status, 
            [FromQuery] DateTime? startDate, 
            [FromQuery] DateTime? endDate)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            // In a real scenario, we might need to map UserId to TechnicianId if they differ.
            // Assuming for now that UserId IS the TechnicianId (User table linked to Technician/Employee)
            // If User is separate from Technician entity, we would need a lookup.
            // For this implementation, let's assume the logged-in user ID corresponds to the AssignedTechnicianId in ServiceJob.

            var query = new GetTechnicianTasksQuery
            {
                TechnicianId = userId,
                Status = status,
                StartDate = startDate,
                EndDate = endDate
            };

            var result = await _mediator.Send(query);
            return Ok(new { Data = result, Success = true });
        }

        [HttpGet("dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var query = new GetDashboardStatsQuery
            {
                TechnicianId = userId
            };

            var result = await _mediator.Send(query);
            return Ok(new { Data = result, Success = true });
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var command = new UpdateTaskStatusCommand
            {
                TaskId = id,
                NewStatus = request.Status,
                Notes = request.Notes,
                UpdatedBy = userId
            };

            var result = await _mediator.Send(command);

            if (!result.IsSuccess)
            {
                return BadRequest(new { Success = false, Message = result.ErrorMessage });
            }

            return Ok(new { Success = true });
        }
    }

    public class UpdateStatusRequest
    {
        public JobStatus Status { get; set; }
        public string? Notes { get; set; }
    }
}
