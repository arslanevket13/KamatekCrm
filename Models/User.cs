using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Kullanıcı entity'si - Kimlik doğrulama ve yetkilendirme için
    /// </summary>
    public class User
    {
        /// <summary>
        /// Kullanıcı ID (Primary Key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Kullanıcı adı (Benzersiz) - Otomatik oluşturulur: ad.soyad
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Şifre hash'i (SHA256)
        /// </summary>
        [Required]
        [MaxLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Kullanıcı rolü (Admin, Technician, Viewer)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "Viewer";

        /// <summary>
        /// Ad
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Ad { get; set; } = string.Empty;

        /// <summary>
        /// Soyad
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Soyad { get; set; } = string.Empty;

        /// <summary>
        /// Ad Soyad (Hesaplanmış - Veritabanında saklanmaz)
        /// </summary>
        [NotMapped]
        public string AdSoyad => $"{Ad} {Soyad}".Trim();

        /// <summary>
        /// Aktif mi?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Oluşturulma tarihi
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Son giriş tarihi
        /// </summary>
        public DateTime? LastLoginDate { get; set; }
    }
}

