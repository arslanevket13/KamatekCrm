using System.ComponentModel;

namespace KamatekCrm.Enums
{
    /// <summary>
    /// Servis işi kategorilerini tanımlar
    /// </summary>
    public enum JobCategory
    {
        [Description("Güvenlik Kamera (CCTV)")]
        CCTV = 0,

        [Description("Görüntülü Diafon")]
        VideoIntercom = 1,

        [Description("Yangın Alarm")]
        FireAlarm = 2,

        [Description("Hırsız Alarm")]
        BurglarAlarm = 3,

        [Description("Akıllı Ev")]
        SmartHome = 4,

        [Description("Kartlı Geçiş (PDKS)")]
        AccessControl = 5,

        [Description("Uydu Sistemleri")]
        SatelliteSystem = 6,

        [Description("Fiber Optik")]
        FiberOptic = 7,

        [Description("Diğer / Genel")]
        Other = 99
    }
}
