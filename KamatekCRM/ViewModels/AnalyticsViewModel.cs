using KamatekCrm.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace KamatekCrm.ViewModels
{
    public partial class AnalyticsViewModel : ViewModelBase
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly PdfService? _pdfService;
        private readonly IDialogService? _dialogService;
        private readonly IToastService? _toastService;

        private string _selectedPeriod = "Month";
        private List<(string Category, int Count)> _lastCategoryBreakdown = new();
        private List<(string Technician, int CompletedCount)> _lastTechnicianStats = new();

        public AnalyticsViewModel(
            IDbContextFactory<AppDbContext> contextFactory,
            PdfService? pdfService = null,
            IDialogService? dialogService = null,
            IToastService? toastService = null)
        {
            _contextFactory = contextFactory;
            _pdfService = pdfService;
            _dialogService = dialogService;
            _toastService = toastService;
            _jobDistributionSeries = Array.Empty<ISeries>();
            _techPerformanceSeries = Array.Empty<ISeries>();
            _techXAxes = Array.Empty<Axis>();
            _trendSeries = Array.Empty<ISeries>();
            _trendXAxes = Array.Empty<Axis>();

            RefreshAsync().SafeFireAndForget("AnalyticsViewModel.RefreshAsync");
        }

        public string SelectedPeriod
        {
            get => _selectedPeriod;
            private set
            {
                if (SetProperty(ref _selectedPeriod, value))
                {
                    OnPropertyChanged(nameof(IsWeekSelected));
                    OnPropertyChanged(nameof(IsMonthSelected));
                    OnPropertyChanged(nameof(IsYearSelected));
                    OnPropertyChanged(nameof(SelectedPeriodDisplay));
                }
            }
        }

        public bool IsWeekSelected => SelectedPeriod == "Week";
        public bool IsMonthSelected => SelectedPeriod == "Month";
        public bool IsYearSelected => SelectedPeriod == "Year";

        public string SelectedPeriodDisplay => SelectedPeriod switch
        {
            "Week" => "Bu Hafta (Son 7 Gün)",
            "Year" => "Bu Yıl",
            _ => "Bu Ay"
        };

        [RelayCommand]
        private async Task SetPeriodAsync(string? period)
        {
            var normalized = period switch
            {
                "Week" => "Week",
                "Year" => "Year",
                _ => "Month"
            };

            if (SelectedPeriod == normalized) return;
            SelectedPeriod = normalized;
            await RefreshAsync();
        }

        [RelayCommand]
        private async Task ExportPdfAsync()
        {
            if (_pdfService == null || _dialogService == null)
            {
                _toastService?.ShowError("Hata", "PDF dışa aktarma servisi kullanılamıyor.");
                return;
            }

            try
            {
                string defaultName = $"AnalitikRapor_{SelectedPeriod}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                string? filePath = await _dialogService.ShowSaveFileDialogAsync(
                    "Analitik Raporu PDF Olarak Kaydet",
                    "PDF Dosyası (*.pdf)|*.pdf",
                    defaultName);

                if (string.IsNullOrWhiteSpace(filePath)) return;

                _pdfService.GenerateAnalyticsReport(
                    SelectedPeriodDisplay,
                    TotalJobs,
                    CompletedJobs,
                    SuccessRate,
                    _lastCategoryBreakdown,
                    _lastTechnicianStats,
                    filePath);

                _toastService?.ShowSuccess("PDF Raporu Hazır", $"Rapor kaydedildi: {System.IO.Path.GetFileName(filePath)}");

                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath)
                    {
                        UseShellExecute = true
                    });
                }
                catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "AnalyticsViewModel.cs"); }
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("PDF Oluşturma Hatası", ex.Message);
            }
        }

        #region Charts Properties

        private ISeries[] _jobDistributionSeries;
        public ISeries[] JobDistributionSeries
        {
            get => _jobDistributionSeries;
            set => SetProperty(ref _jobDistributionSeries, value);
        }

        private ISeries[] _techPerformanceSeries;
        public ISeries[] TechPerformanceSeries
        {
            get => _techPerformanceSeries;
            set => SetProperty(ref _techPerformanceSeries, value);
        }

        private Axis[] _techXAxes;
        public Axis[] TechXAxes
        {
            get => _techXAxes;
            set => SetProperty(ref _techXAxes, value);
        }

        private ISeries[] _trendSeries;
        public ISeries[] TrendSeries
        {
            get => _trendSeries;
            set => SetProperty(ref _trendSeries, value);
        }

        private Axis[] _trendXAxes;
        public Axis[] TrendXAxes
        {
            get => _trendXAxes;
            set => SetProperty(ref _trendXAxes, value);
        }

        #endregion

        #region KPI Properties

        private int _totalJobs;
        public int TotalJobs { get => _totalJobs; set => SetProperty(ref _totalJobs, value); }

        private int _completedJobs;
        public int CompletedJobs { get => _completedJobs; set => SetProperty(ref _completedJobs, value); }

        private double _successRate;
        public double SuccessRate { get => _successRate; set => SetProperty(ref _successRate, value); }

        #endregion

        private DateTime GetPeriodStartUtc()
        {
            var now = DateTime.UtcNow;
            return SelectedPeriod switch
            {
                "Week" => DateTime.SpecifyKind(now.Date.AddDays(-7), DateTimeKind.Utc),
                "Year" => new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                _ => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            };
        }

        private async Task RefreshAsync()
        {
            try
            {
                var periodStartUtc = GetPeriodStartUtc();
                await using var context = await _contextFactory.CreateDbContextAsync();

                var allJobs = await context.ServiceJobs
                    .AsNoTracking()
                    .Include(j => j.AssignedUser)
                    .Where(j => j.CreatedDate >= periodStartUtc)
                    .ToListAsync();

                // --- KPI Hesaplama ---
                TotalJobs = allJobs.Count;
                CompletedJobs = allJobs.Count(j => j.Status == JobStatus.Completed);
                SuccessRate = TotalJobs > 0 ? (double)CompletedJobs / TotalJobs * 100 : 0;

                // --- 1. İş Türü Dağılımı (Pie Chart) ---
                var jobTypes = allJobs
                    .GroupBy(j => j.JobCategory)
                    .Select(g => new { Category = g.Key.ToString(), Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                _lastCategoryBreakdown = jobTypes.Select(x => (x.Category, x.Count)).ToList();

                if (jobTypes.Any())
                {
                    JobDistributionSeries = jobTypes.Select(x => new PieSeries<int>
                    {
                        Values = new int[] { x.Count },
                        Name = x.Category,
                        InnerRadius = 50,
                        DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({point.StackedValue?.Share:P1})"
                    }).ToArray();
                }
                else
                {
                    JobDistributionSeries = new ISeries[]
                    {
                        new PieSeries<int> { Values = new[] { 1 }, Name = "Veri Yok", Fill = new SolidColorPaint(SKColors.LightGray) }
                    };
                }

                // --- 2. Teknisyen Performansı (Column Chart) ---
                var techStats = allJobs
                    .Where(j => !string.IsNullOrEmpty(j.AssignedTechnician))
                    .GroupBy(j => j.AssignedTechnician!)
                    .Select(g => new { Tech = g.Key, Completed = g.Count(j => j.Status == JobStatus.Completed) })
                    .OrderByDescending(x => x.Completed)
                    .Take(5)
                    .ToList();

                _lastTechnicianStats = techStats.Select(x => (x.Tech, x.Completed)).ToList();

                if (techStats.Any())
                {
                    TechPerformanceSeries = new ISeries[]
                    {
                        new ColumnSeries<int>
                        {
                            Values = techStats.Select(x => x.Completed).ToArray(),
                            Name = "Tamamlanan İşler",
                            Fill = new SolidColorPaint(SKColors.CornflowerBlue)
                        }
                    };

                    TechXAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labels = techStats.Select(x => x.Tech).ToList(),
                            LabelsRotation = 0
                        }
                    };
                }
                else
                {
                    TechPerformanceSeries = Array.Empty<ISeries>();
                    TechXAxes = Array.Empty<Axis>();
                }

                // --- 3. Trend Grafiği (Son 6 Ay veya Seçili Dönem) ---
                var trendStartUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddMonths(-5), DateTimeKind.Utc);
                var sixMonthJobs = await context.ServiceJobs
                    .AsNoTracking()
                    .Where(j => j.CreatedDate >= new DateTime(trendStartUtc.Year, trendStartUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc))
                    .Select(j => j.CreatedDate)
                    .ToListAsync();

                var last6Months = Enumerable.Range(0, 6)
                    .Select(i => DateTime.UtcNow.AddMonths(-i))
                    .OrderBy(d => d)
                    .ToList();

                var trendValues = new List<int>();
                var monthLabels = new List<string>();

                foreach (var date in last6Months)
                {
                    var count = sixMonthJobs.Count(d => d.Month == date.Month && d.Year == date.Year);
                    trendValues.Add(count);
                    monthLabels.Add(date.ToString("MMM"));
                }

                TrendSeries = new ISeries[]
                {
                    new LineSeries<int>
                    {
                        Values = trendValues.ToArray(),
                        Name = "Aylık Servis Kaydı",
                        Fill = new SolidColorPaint(SKColors.LightBlue.WithAlpha(100)),
                        Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 3 },
                        GeometrySize = 10,
                        LineSmoothness = 1
                    }
                };

                TrendXAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = monthLabels.ToArray()
                    }
                };
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Analitik Yükleme Hatası", ex.Message);
            }
        }
    }
}
