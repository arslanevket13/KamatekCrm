using MediatR;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KamatekCrm.API.Application.Queries.Tasks
{
    public class GetDashboardStatsQuery : IRequest<DashboardStatsDto>
    {
        public int TechnicianId { get; set; }
    }

    public class GetDashboardStatsQueryHandler 
        : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
    {
        private readonly AppDbContext _context;

        public GetDashboardStatsQueryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardStatsDto> Handle(
            GetDashboardStatsQuery request,
            CancellationToken cancellationToken)
        {
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);

            var tasks = await _context.ServiceJobs
                .Where(j => j.AssignedTechnicianId == request.TechnicianId && !j.IsDeleted)
                .ToListAsync(cancellationToken);

            var stats = new DashboardStatsDto
            {
                TotalTasks = tasks.Count,
                PendingTasks = tasks.Count(t => t.Status == JobStatus.Pending),
                InProgressTasks = tasks.Count(t => t.Status == JobStatus.InProgress),
                CompletedTasks = tasks.Count(t => t.Status == JobStatus.Completed),
                TodayTasks = tasks.Count(t => t.ScheduledDate?.Date == today),
                ThisWeekTasks = tasks.Count(t => t.ScheduledDate >= startOfWeek),
                UrgentTasks = tasks.Count(t => 
                    t.Priority == JobPriority.High || t.Priority == JobPriority.Critical), // Mapping Urgent to High/Critical
                CompletedToday = tasks.Count(t => 
                    t.Status == JobStatus.Completed && 
                    t.ModifiedDate?.Date == today),
                AverageCompletionTime = CalculateAverageCompletionTime(tasks)
            };

            return stats;
        }

        private double CalculateAverageCompletionTime(List<ServiceJob> tasks)
        {
            var completedTasks = tasks
                .Where(t => t.Status == JobStatus.Completed && 
                           t.CreatedDate != DateTime.MinValue && 
                           t.ModifiedDate != null)
                .ToList();

            if (!completedTasks.Any())
                return 0;

            var totalHours = completedTasks
                .Average(t => (t.ModifiedDate!.Value - t.CreatedDate).TotalHours);

            return Math.Round(totalHours, 1);
        }
    }

    public class DashboardStatsDto
    {
        public int TotalTasks { get; set; }
        public int PendingTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int TodayTasks { get; set; }
        public int ThisWeekTasks { get; set; }
        public int UrgentTasks { get; set; }
        public int CompletedToday { get; set; }
        public double AverageCompletionTime { get; set; }
    }
}
