using System.ComponentModel.DataAnnotations;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models.Common;

namespace KamatekCrm.Shared.Models
{
    /// <summary>
    /// Müşteri aktivite timeline kaydı
    /// </summary>
    public class CustomerActivity : BaseEntity
    {
        public int CustomerId { get; set; }

        /// <summary>
        /// Aktivite tipi
        /// </summary>
        public ActivityType Type { get; set; }

        /// <summary>
        /// Aktivite açıklaması
        /// </summary>
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// İlgili kayıt ID (satış, servis işi vb.)
        /// </summary>
        public int? RelatedId { get; set; }

        /// <summary>
        /// İlgili kayıt tipi
        /// </summary>
        [MaxLength(50)]
        public string? RelatedType { get; set; }

        /// <summary>
        /// Kim tarafından yapıldı
        /// </summary>
        [MaxLength(100)]
        public new string? CreatedBy { get; set; }

        public virtual Customer? Customer { get; set; }
    }

    /// <summary>
    /// Aktivite tipleri
    /// </summary>
    public enum ActivityType
    {
        /// <summary>
        /// Müşteri oluşturuldu
        /// </summary>
        Created = 1,

        /// <summary>
        /// Müşteri güncellendi
        /// </summary>
        Updated = 2,

        /// <summary>
        /// Satış yapıldı
        /// </summary>
        Sale = 10,

        /// <summary>
        /// Ödeme alındı
        /// </summary>
        Payment = 11,

        /// <summary>
        /// Borç eklendi
        /// </summary>
        DebtAdded = 12,

        /// <summary>
        /// Servis işi oluşturuldu
        /// </summary>
        ServiceJobCreated = 20,

        /// <summary>
        /// Servis işi tamamlandı
        /// </summary>
        ServiceJobCompleted = 21,

        /// <summary>
        /// Arama yapıldı
        /// </summary>
        CallMade = 30,

        /// <summary>
        /// Ziyaret yapıldı
        /// </summary>
        VisitMade = 31,

        /// <summary>
        /// E-posta gönderildi
        /// </summary>
        EmailSent = 32,

        /// <summary>
        /// Not eklendi
        /// </summary>
        NoteAdded = 40,

        /// <summary>
        /// Etiket eklendi
        /// </summary>
        TagAdded = 41,

        /// <summary>
        /// Doğum günü kutlandı
        /// </summary>
        BirthdayCelebrated = 50
    }
}
