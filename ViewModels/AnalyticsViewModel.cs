using System;
using System.Collections.ObjectModel;
using System.Linq;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    public class AnalyticsViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;

        public AnalyticsViewModel(AppDbContext context)
        {
            _context = context;
            // Initialize non-nullable fields
            _jobDistributionSeries = Array.Empty<ISeries>();
            _techPerformanceSeries = Array.Empty<ISeries>();
            _techXAxes = Array.Empty<Axis>();
            _trendSeries = Array.Empty<ISeries>();
            _trendXAxes = Array.Empty<Axis>();
            
            LoadData();
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

        private void LoadData()
        {
            // Verileri çek (Include ile ilişkili tabloları almayı unutma gerekirse)
            var allJobs = _context.ServiceJobs.Include(j => j.AssignedUser).ToList();

            // --- KPI Hesaplama ---
            TotalJobs = allJobs.Count;
            CompletedJobs = allJobs.Count(j => j.Status == JobStatus.Completed);
            SuccessRate = TotalJobs > 0 ? (double)CompletedJobs / TotalJobs * 100 : 0;

            // --- 1. İş Türü Dağılımı (Pie Chart) ---
            var jobTypes = allJobs
                .GroupBy(j => j.JobCategory)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToList();

            // Eğer veri yoksa boş grafik yerine örnek veri gösterilebilir veya boş bırakılabilir
            if (jobTypes.Any())
            {
                JobDistributionSeries = jobTypes.Select(x => new PieSeries<int>
                {
                    Values = new int[] { x.Count },
                    Name = x.Category.ToString(),
                    InnerRadius = 50,
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    // Use Coordinate.PrimaryValue instead of PrimaryValue (deprecated)
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({point.StackedValue?.Share:P1})"
                }).ToArray();
            }
            else
            {
                // Boş veri durumu için placeholder
                 JobDistributionSeries = new ISeries[] { 
                    new PieSeries<int> { Values = new[] { 1 }, Name = "Veri Yok", Fill = new SolidColorPaint(SKColors.LightGray) } 
                 };
            }


            // --- 2. Teknisyen Performansı (Column Chart) ---
            var techStats = allJobs
                .Where(j => !string.IsNullOrEmpty(j.AssignedTechnician)) // AssignedTechnician string property
                .GroupBy(j => j.AssignedTechnician)
                .Select(g => new { Tech = g.Key!, Completed = g.Count(j => j.Status == JobStatus.Completed) }) // Key is not null here due to Where clause
                .OrderByDescending(x => x.Completed)
                .Take(5) // Top 5
                .ToList();

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
                        // Ensure it is IList<string> by creating a List explicitly
                        Labels = techStats.Select(x => x.Tech).ToList(), 
                        LabelsRotation = 0
                    }
                };
            }


            // --- 3. Son 6 Ay Arıza Trendi (Line Chart) ---
            var last6Months = Enumerable.Range(0, 6)
                .Select(i => DateTime.Now.AddMonths(-i))
                .OrderBy(d => d)
                .ToList();

            var trendValues = new List<int>();
            var monthLabels = new List<string>();

            foreach (var date in last6Months)
            {
                var count = allJobs.Count(j => j.CreatedDate.Month == date.Month && j.CreatedDate.Year == date.Year);
                trendValues.Add(count);
                monthLabels.Add(date.ToString("MMM"));
            }

            TrendSeries = new ISeries[]
            {
                new LineSeries<int>
                {
                    Values = trendValues.ToArray(),
                    Name = "Aylık Arıza Kaydı",
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
    }
}
