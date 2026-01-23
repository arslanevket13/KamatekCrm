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
        /// Toplam (Computed)
        /// </summary>
        [NotMapped]
        public decimal Total => Quantity * UnitPrice;

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
