using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.DTOs.ServiceJobs
{
    /// <summary>
    /// İş emri detay görünümü için tam DTO.
    /// Cihaz, SLA, maliyet ve teknisyen bilgilerini içerir.
    /// </summary>
    public class ServiceJobDetailDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Müşteri Bilgileri
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public int CustomerId { get; set; }

        // Durum & Atama
        public JobStatus Status { get; set; }
        public string StatusDisplay { get; set; } = string.Empty;
        public JobPriority Priority { get; set; }
        public WorkOrderType WorkOrderType { get; set; }
        public string WorkOrderTypeDisplay { get; set; } = string.Empty;
        public string? AssignedTechnician { get; set; }
        public int? AssignedUserId { get; set; }

        // Tarih & SLA
        public DateTime? ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SlaDeadline { get; set; }
        public string SlaStatus { get; set; } = string.Empty;
        public bool IsSlaBreached { get; set; }
        public int? EstimatedDuration { get; set; }
        public int? ActualDuration { get; set; }

        // Cihaz Bilgileri
        public string? DeviceBrand { get; set; }
        public string? DeviceModel { get; set; }
        public string? SerialNumber { get; set; }
        public string? Accessories { get; set; }
        public string? PhysicalCondition { get; set; }
        public string? TechnicianNotes { get; set; }

        // Maliyet
        public decimal Price { get; set; }
        public decimal LaborCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }

        // Diğer
        public string? GpsLocation { get; set; }
        public bool IsOffSite { get; set; }
        public string? Source { get; set; }
        public bool IsCustomerApproved { get; set; }
        public bool HasPhotos { get; set; }
        public bool BelongsToProject { get; set; }
    }
}
