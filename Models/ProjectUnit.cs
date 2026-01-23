using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Proje birimi - Keşif aşamasında oluşturulan birimler (in-memory, DB'de saklanmaz)
    /// Örn: Blok A > Daire 1, Üretim Alanı, Bahçe
    /// </summary>
    [NotMapped]
    public class ProjectUnit
    {
        /// <summary>
        /// Benzersiz tanımlayıcı (Örn: "A-01", "PROD-1")
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Görüntüleme adı (Örn: "Blok A - Daire 1", "Üretim Alanı")
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Üst birim ID (Hiyerarşi için - Blok ID)
        /// </summary>
        public string? ParentId { get; set; }

        /// <summary>
        /// Birim tipi
        /// </summary>
        public UnitType UnitType { get; set; }

        /// <summary>
        /// Sıralama numarası
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Seçili mi? (UI'da checkbox için)
        /// </summary>
        public bool IsSelected { get; set; } = true;

        /// <summary>
        /// Bu birime atanmış ürünler
        /// </summary>
        public List<QuoteLineItem> AssignedItems { get; set; } = new();

        /// <summary>
        /// Kat numarası (Apartman/Site için)
        /// </summary>
        public int? Floor { get; set; }

        /// <summary>
        /// Önceden tanımlı bölge tipi (Fabrika için)
        /// </summary>
        public PredefinedZone? PredefinedZoneType { get; set; }

        /// <summary>
        /// Özel bölge adı (PredefinedZone.Custom için)
        /// </summary>
        public string? CustomZoneName { get; set; }
    }
}
