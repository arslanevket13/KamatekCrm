namespace KamatekCrm.ViewModels.DevCenter
{
    /// <summary>
    /// Aşama 4'te etkinleştirilecek. Şimdilik veri göstermez; yalnızca kapsamı açıklar.
    /// </summary>
    public sealed class BuildAnalysisTabViewModel : DevCenterTabViewModel
    {
        public BuildAnalysisTabViewModel()
            : base("Derleme & Kod Analizi", "🛠️")
        {
        }

        public override bool IsAvailable => false;

        public override string AvailabilityNote =>
            "Aşama 4'te etkinleştirilecek: izin verilen .NET CLI komutları (allowlist), canlı çıktı, " +
            "iptal/zaman aşımı/çıkış kodu, hata-uyarı modeli ve Roslyn analiz değerlendirmesi. " +
            "Bu ekran hazır olmadan hiçbir derleme komutu çalıştırılmaz.";
    }
}
