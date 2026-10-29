namespace KamatekCrm.ViewModels.DevCenter
{
    /// <summary>
    /// Aşama 4/8'de etkinleştirilecek. Şimdilik veri göstermez.
    /// </summary>
    public sealed class AutomatedTestsTabViewModel : DevCenterTabViewModel
    {
        public AutomatedTestsTabViewModel()
            : base("Otomatik Testler", "🧪")
        {
        }

        public override bool IsAvailable => false;

        public override string AvailabilityNote =>
            "Aşama 4/8'de etkinleştirilecek: mevcut xUnit projesinin kontrollü çalıştırılması, " +
            "TRX sonuçlarının ayrıştırılması ve başarısız testlerin hata modeline dönüştürülmesi. " +
            "Şu an testler yalnızca geliştirme ortamında komut satırından çalıştırılıyor.";
    }
}
