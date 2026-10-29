using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KamatekCrm.Diagnostics.Core;

namespace KamatekCrm.ViewModels.DevCenter
{
    /// <summary>
    /// Sonlandırıcı hatalarda bırakılan JSON izlerini listeler.
    /// Dump/WER toplama bilinçli olarak yok; Aşama 6'da politika ile eklenecek.
    /// </summary>
    public sealed class CrashReportsTabViewModel : DevCenterTabViewModel
    {
        private readonly DiagnosticDataService _data;
        private string _statusText = "Henüz taranmadı";

        public CrashReportsTabViewModel(DiagnosticDataService data)
            : base("Çökme ve Donma", "💥")
        {
            _data = data;
        }

        public ObservableCollection<CrashArtifactInfo> Crashes { get; } = new();

        public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

        public string PolicyNote =>
            "Yalnızca redakte edilmiş JSON izleri toplanır (en fazla 50 dosya). " +
            "Windows Error Reporting / ProcDump tabanlı dump toplama Aşama 6'da, açık kullanıcı onayıyla değerlendirilecek.";

        public override async Task RefreshAsync()
        {
            var artifacts = await Task.Run(() => _data.GetCrashArtifacts());

            Crashes.Clear();
            foreach (var artifact in artifacts)
            {
                Crashes.Add(artifact);
            }

            StatusText = Crashes.Count == 0
                ? "Çökme izi bulunamadı"
                : $"{Crashes.Count} çökme izi";
        }
    }
}
