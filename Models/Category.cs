using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Ürün kategorisi entity'si
    /// </summary>
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Kategori adı zorunludur")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Üst Kategori ID (Kök kategori ise null)
        /// </summary>
        public int? ParentId { get; set; }

        /// <summary>
        /// Üst Kategori
        /// </summary>
        [ForeignKey(nameof(ParentId))]
        public virtual Category? ParentCategory { get; set; }

        /// <summary>
        /// Alt Kategoriler
        /// </summary>
        public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
