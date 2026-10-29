using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace KamatekCrm.ViewModels.DevCenter
{
    /// <summary>
    /// Süreç düzeyinde anlık performans göstergeleri.
    /// <para>
    /// Uzun süren işlem/DB/API ölçümleri ve UI thread donma tespiti Aşama 6'da eklenecek;
    /// bu sekme şimdilik yalnızca gerçekten ölçülebilen değerleri gösterir.
    /// </para>
    /// </summary>
    public sealed class PerformanceTabViewModel : DevCenterTabViewModel
    {
        private string _workingSetText = "-";
        private string _privateMemoryText = "-";
        private string _threadCountText = "-";
        private string _gcMemoryText = "-";
        private string _uptimeText = "-";
        private string _lastRefreshedText = "Henüz yenilenmedi";

        public PerformanceTabViewModel()
            : base("Performans", "⚡")
        {
        }

        public string WorkingSetText { get => _workingSetText; private set => SetProperty(ref _workingSetText, value); }
        public string PrivateMemoryText { get => _privateMemoryText; private set => SetProperty(ref _privateMemoryText, value); }
        public string ThreadCountText { get => _threadCountText; private set => SetProperty(ref _threadCountText, value); }
        public string GcMemoryText { get => _gcMemoryText; private set => SetProperty(ref _gcMemoryText, value); }
        public string UptimeText { get => _uptimeText; private set => SetProperty(ref _uptimeText, value); }
        public string LastRefreshedText { get => _lastRefreshedText; private set => SetProperty(ref _lastRefreshedText, value); }

        public string ScopeNote =>
            "Şu an süreç düzeyi ölçümler gösteriliyor. İşlem süreleri, DB sorgu süreleri, " +
            "API gecikmeleri ve UI donma eşiği Aşama 6'da korelasyon kimliğiyle eklenecek.";

        public override async Task RefreshAsync()
        {
            var metrics = await Task.Run(() =>
            {
                using var process = Process.GetCurrentProcess();
                return (
                    WorkingSet: process.WorkingSet64,
                    PrivateMemory: process.PrivateMemorySize64,
                    Threads: process.Threads.Count,
                    GcMemory: GC.GetTotalMemory(forceFullCollection: false),
                    Uptime: DateTime.Now - process.StartTime);
            });

            WorkingSetText = FormatMegabytes(metrics.WorkingSet);
            PrivateMemoryText = FormatMegabytes(metrics.PrivateMemory);
            ThreadCountText = metrics.Threads.ToString();
            GcMemoryText = FormatMegabytes(metrics.GcMemory);
            UptimeText = $"{metrics.Uptime.TotalMinutes:N0} dk";
            LastRefreshedText = $"Son güncelleme: {DateTime.Now:HH:mm:ss}";
        }

        private static string FormatMegabytes(long bytes) => $"{bytes / (1024d * 1024d):N1} MB";
    }
}
