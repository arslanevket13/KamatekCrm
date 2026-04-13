using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Dashboard ViewModel - Komut Merkezi: Kritik uyarılar, günlük işler ve finansal özet
    /// </summary>
    public class DashboardViewModel : ViewModelBase
    {
        private readonly ApiClient _apiClient;
        private readonly IAuthService _authService;
        private readonly ILoadingService _loadingService;
        private readonly IToastService _toastService;

        #region Display Properties

        /// <summary>
        /// Kullanıcı karşılama metni
        /// </summary>
        public string WelcomeMessage => $"Hoşgeldin, {_authService.CurrentUser?.AdSoyad ?? "Kullanıcı"}";

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
        /// Düşük stoklu ürünler
        /// </summary>
        public ObservableCollection<LowStockItemDto> LowStockProducts { get; set; } = new();

        private int _lowStockCount;
        /// <summary>
        /// Düşük stok uyarısı sayısı
        /// </summary>
        public int LowStockCount
        {
            get => _lowStockCount;
            set
            {
                if (SetProperty(ref _lowStockCount, value))
                {
                    OnPropertyChanged(nameof(IsLowStockEmpty));
                }
            }
        }

        public bool IsLowStockEmpty => LowStockCount == 0;

        #endregion

        #region Widget 2: Bugünün İşleri (Arıza & Saha)

        /// <summary>
        /// Bugün planlanan işler
        /// </summary>
        public ObservableCollection<TodayJobItemDto> TodaysJobs { get; set; } = new();

        /// <summary>
        /// Teslime hazır tamirler
        /// </summary>
        public ObservableCollection<ReadyRepairItemDto> ReadyToDeliverRepairs { get; set; } = new();

        private int _todaysJobsCount;
        /// <summary>
        /// Bugünün iş sayısı
        /// </summary>
        public int TodaysJobsCount
        {
            get => _todaysJobsCount;
            set
            {
                if (SetProperty(ref _todaysJobsCount, value))
                {
                    OnPropertyChanged(nameof(IsTodaysJobsEmpty));
                }
            }
        }

        public bool IsTodaysJobsEmpty => TodaysJobsCount == 0;

        private int _readyRepairsCount;
        /// <summary>
        /// Teslime hazır tamir sayısı
        /// </summary>
        public int ReadyRepairsCount
        {
            get => _readyRepairsCount;
            set
            {
                if (SetProperty(ref _readyRepairsCount, value))
                {
                    OnPropertyChanged(nameof(IsReadyRepairsEmpty));
                }
            }
        }

        public bool IsReadyRepairsEmpty => ReadyRepairsCount == 0;

        #endregion

        #region Widget 3: Aylık Özet (Finans)

        private decimal _monthlySalesTotal;
        public decimal MonthlySalesTotal
        {
            get => _monthlySalesTotal;
            set => SetProperty(ref _monthlySalesTotal, value);
        }

        private int _monthlySalesCount;
        public int MonthlySalesCount
        {
            get => _monthlySalesCount;
            set => SetProperty(ref _monthlySalesCount, value);
        }

        private int _monthlyJobsCompleted;
        public int MonthlyJobsCompleted
        {
            get => _monthlyJobsCompleted;
            set => SetProperty(ref _monthlyJobsCompleted, value);
        }

        private int _activeJobsCount;
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

        #region Müşteri İstatistikleri

        private int _totalCustomers;
        public int TotalCustomers
        {
            get => _totalCustomers;
            set => SetProperty(ref _totalCustomers, value);
        }

        private int _newCustomersThisMonth;
        public int NewCustomersThisMonth
        {
            get => _newCustomersThisMonth;
            set => SetProperty(ref _newCustomersThisMonth, value);
        }

        private int _vipCustomers;
        public int VipCustomers
        {
            get => _vipCustomers;
            set => SetProperty(ref _vipCustomers, value);
        }

        private int _upcomingBirthdays;
        public int UpcomingBirthdays
        {
            get => _upcomingBirthdays;
            set => SetProperty(ref _upcomingBirthdays, value);
        }

        public ObservableCollection<Customer> BirthdayCustomers { get; set; } = new();

        #endregion

        #region Satış Raporları

        private decimal _todaySalesTotal;
        public decimal TodaySalesTotal
        {
            get => _todaySalesTotal;
            set => SetProperty(ref _todaySalesTotal, value);
        }

        private int _todaySalesCount;
        public int TodaySalesCount
        {
            get => _todaySalesCount;
            set
            {
                if (SetProperty(ref _todaySalesCount, value))
                {
                    OnPropertyChanged(nameof(TodaySalesCountText));
                }
            }
        }
        
        public string TodaySalesCountText => $"{TodaySalesCount} işlem";

        private decimal _weekSalesTotal;
        public decimal WeekSalesTotal
        {
            get => _weekSalesTotal;
            set => SetProperty(ref _weekSalesTotal, value);
        }

        private decimal _averageSaleAmount;
        public decimal AverageSaleAmount
        {
            get => _averageSaleAmount;
            set => SetProperty(ref _averageSaleAmount, value);
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
        public DashboardViewModel(IAuthService authService, ApiClient apiClient, ILoadingService loadingService, IToastService toastService)
        {
            _authService = authService;
            _apiClient = apiClient;
            _loadingService = loadingService;
            _toastService = toastService;
            
            RefreshDashboardCommand = new RelayCommand(async _ => await LoadDashboardDataAsync());
            _ = LoadDashboardDataAsync();
        }

        /// <summary>
        /// Constructor for design-time support
        /// </summary>
        public DashboardViewModel()
        {
            // Design-time için varsayılan değerler
            _authService = new DesignTimeAuthService();
            _apiClient = null!;
            _loadingService = null!;
            _toastService = null!;
            LowStockProducts = new ObservableCollection<LowStockItemDto>();
            TodaysJobs = new ObservableCollection<TodayJobItemDto>();
            ReadyToDeliverRepairs = new ObservableCollection<ReadyRepairItemDto>();
            RefreshDashboardCommand = new RelayCommand(_ => { });
        }

        /// <summary>
        /// Design-time için basit auth servisi
        /// </summary>
        private class DesignTimeAuthService : IAuthService
        {
            public User? CurrentUser => new User { Ad = "Test", Soyad = "Kullanıcı", Username = "test" };
            public bool IsAdmin => true;
            public bool IsLoggedIn => true;
            public bool CanViewFinance => true;
            public bool CanViewAnalytics => true;
            public bool CanDeleteRecords => true;
            public bool CanApprovePurchase => true;
            public bool CanAccessSettings => true;
            public Task<bool> LoginAsync(string username, string password) => Task.FromResult(true);
            public void Logout() { }
        }

        /// <summary>
        /// Tüm dashboard verilerini yükle (API'den)
        /// </summary>
        private async Task LoadDashboardDataAsync()
        {
            _loadingService?.Show();
            try
            {
                var response = await _apiClient.GetAsync<DashboardSummaryDto>("api/dashboard/summary");
                if (response != null && response.Data != null)
                {
                    var dto = response.Data;
                    
                    // 1. Low Stocks
                    LowStockProducts.Clear();
                    foreach(var item in dto.LowStockProducts) LowStockProducts.Add(item);
                    LowStockCount = dto.LowStockProducts.Count;
                    
                    // 2. Todays Jobs
                    TodaysJobs.Clear();
                    foreach(var item in dto.TodaysJobs)
                    {
                        item.Category = GetCategoryIcon(item.Category) + " " + GetCategoryName(item.Category);
                        TodaysJobs.Add(item);
                    }
                    TodaysJobsCount = dto.TodaysJobs.Count;
                    
                    // 3. Ready Repairs
                    ReadyToDeliverRepairs.Clear();
                    foreach(var item in dto.ReadyToDeliverRepairs) ReadyToDeliverRepairs.Add(item);
                    ReadyRepairsCount = dto.ReadyToDeliverRepairs.Count;
                    
                    // 4. Financials
                    DailyIncome = dto.Financials.DailyIncome;
                    DailyExpense = dto.Financials.DailyExpense;
                    MonthlySalesTotal = dto.Financials.MonthlySalesTotal;
                    MonthlySalesCount = dto.Financials.MonthlySalesCount;
                    MonthlyJobsCompleted = dto.Financials.MonthlyJobsCompleted;
                    ActiveJobsCount = dto.Financials.ActiveJobsCount;
                    
                    // 5. Customer Stats
                    TotalCustomers = dto.CustomerStats.TotalCustomers;
                    NewCustomersThisMonth = dto.CustomerStats.NewCustomersThisMonth;
                    VipCustomers = dto.CustomerStats.VipCustomers;
                    UpcomingBirthdays = dto.CustomerStats.UpcomingBirthdays;
                    BirthdayCustomers.Clear();
                    foreach(var bday in dto.CustomerStats.BirthdayCustomers) BirthdayCustomers.Add(bday);
                    
                    // 6. Sales Reports
                    TodaySalesTotal = dto.SalesReports.TodaySalesTotal;
                    TodaySalesCount = dto.SalesReports.TodaySalesCount;
                    WeekSalesTotal = dto.SalesReports.WeekSalesTotal;
                    AverageSaleAmount = dto.SalesReports.AverageSaleAmount;

                    // 7. Chart Data
                    LoadWeeklyTrendChart(dto.ChartData.WeeklyTrend);
                    LoadJobCategoryPieChart(dto.ChartData.JobCategoryDistribution);
                }
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Dashboard veri yükleme hatası: {ex.Message}");
            }
            finally
            {
                _loadingService?.Hide();
            }
        }

        #region Helper Methods

        private string GetCategoryIcon(string categoryStr)
        {
            if (Enum.TryParse<JobCategory>(categoryStr, out var category))
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
            return "🔧";
        }

        private string GetCategoryName(string categoryStr)
        {
            if (Enum.TryParse<JobCategory>(categoryStr, out var category))
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
            return "Diğer";
        }

        /// <summary>
        /// 7 günlük gelir trend grafiği
        /// </summary>
        private void LoadWeeklyTrendChart(List<WeeklyTrendItemDto> trendData)
        {
            var labels = new List<string>();
            var incomeData = new List<double>();
            var jobsData = new List<double>();

            if (trendData != null)
            {
                foreach(var day in trendData)
                {
                    labels.Add(day.DayName);
                    incomeData.Add((double)day.Income);
                    jobsData.Add((double)day.CompletedJobs);
                }
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
        private void LoadJobCategoryPieChart(List<JobCategoryItemDto> distributionData)
        {
            if (distributionData == null || distributionData.Count == 0)
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
            foreach (var item in distributionData)
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
}

