using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Models
{
    /// <summary>
    /// İşte kullanılan ürünler entity'si
    /// </summary>
    public class ServiceJobItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ServiceJobId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public int QuantityUsed { get; set; }

        /// <summary>
        /// Ürünün düşüldüğü depo (Örn: Teknisyen Aracı)
        /// </summary>
        public int? WarehouseId { get; set; }

        [ForeignKey(nameof(WarehouseId))]
        public virtual Warehouse? Warehouse { get; set; }

        /// <summary>
        /// Kullanım anındaki birim maliyet
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        /// <summary>
        /// Müşteriye yansıtılan birim fiyat (Servis anında değişebilir)
        /// Product.SalePrice'dan gelebilir ama override edilebilir.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [ForeignKey(nameof(ServiceJobId))]
        public virtual ServiceJob ServiceJob { get; set; } = null!;

        [ForeignKey(nameof(ProductId))]
        public virtual Product Product { get; set; } = null!;
    }
}
