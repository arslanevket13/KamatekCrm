using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Bakım Sözleşmesi - SLA Otomasyonu
    /// Periyodik bakım işlerini otomatik oluşturur.
    /// </summary>
    public class MaintenanceContract
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// İlgili Müşteri
        /// </summary>
        [Required]
        public int CustomerId { get; set; }

        /// <summary>
        /// Sözleşme Başlangıç Tarihi
        /// </summary>
        [Required]
        public DateTime StartDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Sözleşme Bitiş Tarihi
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Bakım Sıklığı (Ay) - Örn: 3, 6, 12
        /// </summary>
        [Required]
        public int FrequencyInMonths { get; set; } = 3;

        /// <summary>
        /// Bir Sonraki Bakım Tarihi (Bu tarih geldiğinde otomasyon devreye girer)
        /// </summary>
        [Required]
        public DateTime NextDueDate { get; set; }

        /// <summary>
        /// Bakım Başına Ücret
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePerVisit { get; set; }

        /// <summary>
        /// Aktif mi?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Oluşturulacak İş Tanımı (Örn: "Periyodik CCTV Bakımı")
        /// </summary>
        [MaxLength(200)]
        public string JobDescriptionTemplate { get; set; } = "Periyodik Bakım";

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer Customer { get; set; } = null!;
    }
}
