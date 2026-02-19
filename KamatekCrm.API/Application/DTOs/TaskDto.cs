using System;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.API.Application.DTOs
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
        public CustomerDto Customer { get; set; } = null!;
        public string Category { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    public class CustomerDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
    }
}
