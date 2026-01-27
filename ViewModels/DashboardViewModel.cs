using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Dashboard ViewModel - Komut Merkezi: Kritik uyarılar, günlük işler ve finansal özet
    /// </summary>
    public class DashboardViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;

        #region Display Properties

        /// <summary>
        /// Kullanıcı karşılama metni
        /// </summary>
        public string WelcomeMessage => $"Hoşgeldin, {Services.AuthService.CurrentUser?.AdSoyad ?? "Kullanıcı"}";

        /// <summary>
        /// Bugünün tarihi (Türkçe format)
        /// </summary>
        public string TodayDate => DateTime.Now.ToString("dd MMMM yyyy, dddd", new System.Globalization.CultureInfo("tr-TR"));

        /// <summary>
        /// Mevcut ay adı
        /// </summary>
        public string CurrentMonthName => DateTime.Now.ToString("MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));

        #endregion

        #region Widget 1: Kritik Uyarılar (Stok & Bakım)

        /// <summary>
        /// Düşük stoklu ürünler (Quantity <= 5)
        /// </summary>
        public ObservableCollection<LowStockItem> LowStockProducts { get; set; } = new();

        private int _lowStockCount;
        /// <summary>
        /// Düşük stok uyarısı sayısı
        /// </summary>
        public int LowStockCount
        {
            get => _lowStockCount;
            set => SetProperty(ref _lowStockCount, value);
        }

        #endregion

        #region Widget 2: Bugünün İşleri (Arıza & Saha)

        /// <summary>
        /// Bugün planlanan işler
        /// </summary>
        public ObservableCollection<TodayJobItem> TodaysJobs { get; set; } = new();

        /// <summary>
        /// Teslime hazır tamirler
        /// </summary>
        public ObservableCollection<ReadyRepairItem> ReadyToDeliverRepairs { get; set; } = new();

        private int _todaysJobsCount;
        /// <summary>
        /// Bugünün iş sayısı
        /// </summary>
        public int TodaysJobsCount
        {
            get => _todaysJobsCount;
            set => SetProperty(ref _todaysJobsCount, value);
        }

        private int _readyRepairsCount;
        /// <summary>
        /// Teslime hazır tamir sayısı
        /// </summary>
        public int ReadyRepairsCount
        {
            get => _readyRepairsCount;
            set => SetProperty(ref _readyRepairsCount, value);
        }

        #endregion

        #region Widget 3: Aylık Özet (Finans)

        private decimal _monthlySalesTotal;
        /// <summary>
        /// Bu ay toplam satış
        /// </summary>
        public decimal MonthlySalesTotal
        {
            get => _monthlySalesTotal;
            set => SetProperty(ref _monthlySalesTotal, value);
        }

        private int _monthlySalesCount;
        /// <summary>
        /// Bu ay satış sayısı
        /// </summary>
        public int MonthlySalesCount
        {
            get => _monthlySalesCount;
            set => SetProperty(ref _monthlySalesCount, value);
        }

        private int _monthlyJobsCompleted;
        /// <summary>
        /// Bu ay tamamlanan iş sayısı
        /// </summary>
        public int MonthlyJobsCompleted
        {
            get => _monthlyJobsCompleted;
            set => SetProperty(ref _monthlyJobsCompleted, value);
        }

        private int _activeJobsCount;
        /// <summary>
        /// Aktif iş sayısı
        /// </summary>
        public int ActiveJobsCount
        {
            get => _activeJobsCount;
            set => SetProperty(ref _activeJobsCount, value);
        }

        private decimal _dailyIncome;
        public decimal DailyIncome
        {
            get => _dailyIncome;
            set => SetProperty(ref _dailyIncome, value);
        }

        private decimal _dailyExpense;
        public decimal DailyExpense
        {
            get => _dailyExpense;
            set => SetProperty(ref _dailyExpense, value);
        }

        #endregion

        #region LiveCharts Properties

        /// <summary>
        /// 7 günlük gelir/gider trend grafiği
        /// </summary>
        public ISeries[] WeeklyTrendSeries { get; set; } = Array.Empty<ISeries>();

        /// <summary>
        /// X ekseni - Günler
        /// </summary>
        public Axis[] WeeklyTrendXAxes { get; set; } = Array.Empty<Axis>();

        /// <summary>
        /// Y ekseni
        /// </summary>
        public Axis[] WeeklyTrendYAxes { get; set; } = Array.Empty<Axis>();

        /// <summary>
        /// İş kategorileri dağılımı (Pie Chart)
        /// </summary>
        public ISeries[] JobCategoryPieSeries { get; set; } = Array.Empty<ISeries>();

        /// <summary>
        /// Teknisyen performans grafiği
        /// </summary>
        public ISeries[] TechnicianPerformanceSeries { get; set; } = Array.Empty<ISeries>();

        /// <summary>
        /// X ekseni - Teknisyenler
        /// </summary>
        public Axis[] TechnicianXAxes { get; set; } = Array.Empty<Axis>();

        #endregion

        #region Commands

        public ICommand RefreshDashboardCommand { get; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public DashboardViewModel()
        {
            _context = new AppDbContext();
            RefreshDashboardCommand = new RelayCommand(_ => LoadDashboardData());
            LoadDashboardData();
        }

        /// <summary>
        /// Tüm dashboard verilerini yükle
        /// </summary>
        private void LoadDashboardData()
        {
            LoadLowStockAlerts();
            LoadTodaysJobs();
            LoadReadyRepairs();
            LoadMonthlyFinancials();
            LoadFinancialSummary();
            LoadChartData();
        }

        private void LoadFinancialSummary()
        {
            try 
            {
                var today = DateTime.Today;

                // REPLACE DailyIncome QUERY WITH explicit Enum checks
                DailyIncome = _context.CashTransactions.Where(t => t.Date >= today && 
                    (t.TransactionType == CashTransactionType.CashIncome || 
                     t.TransactionType == CashTransactionType.CardIncome || 
                     t.TransactionType == CashTransactionType.TransferIncome))
                     .Select(t => t.Amount)
                     .AsEnumerable()
                     .Sum();

                // REPLACE DailyExpense QUERY WITH explicit Enum checks
                DailyExpense = _context.CashTransactions.Where(t => t.Date >= today && 
                    (t.TransactionType == CashTransactionType.Expense || 
                     t.TransactionType == CashTransactionType.TransferExpense))
                     .Select(t => t.Amount)
                     .AsEnumerable()
                     .Sum();
            }
            catch (Exception ex)
            {
                // Silent fail or log
                System.Diagnostics.Debug.WriteLine($"Dashboard Finance Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Widget 1: Düşük stok uyarılarını yükle
        /// </summary>
        private void LoadLowStockAlerts()
        {
            var lowStockThreshold = 5;
            var lowStocks = _context.Products
                .Where(p => p.TotalStockQuantity <= lowStockThreshold && p.TotalStockQuantity >= 0)
                .OrderBy(p => p.TotalStockQuantity)
                .Take(10)
                .Select(p => new LowStockItem
                {
                    ProductId = p.Id,
                    ProductName = p.ProductName ?? "Bilinmeyen Ürün",
                    CurrentStock = p.TotalStockQuantity,
                    MinStockLevel = p.MinStockLevel,
                    UrgencyLevel = p.TotalStockQuantity == 0 ? "Kritik" : 
                                   p.TotalStockQuantity <= 2 ? "Çok Düşük" : "Düşük"
                })
                .ToList();

            LowStockProducts.Clear();
            foreach (var item in lowStocks)
            {
                LowStockProducts.Add(item);
            }
            LowStockCount = _context.Products.Count(p => p.TotalStockQuantity <= lowStockThreshold);
        }

        /// <summary>
        /// Widget 2: Bugünün işlerini yükle
        /// </summary>
        private void LoadTodaysJobs()
        {
            var today = DateTime.Today;
            var todaysJobs = _context.ServiceJobs
                .Include(j => j.Customer)
                .Where(j => j.ScheduledDate.HasValue && 
                           j.ScheduledDate.Value.Date == today &&
                           j.Status != JobStatus.Completed)
                .OrderBy(j => j.ScheduledDate)
                .Take(10)
                .ToList()
                .Select(j => new TodayJobItem
                {
                    JobId = j.Id,
                    CustomerName = j.Customer?.FullName ?? "Bilinmeyen Müşteri",
                    Category = GetCategoryIcon(j.JobCategory) + " " + GetCategoryName(j.JobCategory),
                    ScheduledTime = j.ScheduledDate?.ToString("HH:mm") ?? "--:--",
                    Priority = j.Priority.ToString(),
                    Address = j.Customer?.FullAddress ?? ""
                })
                .ToList();

            TodaysJobs.Clear();
            foreach (var job in todaysJobs)
            {
                TodaysJobs.Add(job);
            }
            TodaysJobsCount = _context.ServiceJobs.Count(j => 
                j.ScheduledDate.HasValue && 
                j.ScheduledDate.Value.Date == today &&
                j.Status != JobStatus.Completed);
        }

        /// <summary>
        /// Widget 2b: Teslime hazır tamirleri yükle
        /// </summary>
        private void LoadReadyRepairs()
        {
            var readyRepairs = _context.ServiceJobs
                .Include(j => j.Customer)
                .Where(j => j.WorkOrderType == WorkOrderType.Repair && 
                           j.RepairStatus == RepairStatus.ReadyForPickup)
                .OrderBy(j => j.CreatedDate)
                .Take(10)
                .ToList()
                .Select(j => new ReadyRepairItem
                {
                    JobId = j.Id,
                    TicketNo = $"T-{j.Id}",
                    CustomerName = j.Customer?.FullName ?? "Bilinmeyen Müşteri",
                    DeviceInfo = $"{j.DeviceBrand} {j.DeviceModel}",
                    DaysWaiting = (DateTime.Now - (j.CompletedDate ?? j.CreatedDate)).Days,
                    CustomerPhone = j.Customer?.PhoneNumber ?? ""
                })
                .ToList();

            ReadyToDeliverRepairs.Clear();
            foreach (var repair in readyRepairs)
            {
                ReadyToDeliverRepairs.Add(repair);
            }
            ReadyRepairsCount = _context.ServiceJobs.Count(j => 
                j.WorkOrderType == WorkOrderType.Repair && 
                j.RepairStatus == RepairStatus.ReadyForPickup);
        }

        /// <summary>
        /// Widget 3: Aylık finansal özeti yükle
        /// </summary>
        private void LoadMonthlyFinancials()
        {
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1);

            // Bu ay satış toplamı
            // Bu ay satış toplamı
            var salesAmounts = _context.SalesOrders
                .Where(o => o.Date >= startOfMonth && o.Date < endOfMonth)
                .Select(o => o.TotalAmount)
                .ToList();

            MonthlySalesTotal = salesAmounts.Sum();

            MonthlySalesCount = salesAmounts.Count;

            // Bu ay tamamlanan işler
            MonthlyJobsCompleted = _context.ServiceJobs
                .Count(j => j.CompletedDate.HasValue && 
                           j.CompletedDate.Value >= startOfMonth && 
                           j.CompletedDate.Value < endOfMonth);

            // Aktif işler
            ActiveJobsCount = _context.ServiceJobs
                .Count(j => j.Status != JobStatus.Completed);
        }

        #region Helper Methods

        private string GetCategoryIcon(JobCategory category)
        {
            return category switch
            {
                JobCategory.CCTV => "📹",
                JobCategory.VideoIntercom => "📞",
                JobCategory.FireAlarm => "🔥",
                JobCategory.BurglarAlarm => "🚨",
                JobCategory.SmartHome => "🏠",
                JobCategory.AccessControl => "🔐",
                JobCategory.SatelliteSystem => "📡",
                JobCategory.FiberOptic => "🔌",
                _ => "🔧"
            };
        }

        private string GetCategoryName(JobCategory category)
        {
            return category switch
            {
                JobCategory.CCTV => "CCTV",
                JobCategory.VideoIntercom => "Diafon",
                JobCategory.FireAlarm => "Yangın",
                JobCategory.BurglarAlarm => "Alarm",
                JobCategory.SmartHome => "Akıllı Ev",
                JobCategory.AccessControl => "PDKS",
                JobCategory.SatelliteSystem => "Uydu",
                JobCategory.FiberOptic => "Fiber",
                _ => "Diğer"
            };
        }

        /// <summary>
        /// Grafik verilerini yükle
        /// </summary>
        private void LoadChartData()
        {
            try
            {
                LoadWeeklyTrendChart();
                LoadJobCategoryPieChart();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chart loading error: {ex.Message}");
            }
        }

        /// <summary>
        /// 7 günlük gelir trend grafiği
        /// </summary>
        private void LoadWeeklyTrendChart()
        {
            var labels = new List<string>();
            var incomeData = new List<double>();
            var jobsData = new List<double>();

            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                labels.Add(date.ToString("ddd", new System.Globalization.CultureInfo("tr-TR")));

                // Günlük gelir
                var dailyIncome = _context.CashTransactions
                    .Where(t => t.Date.Date == date.Date && 
                        (t.TransactionType == CashTransactionType.CashIncome || 
                         t.TransactionType == CashTransactionType.CardIncome || 
                         t.TransactionType == CashTransactionType.TransferIncome))
                    .Select(t => (double)t.Amount)
                    .AsEnumerable()
                    .Sum();
                incomeData.Add(dailyIncome);

                // Günlük tamamlanan iş sayısı
                var dailyJobs = _context.ServiceJobs
                    .Count(j => j.CompletedDate.HasValue && j.CompletedDate.Value.Date == date.Date);
                jobsData.Add(dailyJobs);
            }

            WeeklyTrendSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = incomeData,
                    Name = "Gelir (₺)",
                    Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 3 },
                    Fill = new LinearGradientPaint(
                        new[] { SKColors.DodgerBlue.WithAlpha(100), SKColors.DodgerBlue.WithAlpha(20) },
                        new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                    GeometrySize = 10,
                    GeometryStroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 },
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    LineSmoothness = 0.7
                },
                new ColumnSeries<double>
                {
                    Values = jobsData,
                    Name = "Tamamlanan İş",
                    Fill = new SolidColorPaint(SKColors.MediumSeaGreen.WithAlpha(180)),
                    MaxBarWidth = 20,
                    Rx = 4,
                    Ry = 4
                }
            };

            WeeklyTrendXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = labels,
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    TextSize = 11
                }
            };

            WeeklyTrendYAxes = new Axis[]
            {
                new Axis
                {
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    TextSize = 11,
                    Labeler = value => value.ToString("N0")
                }
            };

            OnPropertyChanged(nameof(WeeklyTrendSeries));
            OnPropertyChanged(nameof(WeeklyTrendXAxes));
            OnPropertyChanged(nameof(WeeklyTrendYAxes));
        }

        /// <summary>
        /// İş kategorileri dağılım grafiği
        /// </summary>
        private void LoadJobCategoryPieChart()
        {
            var categoryData = _context.ServiceJobs
                .Where(j => j.Status != JobStatus.Completed)
                .GroupBy(j => j.JobCategory)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToList();

            if (categoryData.Count == 0)
            {
                JobCategoryPieSeries = Array.Empty<ISeries>();
                OnPropertyChanged(nameof(JobCategoryPieSeries));
                return;
            }

            var colors = new SKColor[]
            {
                SKColors.DodgerBlue,
                SKColors.Orange,
                SKColors.MediumSeaGreen,
                SKColors.Tomato,
                SKColors.MediumPurple,
                SKColors.Gold,
                SKColors.DeepPink,
                SKColors.Teal
            };

            var series = new List<ISeries>();
            int colorIndex = 0;
            foreach (var item in categoryData)
            {
                series.Add(new PieSeries<int>
                {
                    Values = new[] { item.Count },
                    Name = GetCategoryName(item.Category),
                    Fill = new SolidColorPaint(colors[colorIndex % colors.Length]),
                    Pushout = 2
                });
                colorIndex++;
            }

            JobCategoryPieSeries = series.ToArray();
            OnPropertyChanged(nameof(JobCategoryPieSeries));
        }

        #endregion
    }

    #region Display Models

    /// <summary>
    /// Düşük stok ürün görüntüleme modeli
    /// </summary>
    public class LowStockItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int MinStockLevel { get; set; }
        public string UrgencyLevel { get; set; } = string.Empty;
    }

    /// <summary>
    /// Bugünün işleri görüntüleme modeli
    /// </summary>
    public class TodayJobItem
    {
        public int JobId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ScheduledTime { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    /// <summary>
    /// Teslime hazır tamir görüntüleme modeli
    /// </summary>
    public class ReadyRepairItem
    {
        public int JobId { get; set; }
        public string TicketNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string DeviceInfo { get; set; } = string.Empty;
        public int DaysWaiting { get; set; }
        public string CustomerPhone { get; set; } = string.Empty;
    }

    #endregion
}
