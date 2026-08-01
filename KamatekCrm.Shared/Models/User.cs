using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Shared.Models
{
    public class User : KamatekCrm.Shared.Models.Common.BaseEntity
    {
        // Id is in BaseEntity

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "Viewer";

        [Required]
        [MaxLength(50)]
        public string Ad { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Soyad { get; set; } = string.Empty;

        [NotMapped]
        public string AdSoyad => $"{Ad} {Soyad}".Trim();

        [NotMapped]
        public string FullName => !string.IsNullOrWhiteSpace(AdSoyad) ? AdSoyad : Username;

        public bool IsActive { get; set; } = true;
        // CreatedDate is in BaseEntity (CreatedAt)
        public DateTime? LastLoginDate { get; set; }
        public bool MustChangePassword { get; set; }

        #region RBAC - Granular Permissions
        public bool CanViewFinance { get; set; } = false;
        public bool CanViewAnalytics { get; set; } = false;
        public bool CanDeleteRecords { get; set; } = false;
        public bool CanApprovePurchase { get; set; } = false;
        public bool CanAccessSettings { get; set; } = false;
        #endregion

        #region Teknisyen & Saha Alanları

        /// <summary>
        /// Teknisyen türü mü?
        /// </summary>
        public bool IsTechnician { get; set; }

        /// <summary>
        /// Teknisyen telefon numarası
        /// </summary>
        [MaxLength(20)]
        public string? Phone { get; set; }

        /// <summary>
        /// Teknisyen araç plakası
        /// </summary>
        [MaxLength(20)]
        public string? VehiclePlate { get; set; }

        /// <summary>
        /// Hizmet/Saha Bölgesi (Örn: "İstanbul Avrupa", "Kadıköy")
        /// </summary>
        [MaxLength(100)]
        public string? ServiceArea { get; set; }

        /// <summary>
        /// Uzmanlık alanları (Virgülle ayrılmış: "CCTV, Alarm, Yangın")
        /// </summary>
        [MaxLength(250)]
        public string? ExpertiseAreas { get; set; }

        /// <summary>
        /// Specialties alias (Geriye uyumluluk)
        /// </summary>
        [NotMapped]
        public string? Specialties
        {
            get => ExpertiseAreas;
            set => ExpertiseAreas = value;
        }

        /// <summary>
        /// Anlık GPS enlemi (Çalışma zamanı bilgisi)
        /// </summary>
        [NotMapped]
        public double? CurrentLatitude { get; set; }

        /// <summary>
        /// Anlık GPS boylamı (Çalışma zamanı bilgisi)
        /// </summary>
        [NotMapped]
        public double? CurrentLongitude { get; set; }

        /// <summary>
        /// Konum güncellenme zamanı
        /// </summary>
        [NotMapped]
        public DateTime? LocationUpdatedAt { get; set; }

        /// <summary>
        /// Teknisyenin müsaitlik durumu
        /// </summary>
        [NotMapped]
        public bool IsAvailable { get; set; } = true;

        /// <summary>
        /// Atanmış olan aktif servis işlerinin sayısı (Çalışma zamanı hesaplanır)
        /// </summary>
        [NotMapped]
        public int ActiveJobCount { get; set; } = 0;

        /// <summary>
        /// Tamamlanan toplam iş sayısı (Çalışma zamanı hesaplanır)
        /// </summary>
        [NotMapped]
        public int CompletedJobCount { get; set; } = 0;

        /// <summary>
        /// Teknisyen performans puanı (0.0 - 5.0)
        /// </summary>
        [NotMapped]
        public double Rating { get; set; } = 5.0;

        #endregion
    }
}
