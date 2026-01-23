using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Mahalle modeli (Sadece UI için - Veritabanına kaydedilmez)
    /// </summary>
    [NotMapped]
    public class Neighborhood
    {
        /// <summary>
        /// Mahalle ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Mahalle adı
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Bağlı olduğu ilçe ID
        /// </summary>
        public int DistrictId { get; set; }
    }
}
