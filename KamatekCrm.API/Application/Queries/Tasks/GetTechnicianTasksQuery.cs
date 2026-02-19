using MediatR;
using KamatekCrm.API.Application.DTOs;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KamatekCrm.API.Application.Queries.Tasks
{
    public class GetTechnicianTasksQuery : IRequest<List<TaskDto>>
    {
        public int TechnicianId { get; set; }
        public JobStatus? Status { get; set; } // Changed from TaskStatus to JobStatus
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class GetTechnicianTasksQueryHandler 
        : IRequestHandler<GetTechnicianTasksQuery, List<TaskDto>>
    {
        private readonly AppDbContext _context;

        public GetTechnicianTasksQueryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<TaskDto>> Handle(
            GetTechnicianTasksQuery request,
            CancellationToken cancellationToken)
        {
            var query = _context.ServiceJobs
                .Include(j => j.Customer)
                .Include(j => j.AssignedTechnician)
                .Include(j => j.JobCategory) // Include Category
                .Where(j => j.AssignedTechnicianId == request.TechnicianId && !j.IsDeleted);

            // Durum filtresi
            if (request.Status.HasValue)
            {
                query = query.Where(j => j.Status == request.Status.Value);
            }

            // Tarih filtresi
            if (request.StartDate.HasValue)
            {
                query = query.Where(j => j.ScheduledDate >= request.StartDate.Value);
            }

            if (request.EndDate.HasValue)
            {
                query = query.Where(j => j.ScheduledDate <= request.EndDate.Value);
            }

            var tasks = await query
                .OrderBy(j => j.Priority)
                .ThenBy(j => j.ScheduledDate)
                .Select(j => new TaskDto
                {
                    Id = j.Id,
                    Title = j.Title ?? $"İş #{j.Id}",
                    Description = j.Description,
                    Status = j.Status,
                    Priority = j.Priority,
                    ScheduledDate = j.ScheduledDate,
                    Customer = new CustomerDto
                    {
                        Id = j.Customer.Id,
                        FullName = j.Customer.FullName,
                        PhoneNumber = j.Customer.PhoneNumber,
                        Address = j.Customer.Street,
                        City = j.Customer.City,
                        District = j.Customer.District
                    },
                    Category = j.JobCategory.ToString(),
                    CreatedDate = j.CreatedDate
                })
                .ToListAsync(cancellationToken);

            return tasks;
        }
    }
}
