using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Satın alma kalemi
    /// </summary>
    public class PurchaseOrderItem
    {
        /// <summary>
        /// Kalem ID (Primary Key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Satın alma emri ID (Foreign Key)
        /// </summary>
        [Required]
        public int PurchaseOrderId { get; set; }

        /// <summary>
        /// Ürün ID (Foreign Key - Opsiyonel, yeni ürün için null)
        /// </summary>
        public int? ProductId { get; set; }

        /// <summary>
        /// Ürün adı (Kayıtlı veya manuel giriş)
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Miktar
        /// </summary>
        [Required]
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Birim fiyat
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// KDV Oranı (%, örn: 1, 10, 20)
        /// </summary>
        public int TaxRate { get; set; } = 20;

        /// <summary>
        /// İndirim Oranı (%)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountRate { get; set; } = 0;

        /// <summary>
        /// Ara Toplam (Miktar × Birim Fiyat)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        /// <summary>
        /// İndirim Tutarı
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        /// <summary>
        /// KDV Tutarı
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        /// <summary>
        /// Satır Toplamı (Ara Toplam - İndirim + KDV)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        /// <summary>
        /// Eski Total property (geriye uyumluluk için, LineTotal'a map edilebilir veya kaldırılabilir ama şimdilik tutalım)
        /// </summary>
        [NotMapped]
        public decimal Total => LineTotal;

        /// <summary>
        /// İlgili satın alma emri
        /// </summary>
        [ForeignKey(nameof(PurchaseOrderId))]
        public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;

        /// <summary>
        /// İlgili ürün (Opsiyonel)
        /// </summary>
        [ForeignKey(nameof(ProductId))]
        public virtual Product? Product { get; set; }
    }
}

