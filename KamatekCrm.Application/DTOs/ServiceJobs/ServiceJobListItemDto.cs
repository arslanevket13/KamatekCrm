using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.DTOs.ServiceJobs
{
    /// <summary>
    /// İş emri listeleme (DataGrid / tablo) için hafifletilmiş DTO.
    /// </summary>
    public class ServiceJobListItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerFullName => CustomerName;
        public int CustomerId { get; set; }
        public JobStatus Status { get; set; }
        public string StatusDisplay { get; set; } = string.Empty;
        public JobPriority Priority { get; set; }
        public WorkOrderType WorkOrderType { get; set; }
        public string WorkOrderTypeDisplay { get; set; } = string.Empty;
        public string? AssignedTechnician { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string SlaStatus { get; set; } = string.Empty;
        public bool IsSlaBreached { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
