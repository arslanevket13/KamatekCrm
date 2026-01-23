using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Satış Siparişi Kalemi
    /// </summary>
    public class SalesOrderItem
    {
        /// <summary>
        /// Kalem ID (PK)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Bağlı Sipariş ID (FK)
        /// </summary>
        [Required]
        public int SalesOrderId { get; set; }

        /// <summary>
        /// Bağlı Sipariş
        /// </summary>
        [ForeignKey(nameof(SalesOrderId))]
        public virtual SalesOrder SalesOrder { get; set; } = null!;

        /// <summary>
        /// Ürün ID (FK)
        /// </summary>
        [Required]
        public int ProductId { get; set; }

        /// <summary>
        /// Bağlı Ürün
        /// </summary>
        [ForeignKey(nameof(ProductId))]
        public virtual Product Product { get; set; } = null!;

        /// <summary>
        /// Ürün Adı (Satış anındaki snapshot)
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Satış Adedi
        /// </summary>
        [Required]
        public int Quantity { get; set; }

        /// <summary>
        /// Birim Fiyat (Satış anındaki snapshot)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Toplam Tutar (Quantity × UnitPrice)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice => Quantity * UnitPrice;
    }
}
