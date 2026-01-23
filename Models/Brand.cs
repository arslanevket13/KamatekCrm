using System.ComponentModel.DataAnnotations;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Ürün markası entity'si
    /// </summary>
    public class Brand
    {
        /// <summary>
        /// Marka ID (Primary Key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Marka adı
        /// </summary>
        [Required(ErrorMessage = "Marka adı zorunludur")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Bu markaya ait ürünler
        /// </summary>
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
