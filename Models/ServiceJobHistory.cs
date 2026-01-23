using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Tamir işlemi tarihçesi (Timeline için)
    /// </summary>
    public class ServiceJobHistory
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// İlgili iş emri
        /// </summary>
        [Required]
        public int ServiceJobId { get; set; }

        /// <summary>
        /// İşlem tarihi
        /// </summary>
        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        /// <summary>
        /// Değişen durum (Opsiyonel)
        /// </summary>
        public RepairStatus? StatusChange { get; set; }

        /// <summary>
        /// Teknisyen notu / İşlem detayı
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string TechnicianNote { get; set; } = string.Empty;

        /// <summary>
        /// İşlemi yapan kullanıcı
        /// </summary>
        [MaxLength(100)]
        public string? UserId { get; set; }

        /// <summary>
        /// Navigation Property
        /// </summary>
        [ForeignKey(nameof(ServiceJobId))]
        public virtual ServiceJob ServiceJob { get; set; } = null!;
    }
}
