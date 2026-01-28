using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Envanter / Mevcut Stok Durumu
    /// Hangi üründen, hangi depoda kaç adet var?
    /// </summary>
    public class Inventory
    {
        // Composite Key (ProductId + WarehouseId) AppDbContext içinde tanımlanacak

        /// <summary>
        /// Ürün ID (FK)
        /// </summary>
        [Required]
        public int ProductId { get; set; }

        /// <summary>
        /// İlgili Ürün
        /// </summary>
        [ForeignKey(nameof(ProductId))]
        public virtual Product Product { get; set; } = null!;

        /// <summary>
        /// Depo ID (FK)
        /// </summary>
        [Required]
        public int WarehouseId { get; set; }

        /// <summary>
        /// İlgili Depo
        /// </summary>
        [ForeignKey(nameof(WarehouseId))]
        public virtual Warehouse Warehouse { get; set; } = null!;

        /// <summary>
        /// Mevcut Miktar
        /// </summary>
        [Required]
        public int Quantity { get; set; }

        /// <summary>
        /// Eşzamanlılık kontrolü için RowVersion (Optimistic Concurrency)
        /// SQLite: BLOB olarak saklanır
        /// SQL Server: rowversion/timestamp olarak saklanır
        /// </summary>
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        /// <summary>
        /// Kritik seviye kontrolü için özel override (Opsiyonel)
        /// Ürün bazlı MinStockLevel'a bakılır genelde ama depo bazlı da olabilir.
        /// Şimdilik sadece miktar tutuyoruz.
        /// </summary>
    }
}
