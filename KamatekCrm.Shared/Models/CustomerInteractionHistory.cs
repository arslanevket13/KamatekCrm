using System;
using System.ComponentModel.DataAnnotations;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
    /// <summary>
    /// Müşteri Görüşmesi Durum ve Atama Geçmişi Entity
    /// </summary>
    public class CustomerInteractionHistory
    {
        [Key]
        public int Id { get; set; }

        public int CustomerInteractionId { get; set; }

        public InteractionStatus PreviousStatus { get; set; }

        public InteractionStatus NewStatus { get; set; }

        [MaxLength(150)]
        public string? PreviousAssignedToUsername { get; set; }

        [MaxLength(150)]
        public string? NewAssignedToUsername { get; set; }

        [Required]
        [MaxLength(150)]
        public string ChangedByUsername { get; set; } = string.Empty;

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Reason { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual CustomerInteraction? CustomerInteraction { get; set; }
    }
}
