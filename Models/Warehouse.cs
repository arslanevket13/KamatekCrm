using System.ComponentModel.DataAnnotations;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Depo / Lokasyon Entity'si
    /// </summary>
    public class Warehouse
    {
        /// <summary>
        /// Depo ID (Primary Key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Depo Adı (Örn: Merkez Depo, 34ABC12 Araç)
        /// </summary>
        [Required(ErrorMessage = "Depo adı zorunludur")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Depo Tipi
        /// </summary>
        [Required]
        public WarehouseType Type { get; set; }

        /// <summary>
        /// Aktif mi?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Bu depodaki stoklar
        /// </summary>
        public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
    }
}
