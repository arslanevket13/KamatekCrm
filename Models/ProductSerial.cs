using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Ürün Seri Numaraları
    /// </summary>
    public class ProductSerial
    {
        /// <summary>
        /// ID (PK)
        /// </summary>
        [Key]
        public int Id { get; set; }

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
        /// Seri Numarası (Unique)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string SerialNumber { get; set; } = string.Empty;

        /// <summary>
        /// Güncel Depo ID (Şu an nerede?)
        /// </summary>
        public int? CurrentWarehouseId { get; set; }

        /// <summary>
        /// Güncel Depo
        /// </summary>
        [ForeignKey(nameof(CurrentWarehouseId))]
        public virtual Warehouse? CurrentWarehouse { get; set; }

        /// <summary>
        /// Durum (Satıldı, Stokta, Arızalı)
        /// </summary>
        [Required]
        public SerialStatus Status { get; set; } = SerialStatus.Available;

        /// <summary>
        /// Eklenme Tarihi
        /// </summary>
        public DateTime DateAdded { get; set; } = DateTime.Now;
    }
}
