using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Yapı tanımı - ServiceProject.StructureDefinitionJson'a serialize edilir
    /// </summary>
    [NotMapped]
    public class StructureDefinition
    {
        /// <summary>
        /// Yapı tipi
        /// </summary>
        public StructureType Type { get; set; } = StructureType.SingleUnit;

        #region Apartman Özellikleri

        /// <summary>
        /// Kat sayısı
        /// </summary>
        public int FloorCount { get; set; } = 1;

        /// <summary>
        /// Her kattaki daire sayısı
        /// </summary>
        public int UnitsPerFloor { get; set; } = 1;

        /// <summary>
        /// Zemin kat dahil mi?
        /// </summary>
        public bool IncludeGroundFloor { get; set; } = true;

        /// <summary>
        /// Çatı katı dahil mi?
        /// </summary>
        public bool IncludeRooftop { get; set; } = false;

        /// <summary>
        /// Bodrum kat sayısı
        /// </summary>
        public int BasementFloorCount { get; set; } = 0;

        #endregion

        #region Site Özellikleri

        /// <summary>
        /// Blok sayısı (Site için)
        /// </summary>
        public int BlockCount { get; set; } = 1;

        /// <summary>
        /// Blok isimleri (Manuel girilir)
        /// Örn: ["A Blok", "B Blok", "C Blok"]
        /// </summary>
        public List<string> BlockNames { get; set; } = new();

        /// <summary>
        /// Her bloktaki kat sayısı
        /// </summary>
        public int FloorsPerBlock { get; set; } = 1;

        /// <summary>
        /// Her kattaki daire sayısı (tüm bloklar için)
        /// </summary>
        public int UnitsPerFloorPerBlock { get; set; } = 1;

        #endregion

        #region Ticari/Fabrika Özellikleri

        /// <summary>
        /// Seçilen önceden tanımlı bölgeler
        /// </summary>
        public List<PredefinedZone> SelectedZones { get; set; } = new();

        /// <summary>
        /// Her bölge için adet (Örn: 3 adet Depo)
        /// Key: PredefinedZone, Value: Adet
        /// </summary>
        public Dictionary<PredefinedZone, int> ZoneCounts { get; set; } = new();

        /// <summary>
        /// Özel bölge isimleri (PredefinedZone.Custom için)
        /// </summary>
        public List<string> CustomZoneNames { get; set; } = new();

        #endregion

        #region Ortak Alanlar

        /// <summary>
        /// Bina girişi dahil mi?
        /// </summary>
        public bool IncludeEntrance { get; set; } = true;

        /// <summary>
        /// Bahçe/Dış alan dahil mi?
        /// </summary>
        public bool IncludeGarden { get; set; } = false;

        /// <summary>
        /// Otopark dahil mi?
        /// </summary>
        public bool IncludeParking { get; set; } = false;

        #endregion

        /// <summary>
        /// Toplam birim sayısını hesapla
        /// </summary>
        [JsonIgnore]
        public int CalculatedTotalUnits
        {
            get
            {
                return Type switch
                {
                    StructureType.SingleUnit => 1,
                    StructureType.Apartment => CalculateApartmentUnits(),
                    StructureType.Site => CalculateSiteUnits(),
                    StructureType.Commercial => CalculateCommercialUnits(),
                    _ => 1
                };
            }
        }

        private int CalculateApartmentUnits()
        {
            int total = FloorCount * UnitsPerFloor;
            if (IncludeGroundFloor) total += UnitsPerFloor;
            if (IncludeRooftop) total += 1;
            total += BasementFloorCount;
            if (IncludeEntrance) total += 1;
            if (IncludeGarden) total += 1;
            if (IncludeParking) total += 1;
            return total;
        }

        private int CalculateSiteUnits()
        {
            int flatCount = BlockCount * FloorsPerBlock * UnitsPerFloorPerBlock;
            int commonAreas = BlockCount; // Her blok için 1 giriş
            if (IncludeGarden) commonAreas += 1;
            if (IncludeParking) commonAreas += 1;
            return flatCount + commonAreas;
        }

        private int CalculateCommercialUnits()
        {
            int total = 0;
            foreach (var zone in SelectedZones)
            {
                total += ZoneCounts.GetValueOrDefault(zone, 1);
            }
            total += CustomZoneNames.Count;
            return total;
        }
    }
}
