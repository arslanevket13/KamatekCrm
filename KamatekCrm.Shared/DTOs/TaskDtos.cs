using System;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.DTOs
{
    public class TaskDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public JobStatus Status { get; set; }
        public JobPriority Priority { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public int? EstimatedDuration { get; set; }
        public CustomerDto Customer { get; set; } = new CustomerDto();
        public string Category { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class TaskDetailDto : TaskDto
    {
        public List<TaskHistoryDto> History { get; set; } = new();
        public List<MaterialDto> UsedMaterials { get; set; } = new();
        public List<PhotoDto> Photos { get; set; } = new();
    }

    public class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class TaskHistoryDto
    {
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string UpdatedBy { get; set; } = string.Empty;
    }

    public class MaterialDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
    
    public class PhotoDto
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
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
