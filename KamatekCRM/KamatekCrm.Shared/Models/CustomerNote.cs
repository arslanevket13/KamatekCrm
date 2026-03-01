using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Shared.Models
{
    /// <summary>
    /// Müşteri notu / etkinlik kaydı — CRM timeline.
    /// Arama, ziyaret, teklif, şikayet, not, e-posta vs.
    /// </summary>
    public class CustomerNote
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string Content { get; set; } = "";
        public CustomerActivityType ActivityType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = "";
        public bool IsPinned { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer Customer { get; set; } = null!;
    }

    public enum CustomerActivityType
    {
        Note,
        PhoneCall,
        Visit,
        Email,
        Proposal,
        Complaint,
        Payment,
        ServiceRequest,
        Follow_Up
    }
}
