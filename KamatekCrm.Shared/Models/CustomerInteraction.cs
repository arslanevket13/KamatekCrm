using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
    /// <summary>
    /// Müşteri İletişim ve Talep Merkezi - Görüşme Kaydı Entity
    /// </summary>
    public class CustomerInteraction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string InteractionNumber { get; set; } = string.Empty;

        public int? CustomerId { get; set; }

        [MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string CallerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string CallerPhone { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string NormalizedPhone { get; set; } = string.Empty;

        public InteractionChannel Channel { get; set; } = InteractionChannel.Phone;

        public InteractionRequestType RequestType { get; set; } = InteractionRequestType.PriceQuote;

        [Required]
        [MaxLength(250)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Summary { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string? DetailedNotes { get; set; }

        public InteractionPriority Priority { get; set; } = InteractionPriority.Normal;

        public InteractionStatus Status { get; set; } = InteractionStatus.New;

        public DateTime InteractionDate { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string CreatedByUserId { get; set; } = string.Empty;

        [MaxLength(150)]
        public string CreatedByUsername { get; set; } = string.Empty;

        public int? AssignedToUserId { get; set; }

        [MaxLength(150)]
        public string? AssignedToUsername { get; set; }

        public bool RequiresFollowUp { get; set; }

        public DateTime? FollowUpDate { get; set; }

        public bool RequiresManagerAttention { get; set; }

        [MaxLength(2000)]
        public string? ManagerNotes { get; set; }

        public DateTime? CompletedDate { get; set; }

        [MaxLength(2000)]
        public string? ResolutionNotes { get; set; }

        [MaxLength(50)]
        public string? RelatedEntityType { get; set; }

        public int? RelatedEntityId { get; set; }

        [MaxLength(100)]
        public string? RelatedEntityNumber { get; set; }

        public bool IsDraft { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        public bool IsActive { get; set; } = true;

        // Foreign Key Relationships
        public virtual Customer? Customer { get; set; }
        public virtual User? AssignedToUser { get; set; }

        public virtual ICollection<CustomerInteractionHistory> Histories { get; set; } = new List<CustomerInteractionHistory>();
    }
}
