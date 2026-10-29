using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using KamatekCrm.Diagnostics.Core;

namespace KamatekCrm.ViewModels.DevCenter
{
    public sealed record ErrorGroup(
        string Key,
        int Count,
        string SampleMessage,
        DateTimeOffset LastSeen)
    {
        public string Summary => $"{Count}x · {Key}";
        public string LastSeenText => LastSeen.LocalDateTime.ToString("HH:mm:ss");
    }

    /// <summary>Hata Merkezi: tampondaki Error/Fatal olaylarını tipe/koda göre gruplar.</summary>
    public sealed class ErrorCenterTabViewModel : DevCenterTabViewModel
    {
        private readonly DiagnosticDataService _data;
        private string _statusText = "Henüz taranmadı";

        public ErrorCenterTabViewModel(DiagnosticDataService data)
            : base("Hata Merkezi", "🚨")
        {
            _data = data;
        }

        public ObservableCollection<ErrorGroup> Groups { get; } = new();

        public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

        public override async Task RefreshAsync()
        {
            var events = await Task.Run(() => _data.Query(new DiagnosticQuery
            {
                MinimumLevel = DiagnosticLevel.Error,
                Take = 500
            }));

            Groups.Clear();

            var grouped = events
                .GroupBy(e => string.IsNullOrWhiteSpace(e.ExceptionType)
                    ? (string.IsNullOrWhiteSpace(e.EventCode) ? e.Module : e.EventCode!)
                    : e.ExceptionType!)
                .OrderByDescending(group => group.Count());

            foreach (var group in grouped)
            {
                Groups.Add(new ErrorGroup(
                    group.Key,
                    group.Count(),
                    group.First().Message,
                    group.Max(item => item.Timestamp)));
            }

            StatusText = Groups.Count == 0
                ? "Kayıtlı hata yok"
                : $"{Groups.Count} hata grubu · {events.Count} olay";
        }
    }
}
