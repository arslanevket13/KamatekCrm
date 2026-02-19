using MediatR;
using KamatekCrm.Shared.DTOs;
using KamatekCrm.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace KamatekCrm.Application.Queries.Tasks
{
    public class GetTaskDetailQuery : IRequest<TaskDetailDto>
    {
        public int TaskId { get; set; }
    }

    public class GetTaskDetailQueryHandler : IRequestHandler<GetTaskDetailQuery, TaskDetailDto>
    {
        private readonly AppDbContext _context;

        public GetTaskDetailQueryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<TaskDetailDto?> Handle(GetTaskDetailQuery request, CancellationToken cancellationToken)
        {
            var job = await _context.ServiceJobs
                .Include(j => j.Customer)
                .Include(j => j.ServiceJobItems)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(j => j.Id == request.TaskId, cancellationToken);

            if (job == null) return null;

            var photos = await _context.TaskPhotos
                .Where(p => p.TaskId == request.TaskId && !p.IsDeleted)
                .Select(p => new PhotoDto
                {
                    Id = p.Id,
                    Url = p.FilePath, // TODO: Use proper URL resolution
                    ThumbnailUrl = p.ThumbnailPath ?? p.FilePath,
                    Description = p.Description ?? string.Empty
                })
                .ToListAsync(cancellationToken);

            var history = await _context.ServiceJobHistories
                .Where(h => h.ServiceJobId == request.TaskId && !h.IsDeleted)
                .OrderByDescending(h => h.Date)
                .Select(h => new TaskHistoryDto
                {
                    Date = h.Date,
                    Status = h.JobStatusChange.HasValue ? h.JobStatusChange.ToString() : (h.StatusChange.HasValue ? h.StatusChange.ToString() : "Info"),
                    Note = h.TechnicianNote,
                    UpdatedBy = h.UserId ?? "System"
                })
                .ToListAsync(cancellationToken);

            var taskDetail = new TaskDetailDto
            {
                Id = job.Id,
                Title = job.Title ?? $"İş #{job.Id}",
                Description = job.Description,
                Status = job.Status,
                Priority = job.Priority,
                ScheduledDate = job.ScheduledDate,
                EstimatedDuration = 60,
                Customer = new CustomerDto
                {
                    Id = job.Customer.Id,
                    Name = job.Customer.FullName,
                    Phone = job.Customer.PhoneNumber,
                    Address = job.Customer.FullAddress,
                    City = job.Customer.City,
                    District = job.Customer.District,
                    Latitude = job.Customer.Latitude,
                    Longitude = job.Customer.Longitude
                },
                Category = job.JobCategory.ToString(),
                CreatedAt = job.CreatedDate,
                
                History = history,
                Photos = photos,
                UsedMaterials = job.ServiceJobItems.Select(i => new MaterialDto
                {
                    Id = i.Id,
                    Name = i.Product.ProductName,
                    Quantity = i.QuantityUsed,
                    Unit = i.Product.Unit
                }).ToList()
            };
            
            return taskDetail;
        }
    }
}
