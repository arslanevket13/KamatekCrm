using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Satış Siparişi (POS)
    /// </summary>
    public class SalesOrder
    {
        /// <summary>
        /// Sipariş ID (PK)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Sipariş Numarası (SO-yyyyMMdd-xxx)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string OrderNumber { get; set; } = string.Empty;

        /// <summary>
        /// Satış Tarihi
        /// </summary>
        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        /// <summary>
        /// Ödeme Yöntemi
        /// </summary>
        [Required]
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

        /// <summary>
        /// Toplam Tutar
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Müşteri Adı (Opsiyonel - varsayılan: Perakende Müşteri)
        /// </summary>
        [MaxLength(200)]
        public string? CustomerName { get; set; }

        /// <summary>
        /// Sipariş Kalemleri
        /// </summary>
        public virtual ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();

        /// <summary>
        /// İşlemi Yapan Kullanıcı
        /// </summary>
        [MaxLength(100)]
        public string? UserId { get; set; }

        /// <summary>
        /// Açıklama/Not
        /// </summary>
        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
