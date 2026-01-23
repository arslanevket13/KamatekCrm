using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Müşteri entity'si
    /// </summary>
    public class Customer
    {
        /// <summary>
        /// Müşteri ID (Primary Key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Müşteri tipi (Bireysel/Kurumsal)
        /// </summary>
        [Required]
        public CustomerType Type { get; set; } = CustomerType.Individual;

        /// <summary>
        /// Benzersiz müşteri kodu (Otomatik oluşturulur)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string CustomerCode { get; set; } = string.Empty;

        /// <summary>
        /// Müşteri adı soyadı (Bireysel için) veya Yetkili Kişi (Kurumsal için)
        /// </summary>
        [Required(ErrorMessage = "Ad Soyad zorunludur")]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Telefon numarası
        /// </summary>
        [Required(ErrorMessage = "Telefon numarası zorunludur")]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// E-posta adresi (İsteğe bağlı)
        /// </summary>
        [MaxLength(100)]
        public string? Email { get; set; }

        /// <summary>
        /// Şehir (Zorunlu)
        /// </summary>
        [Required(ErrorMessage = "Şehir zorunludur")]
        [MaxLength(50)]
        public string City { get; set; } = string.Empty;

        /// <summary>
        /// İlçe (İsteğe bağlı)
        /// </summary>
        [MaxLength(50)]
        public string? District { get; set; }

        /// <summary>
        /// Mahalle (İsteğe bağlı)
        /// </summary>
        [MaxLength(100)]
        public string? Neighborhood { get; set; }

        /// <summary>
        /// Sokak/Cadde (İsteğe bağlı)
        /// </summary>
        [MaxLength(200)]
        public string? Street { get; set; }

        /// <summary>
        /// Bina No (İsteğe bağlı)
        /// </summary>
        [MaxLength(10)]
        public string? BuildingNo { get; set; }

        /// <summary>
        /// Daire No (İsteğe bağlı)
        /// </summary>
        [MaxLength(10)]
        public string? ApartmentNo { get; set; }

        /// <summary>
        /// Müşteri notları (İsteğe bağlı)
        /// </summary>
        [MaxLength(2000)]
        public string? Notes { get; set; }

        // ========== BİREYSEL MÜŞTERİ ALANLARI ==========
        
        /// <summary>
        /// TC Kimlik Numarası (Sadece bireysel müşteriler için)
        /// </summary>
        [MaxLength(11)]
        public string? TcKimlikNo { get; set; }

        // ========== KURUMSAL MÜŞTERİ ALANLARI ==========
        
        /// <summary>
        /// Şirket Ünvanı (Sadece kurumsal müşteriler için)
        /// </summary>
        [MaxLength(300)]
        public string? CompanyName { get; set; }

        /// <summary>
        /// Vergi Numarası (Sadece kurumsal müşteriler için)
        /// </summary>
        [MaxLength(20)]
        public string? TaxNumber { get; set; }

        /// <summary>
        /// Vergi Dairesi (Sadece kurumsal müşteriler için)
        /// </summary>
        [MaxLength(100)]
        public string? TaxOffice { get; set; }

        /// <summary>
        /// Tam adres (Computed Property - Veritabanında saklanmaz)
        /// </summary>
        public string FullAddress
        {
            get
            {
                var parts = new List<string>();
                
                if (!string.IsNullOrWhiteSpace(Neighborhood)) parts.Add(Neighborhood);
                if (!string.IsNullOrWhiteSpace(Street)) parts.Add(Street);
                if (!string.IsNullOrWhiteSpace(BuildingNo)) parts.Add($"No: {BuildingNo}");
                if (!string.IsNullOrWhiteSpace(ApartmentNo)) parts.Add($"D: {ApartmentNo}");
                if (!string.IsNullOrWhiteSpace(District)) parts.Add(District);
                parts.Add(City);
                
                return string.Join(", ", parts);
            }
        }

        /// <summary>
        /// Bu müşteriye ait iş kayıtları
        /// </summary>
        public virtual ICollection<ServiceJob> ServiceJobs { get; set; } = new List<ServiceJob>();

        /// <summary>
        /// Bu müşteriye ait finansal işlemler
        /// </summary>
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
