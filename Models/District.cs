using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Models
{
    /// <summary>
    /// İlçe modeli (Sadece UI için - Veritabanına kaydedilmez)
    /// </summary>
    [NotMapped]
    public class District
    {
        /// <summary>
        /// İlçe ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// İlçe adı
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Bağlı olduğu şehir ID
        /// </summary>
        public int CityId { get; set; }

        /// <summary>
        /// Bu ilçeye ait mahalleler
        /// </summary>
        public List<Neighborhood> Neighborhoods { get; set; } = new List<Neighborhood>();
    }
}
