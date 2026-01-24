using System.ComponentModel;

namespace KamatekCrm.Enums
{
    /// <summary>
    /// Ürün kategorilerini tanımlar (Stok kartı oluştururken kullanılır)
    /// </summary>
    public enum ProductCategory
    {
        [Description("Güvenlik Kamera (CCTV)")]
        Camera = 0,

        [Description("Görüntülü Diafon")]
        Intercom = 1,

        [Description("Yangın Alarm")]
        FireAlarm = 2,

        [Description("Hırsız Alarm")]
        BurglarAlarm = 3,

        [Description("Akıllı Ev")]
        SmartHome = 4,

        [Description("Kartlı Geçiş (PDKS)")]
        AccessControl = 5,

        [Description("Uydu Sistemleri")]
        Satellite = 6,

        [Description("Fiber Optik")]
        FiberOptic = 7,

        [Description("Kablo ve Altyapı")]
        Cable = 8,

        [Description("Diğer / Genel")]
        Other = 99
    }
}
