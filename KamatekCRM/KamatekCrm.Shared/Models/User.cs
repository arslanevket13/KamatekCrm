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

        public bool IsActive { get; set; } = true;
        // CreatedDate is in BaseEntity (CreatedAt)
        public DateTime? LastLoginDate { get; set; }

        #region RBAC - Granular Permissions
        public bool CanViewFinance { get; set; } = false;
        public bool CanViewAnalytics { get; set; } = false;
        public bool CanDeleteRecords { get; set; } = false;
        public bool CanApprovePurchase { get; set; } = false;
        public bool CanAccessSettings { get; set; } = false;
        #endregion

        #region Teknisyen Alanları

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
        /// Çalışma bölgesi (il/ilçe)
        /// </summary>
        [MaxLength(100)]
        public string? ServiceArea { get; set; }

        /// <summary>
        ///Uzmanlık alanları (virgülle ayrılmış)
        /// </summary>
        [MaxLength(500)]
        public string? ExpertiseAreas { get; set; }

        /// <summary>
        /// Bugünkü GPS konumu
        /// </summary>
        [MaxLength(50)]
        public string? CurrentGpsLocation { get; set; }

        /// <summary>
        /// Son konum güncelleme zamanı
        /// </summary>
        public DateTime? LastLocationUpdate { get; set; }

        /// <summary>
        /// Aktif mi? (çalışıyor mu)
        /// </summary>
        public bool IsOnDuty { get; set; }

        /// <summary>
        /// Toplam tamamlanan iş sayısı
        /// </summary>
        public int TotalJobsCompleted { get; set; }

        /// <summary>
        /// Toplam müşteri memnuniyeti (1-5)
        /// </summary>
        public double? AverageRating { get; set; }

        #endregion
    }
}
