using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
    public class ServiceJobHistory
    {
        [Key]
        public int Id { get; set; }

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
    }
}
