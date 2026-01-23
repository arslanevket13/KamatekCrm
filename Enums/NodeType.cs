using System.ComponentModel;

namespace KamatekCrm.Enums
{
    /// <summary>
    /// Yapı ağacı node tipi
    /// </summary>
    public enum NodeType
    {
        [Description("Proje")]
        Project = 0,

        [Description("Blok")]
        Block = 1,

        [Description("Kat")]
        Floor = 2,

        [Description("Daire")]
        Flat = 3,

        [Description("Bölge")]
        Zone = 4,

        [Description("Giriş")]
        Entrance = 5,

        [Description("Bahçe")]
        Garden = 6,

        [Description("Otopark")]
        Parking = 7,

        [Description("Ortak Alan")]
        CommonArea = 8
    }
}
