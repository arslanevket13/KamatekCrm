using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

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
        /// Tedarikçi Tipi (Toptancı, Servis, Üretici vb.)
        /// </summary>
        public SupplierType SupplierType { get; set; } = SupplierType.Wholesaler;

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
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz")]
        public string? Email { get; set; }

        /// <summary>
        /// Web Sitesi
        /// </summary>
        [MaxLength(200)]
        [Url(ErrorMessage = "Geçerli bir URL giriniz")]
        public string? Website { get; set; }

        /// <summary>
        /// Vade Günü (0 = Peşin)
        /// </summary>
        [Range(0, 365, ErrorMessage = "Vade günü 0-365 arasında olmalıdır")]
        public int PaymentTermDays { get; set; } = 0;

        /// <summary>
        /// Borç Bakiyesi (Tedarikçiye olan borcumuz)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0;

        /// <summary>
        /// İlgili Kişi
        /// </summary>
        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        /// <summary>
        /// Vergi Dairesi
        /// </summary>
        [MaxLength(100)]
        public string? TaxOffice { get; set; }

        /// <summary>
        /// Vergi Numarası
        /// </summary>
        [MaxLength(20)]
        public string? TaxNumber { get; set; }

        /// <summary>
        /// IBAN
        /// </summary>
        [MaxLength(50)]
        public string? IBAN { get; set; }

        /// <summary>
        /// Notlar
        /// </summary>
        [MaxLength(1000)]
        public string? Notes { get; set; }

        /// <summary>
        /// Aktif mi?
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
