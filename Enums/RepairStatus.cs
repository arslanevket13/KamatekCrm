using System.ComponentModel;

namespace KamatekCrm.Enums
{
    /// <summary>
    /// Tamir durum yaşam döngüsü
    /// </summary>
    public enum RepairStatus
    {
        [Description("Kayıt Açıldı")]
        Registered = 0,

        [Description("Arıza Tespiti")]
        Diagnosing = 10,

        [Description("Yedek Parça Bekleniyor")]
        WaitingForParts = 20,

        [Description("Fabrikaya Gönderildi")]
        SentToFactory = 30,

        [Description("Fabrikadan Geldi")]
        ReturnedFromFactory = 35,

        [Description("Tamir İşlemi Sürüyor")]
        InRepair = 40,

        [Description("Test Aşamasında")]
        Testing = 50,

        [Description("Teslimata Hazır")]
        ReadyForPickup = 60,

        [Description("Teslim Edildi")]
        Delivered = 70,

        [Description("İade/Hurda")]
        Unrepairable = 99
    }
}
