using System.ComponentModel;

namespace KamatekCrm.Enums
{
    /// <summary>
    /// Bina/İşyeri tipi
    /// </summary>
    public enum BuildingType
    {
        [Description("Müstakil Ev")]
        MustakilEv = 0,

        [Description("Apartman")]
        Apartman = 1,

        [Description("Site")]
        Site = 2,

        [Description("Otel")]
        Otel = 3,

        [Description("Dükkan")]
        Dukkan = 4,

        [Description("İş Yeri")]
        IsYeri = 5,

        [Description("Fabrika")]
        Fabrika = 6,

        [Description("Diğer")]
        Diger = 7
    }
}
