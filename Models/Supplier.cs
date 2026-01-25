using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Tedarikçi Firma Entity'si
    /// </summary>
    public class Supplier
    {
        /// <summary>
        /// Tedarikçi ID (PK)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Firma Ünvanı
        /// </summary>
        [Required(ErrorMessage = "Firma ünvanı zorunludur")]
        [MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        /// <summary>
        /// İletişim Kişisi / Bilgileri
        /// </summary>
        [MaxLength(200)]
        public string? ContactInfo { get; set; }

        /// <summary>
        /// Telefon
        /// </summary>
        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Vergi Dairesi / No vb.
        /// </summary>
        [MaxLength(100)]
        public string? TaxInfo { get; set; }

        /// <summary>
        /// Adres
        /// </summary>
        [MaxLength(500)]
        public string? Address { get; set; }

        /// <summary>
        /// E-posta
        /// </summary>
        [MaxLength(100)]
        public string? Email { get; set; }

        /// <summary>
        /// Borç Bakiyesi (Tedarikçiye olan borcumuz)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0;

        /// <summary>
        /// Aktif mi?
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
