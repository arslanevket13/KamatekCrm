using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Şehir modeli (Sadece UI için - Veritabanına kaydedilmez)
    /// </summary>
    [NotMapped]
    public class City
    {
        /// <summary>
        /// Şehir ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Şehir adı
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Bu şehre ait ilçeler
        /// </summary>
        public List<District> Districts { get; set; } = new List<District>();
    }
}
