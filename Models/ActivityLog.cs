using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Sistem aktivite logu - Kim, ne zaman, ne yaptı
    /// </summary>
    public class ActivityLog
    {
        /// <summary>
        /// Benzersiz ID
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// İşlemi yapan kullanıcı ID
        /// </summary>
        public int? UserId { get; set; }

        /// <summary>
        /// Kullanıcı adı (hızlı görüntüleme için cache)
        /// </summary>
        [MaxLength(100)]
        public string? Username { get; set; }

        /// <summary>
        /// İşlem tipi (Create, Update, Delete, Login, Logout, PasswordChange)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ActionType { get; set; } = string.Empty;

        /// <summary>
        /// Etkilenen entity adı (Customer, Product, User, etc.)
        /// </summary>
        [MaxLength(100)]
        public string? EntityName { get; set; }

        /// <summary>
        /// Etkilenen kayıt ID'si
        /// </summary>
        [MaxLength(50)]
        public string? RecordId { get; set; }

        /// <summary>
        /// İşlem açıklaması (detaylar)
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// IP adresi (opsiyonel)
        /// </summary>
        [MaxLength(50)]
        public string? IpAddress { get; set; }

        /// <summary>
        /// İşlem zamanı
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Ek veri (JSON formatında)
        /// </summary>
        public string? AdditionalData { get; set; }

        #region Navigation Properties

        /// <summary>
        /// İlişkili kullanıcı
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        #endregion

        #region Helper Properties (Not Mapped)

        /// <summary>
        /// İşlem tipi ikonu
        /// </summary>
        [NotMapped]
        public string ActionIcon => ActionType switch
        {
            "Login" => "🔓",
            "Logout" => "🚪",
            "Create" => "➕",
            "Update" => "✏️",
            "Delete" => "🗑️",
            "PasswordChange" => "🔑",
            "PasswordReset" => "🔄",
            _ => "📝"
        };

        /// <summary>
        /// İşlem tipi Türkçe gösterimi
        /// </summary>
        [NotMapped]
        public string ActionTypeDisplay => ActionType switch
        {
            "Login" => "Giriş",
            "Logout" => "Çıkış",
            "Create" => "Oluşturma",
            "Update" => "Güncelleme",
            "Delete" => "Silme",
            "PasswordChange" => "Şifre Değişikliği",
            "PasswordReset" => "Şifre Sıfırlama",
            _ => ActionType
        };

        /// <summary>
        /// Özet gösterimi
        /// </summary>
        [NotMapped]
        public string Summary => $"{ActionIcon} {ActionTypeDisplay}: {Description}";

        #endregion
    }
}
