using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Müşteri cihazı/varlığı - Kurulu sistemlerin kaydı
    /// </summary>
    public class CustomerAsset
    {
        /// <summary>
        /// Cihaz ID (Primary Key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Müşteri ID (Foreign Key)
        /// </summary>
        [Required]
        public int CustomerId { get; set; }

        /// <summary>
        /// Cihaz kategorisi
        /// </summary>
        [Required]
        public JobCategory Category { get; set; }

        /// <summary>
        /// Marka
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Brand { get; set; } = string.Empty;

        /// <summary>
        /// Model
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Seri numarası
        /// </summary>
        [MaxLength(100)]
        public string? SerialNumber { get; set; }

        /// <summary>
        /// Kurulum tarihi
        /// </summary>
        public DateTime? InstallDate { get; set; }

        /// <summary>
        /// Garanti bitiş tarihi
        /// </summary>
        public DateTime? WarrantyEndDate { get; set; }

        /// <summary>
        /// Konum (Örn: "Giriş Kapısı", "Bahçe", "1. Kat")
        /// </summary>
        [MaxLength(200)]
        public string? Location { get; set; }

        /// <summary>
        /// Cihaz durumu
        /// </summary>
        [Required]
        public AssetStatus Status { get; set; } = AssetStatus.Active;

        /// <summary>
        /// Notlar
        /// </summary>
        [MaxLength(1000)]
        public string? Notes { get; set; }

        /// <summary>
        /// Kayıt tarihi
        /// </summary>
        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// İlgili müşteri
        /// </summary>
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer Customer { get; set; } = null!;

        /// <summary>
        /// Bu cihaza ait iş emirleri
        /// </summary>
        public virtual ICollection<ServiceJob> ServiceJobs { get; set; } = new List<ServiceJob>();

        /// <summary>
        /// Tam cihaz adı (Computed)
        /// </summary>
        [NotMapped]
        public string FullName => $"{Brand} {Model}";

        /// <summary>
        /// Kategori ikonu (Computed)
        /// </summary>
        [NotMapped]
        public string CategoryIcon => Category switch
        {
            JobCategory.CCTV => "📹",
            JobCategory.VideoIntercom => "📞",
            JobCategory.FireAlarm => "🔥",
            JobCategory.BurglarAlarm => "🚨",
            JobCategory.SmartHome => "🏠",
            JobCategory.AccessControl => "🔐",
            JobCategory.SatelliteSystem => "📡",
            JobCategory.FiberOptic => "🔌",
            _ => "📦"
        };

        /// <summary>
        /// Durum gösterim metni
        /// </summary>
        [NotMapped]
        public string StatusDisplay => Status switch
        {
            AssetStatus.Active => "✅ Aktif",
            AssetStatus.NeedsRepair => "🔧 Tamir Gerekiyor",
            AssetStatus.UnderMaintenance => "🛠️ Bakımda",
            AssetStatus.Replaced => "🔄 Değiştirildi",
            AssetStatus.Retired => "📴 Kullanım Dışı",
            _ => Status.ToString()
        };

        /// <summary>
        /// Garanti durumu (Computed)
        /// </summary>
        [NotMapped]
        public bool IsUnderWarranty => WarrantyEndDate.HasValue && WarrantyEndDate.Value > DateTime.Now;
    }
}
