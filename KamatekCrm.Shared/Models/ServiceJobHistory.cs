using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
    public class ServiceJobHistory : KamatekCrm.Shared.Models.Common.BaseEntity
    {
        // Id is in BaseEntity

        [Required]
        public int ServiceJobId { get; set; }

        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        public RepairStatus? StatusChange { get; set; }
        public JobStatus? JobStatusChange { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [Required]
        [MaxLength(500)]
        public string TechnicianNote { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? UserId { get; set; }

        [ForeignKey(nameof(ServiceJobId))]
        public virtual ServiceJob ServiceJob { get; set; } = null!;

        // --- New Fields for Technician Web App ---
        public string Action { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public int PerformedBy { get; set; }
        public DateTime PerformedAt { get; set; } = DateTime.Now;
    }
}
