using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KamatekCrm.Diagnostics.Core;

namespace KamatekCrm.ViewModels.DevCenter
{
    /// <summary>
    /// Bağlama hatalarını gösterir. Kayıtlar <c>BindingErrorTraceListener</c> üzerinden
    /// yalnızca DEBUG derlemelerinde toplanır; Release'te bu liste boş kalır.
    /// </summary>
    public sealed class BindingIssuesTabViewModel : DevCenterTabViewModel
    {
        private readonly DiagnosticDataService _data;
        private string _statusText = "Henüz taranmadı";

        public BindingIssuesTabViewModel(DiagnosticDataService data)
            : base("XAML / Binding", "🧩")
        {
            _data = data;
        }

        public ObservableCollection<DiagnosticEvent> Issues { get; } = new();

        public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

        public string ScopeNote =>
            "Bağlama hataları yalnızca DEBUG derlemelerinde toplanır (Release'te boş). " +
            "Statik XAML/click-handler analizi Aşama 5'te eklenecek; bu ekran şu an çalışma zamanı verisini gösterir.";

        public override async Task RefreshAsync()
        {
            var events = await Task.Run(() => _data.Query(new DiagnosticQuery
            {
                Module = "WPF.Binding",
                MinimumLevel = DiagnosticLevel.Warning,
                Take = 500
            }));

            Issues.Clear();
            foreach (var item in events)
            {
                Issues.Add(item);
            }

            StatusText = Issues.Count == 0
                ? "Bağlama hatası yok"
                : $"{Issues.Count} bağlama hatası";
        }
    }
}
