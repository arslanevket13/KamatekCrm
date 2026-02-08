using System;
using System.ComponentModel.DataAnnotations;

namespace KamatekCrm.Shared.Models.Common
{
    public abstract class BaseEntity : ISoftDeletable, IAuditable
    {
        [Key]
        public int Id { get; set; }
        
        // Soft Delete
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        
        // Audit Trail
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
    }
}
