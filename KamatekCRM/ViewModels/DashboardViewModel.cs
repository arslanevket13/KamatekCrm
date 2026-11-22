using KamatekCrm.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using KamatekCrm.Services;
using KamatekCrm.Shared.Services;
using KamatekCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using KamatekCrm.Views;
using KamatekCrm.ApplicationCore.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Dashboard ViewModel - Komut Merkezi: Kritik uyarılar, günlük işler ve finansal özet
    /// </summary>
    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
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
        public string TodayDate => DateTime.UtcNow.ToString("dd MMMM yyyy, dddd", new System.Globalization.CultureInfo("tr-TR"));

        /// <summary>
        /// Mevcut ay adı
        /// </summary>
        public string CurrentMonthName => DateTime.UtcNow.ToString("MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));

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
        
        public string TodaySalesCountText => $"{TodaySalesCount} satış";

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

        // Source Generator: RefreshDashboard() metodu [RelayCommand] ile işaretlenmiştir.
        // Otomatik olarak 'RefreshDashboardCommand' özelliği üretilir.

        #endregion

        private readonly IServiceProvider? _serviceProvider;
        private readonly IQuotationLauncher? _quotationLauncher;

        /// <summary>
        /// Constructor
        /// </summary>
        public DashboardViewModel(
            IAuthService authService, 
            IDbContextFactory<AppDbContext> dbContextFactory, 
            ILoadingService loadingService, 
            IToastService toastService, 
            IDatabaseConnectionProvider connectionProvider,
            IServiceProvider serviceProvider,
            IQuotationLauncher quotationLauncher)
        {
            _authService = authService;
            _dbContextFactory = dbContextFactory;
            _loadingService = loadingService;
            _toastService = toastService;
            _serviceProvider = serviceProvider;
            _quotationLauncher = quotationLauncher;
            
            // Eğer bağlantı zaten varsa direkt yükle
            if (connectionProvider.IsConnected)
            {
                _ = RefreshDashboard();
            }
            else
            {
                // Bağlantı henüz yoksa (uygulama yeni açılıyorsa Zero-Config sürecini bekle)
                EventAggregator.Instance.Subscribe<DatabaseConnectionEstablishedEvent>(OnDatabaseConnected);
                EventAggregator.Instance.Subscribe<DatabaseConnectionRestoredEvent>(OnDatabaseRestored);
            }
        }

        private void OnDatabaseConnected(DatabaseConnectionEstablishedEvent _)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await RefreshDashboard());
        }

        private void OnDatabaseRestored(DatabaseConnectionRestoredEvent _)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await RefreshDashboard());
        }

        /// <summary>
        /// Constructor for design-time support
        /// </summary>
        public DashboardViewModel()
        {
            // Design-time için varsayılan değerler
            _authService = new DesignTimeAuthService();
            _dbContextFactory = null!;
            _loadingService = null!;
            _toastService = null!;
            _serviceProvider = null;
            _quotationLauncher = null;
            LowStockProducts = new ObservableCollection<LowStockItemDto>();
            TodaysJobs = new ObservableCollection<TodayJobItemDto>();
            ReadyToDeliverRepairs = new ObservableCollection<ReadyRepairItemDto>();
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
            public Task<bool> TryAutoLoginAsync(int userId, string username) => Task.FromResult(true);
            public Task<(bool Success, string? ErrorMessage, User? CreatedUser)> CreateInitialAdminAsync(string fullName, string username, string password) => Task.FromResult<(bool, string?, User?)>((true, null, CurrentUser));
            public Task<(bool Success, string? ErrorMessage)> ResetAdminPasswordAsync(string newPassword) => Task.FromResult<(bool, string?)>((true, null));
            public void Logout() { }
            public bool HasRole(string role) => true;
            public void EnsureAuthorized(KamatekCrm.ApplicationCore.Security.ApplicationPermission permission) { }
        }

        #region Quick Action & Drill-Down Commands

        [RelayCommand]
        private void OpenFaultTicket()
        {
            if (_serviceProvider != null)
            {
                var faultVm = _serviceProvider.GetRequiredService<FaultTicketViewModel>();
                var window = new Views.FaultTicketWindow(faultVm);
                window.ShowDialog();
            }
        }

        [RelayCommand]
        private void OpenDirectSales()
        {
            if (_serviceProvider != null)
            {
                var directSalesVm = _serviceProvider.GetRequiredService<DirectSalesViewModel>();
                var window = new DirectSalesWindow(directSalesVm);
                window.Show();
            }
        }

        [RelayCommand]
        private async Task OpenQuotation()
        {
            if (_quotationLauncher is not null)
                await _quotationLauncher.ShowAsync(modal: false);
        }

        [RelayCommand]
        private void NavigateToFinance()
        {
            if (_serviceProvider == null) return;
            if (!_authService.CanViewFinance)
            {
                _toastService?.ShowWarning("Finans ve Kasa ekranı için yetkiniz bulunmuyor.");
                return;
            }
            var nav = _serviceProvider.GetRequiredService<NavigationService>();
            nav.NavigateTo<FinanceViewModel>();
        }

        [RelayCommand]
        private void NavigateToAnalytics()
        {
            if (_serviceProvider == null) return;
            if (_authService.CanViewAnalytics)
            {
                var nav = _serviceProvider.GetRequiredService<NavigationService>();
                nav.NavigateTo<AnalyticsViewModel>();
                return;
            }
            if (_authService.CanViewFinance)
            {
                var nav = _serviceProvider.GetRequiredService<NavigationService>();
                nav.NavigateTo<FinanceViewModel>();
                return;
            }
            _toastService?.ShowWarning("Analitik veya finans raporları için yetkiniz bulunmuyor.");
        }

        [RelayCommand]
        private void NavigateToActiveJobs()
        {
            if (_serviceProvider == null) return;
            var nav = _serviceProvider.GetRequiredService<NavigationService>();
            var jobVm = nav.NavigateTo<ServiceJobViewModel>();
            jobVm.SelectedStatusFilter = StatusFilter.InProgress;
        }

        [RelayCommand]
        private void NavigateToReadyRepairs()
        {
            if (_serviceProvider == null) return;
            var nav = _serviceProvider.GetRequiredService<NavigationService>();
            nav.NavigateTo<RepairListViewModel>();
        }

        [RelayCommand]
        private void OpenLowStockProduct(LowStockItemDto? item)
        {
            if (_serviceProvider == null || item == null) return;
            var nav = _serviceProvider.GetRequiredService<NavigationService>();
            var productVm = nav.NavigateTo<ProductViewModel>();
            productVm.FocusProductById(item.ProductId);
        }

        [RelayCommand]
        private void OpenTodayJob(TodayJobItemDto? item)
        {
            if (_serviceProvider == null || item == null) return;
            var nav = _serviceProvider.GetRequiredService<NavigationService>();
            var jobVm = nav.NavigateTo<ServiceJobViewModel>();
            jobVm.FocusJobByIdAsync(item.JobId).SafeFireAndForget("DashboardViewModel.jobVm.FocusJobByIdAsync");
        }

        #endregion

        /// <summary>
        /// Source Generator: Bu metottan otomatik olarak 'RefreshDashboardCommand' ICommand özelliği üretilir.
        /// </summary>
        [RelayCommand]
        private async Task RefreshDashboard()
        {
            _loadingService?.Show();
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
                var tomorrowUtc = todayUtc.AddDays(1);
                var startOfMonthUtc = DateTime.SpecifyKind(new DateTime(todayUtc.Year, todayUtc.Month, 1), DateTimeKind.Utc);
                var nextMonthUtc = startOfMonthUtc.AddMonths(1);
                var startOfWeekUtc = todayUtc.AddDays(-(int)todayUtc.DayOfWeek);

                // 1. Low Stocks (gerçek SQL toplam sayı + önizleme için ilk 10)
                var lowStockQuery = context.Products.AsNoTracking()
                    .Where(p => p.TotalStockQuantity <= p.MinStockLevel);

                LowStockCount = await lowStockQuery.CountAsync();
                var lowStock = await lowStockQuery
                    .OrderBy(p => p.TotalStockQuantity)
                    .Take(10)
                    .ToListAsync();

                LowStockProducts.Clear();
                foreach (var item in lowStock)
                {
                    string urgency = item.TotalStockQuantity <= 0
                        ? "Tükendi"
                        : (item.TotalStockQuantity <= item.MinStockLevel / 2 ? "Kritik" : "Uyarı");
                    LowStockProducts.Add(new LowStockItemDto
                    {
                        ProductId = item.Id,
                        ProductName = item.ProductName,
                        CurrentStock = item.TotalStockQuantity,
                        MinStockLevel = item.MinStockLevel,
                        UrgencyLevel = urgency
                    });
                }

                // 2. Todays Jobs (ScheduledDate bazlı UTC aralık + gerçek SQL CountAsync)
                var todaysJobsQuery = context.ServiceJobs.AsNoTracking()
                    .Where(j => j.ScheduledDate.HasValue &&
                                j.ScheduledDate.Value >= todayUtc &&
                                j.ScheduledDate.Value < tomorrowUtc &&
                                j.Status != JobStatus.Completed &&
                                j.Status != JobStatus.Cancelled);

                TodaysJobsCount = await todaysJobsQuery.CountAsync();
                var todaysJobs = await todaysJobsQuery
                    .Include(j => j.Customer)
                    .OrderBy(j => j.ScheduledDate)
                    .Take(10)
                    .ToListAsync();

                TodaysJobs.Clear();
                foreach (var item in todaysJobs)
                {
                    TodaysJobs.Add(new TodayJobItemDto
                    {
                        JobId = item.Id,
                        CustomerName = item.Customer?.CompanyName ?? item.Customer?.FullName ?? "Bilinmiyor",
                        Category = GetCategoryIcon(item.JobCategory.ToString()) + " " + GetCategoryName(item.JobCategory.ToString()),
                        ScheduledTime = item.ScheduledDate?.ToLocalTime().ToString("HH:mm") ?? "",
                        Priority = item.Priority.ToString()
                    });
                }

                // 3. Ready Repairs (gerçek SQL CountAsync + ilk 10 önizleme)
                var readyJobsQuery = context.ServiceJobs.AsNoTracking()
                    .Where(j => j.Status == JobStatus.Completed || j.Status == JobStatus.WaitingForApproval);

                ReadyRepairsCount = await readyJobsQuery.CountAsync();
                var readyJobs = await readyJobsQuery
                    .Include(j => j.Customer)
                    .OrderBy(j => j.CreatedDate)
                    .Take(10)
                    .ToListAsync();

                ReadyToDeliverRepairs.Clear();
                foreach (var item in readyJobs)
                {
                    ReadyToDeliverRepairs.Add(new ReadyRepairItemDto
                    {
                        JobId = item.Id,
                        CustomerName = item.Customer?.CompanyName ?? item.Customer?.FullName ?? "Bilinmiyor",
                        DeviceInfo = item.Description,
                        DaysWaiting = (DateTime.UtcNow - item.CreatedDate).Days
                    });
                }

                // 4. Financials (UTC aralık sorguları + gerçek satış sayıları)
                DailyIncome = await context.CashTransactions.AsNoTracking()
                    .Where(c => c.Date >= todayUtc && c.Date < tomorrowUtc && FinancialTransactionPolicy.CashIncomeTypes.Contains(c.TransactionType))
                    .SumAsync(c => c.Amount);
                DailyExpense = await context.CashTransactions.AsNoTracking()
                    .Where(c => c.Date >= todayUtc && c.Date < tomorrowUtc && FinancialTransactionPolicy.CashExpenseTypes.Contains(c.TransactionType))
                    .SumAsync(c => c.Amount);
                MonthlySalesTotal = await context.CashTransactions.AsNoTracking()
                    .Where(c => c.Date >= startOfMonthUtc && c.Date < nextMonthUtc && FinancialTransactionPolicy.CashIncomeTypes.Contains(c.TransactionType))
                    .SumAsync(c => c.Amount);
                MonthlySalesCount = await context.SalesOrders.AsNoTracking()
                    .CountAsync(s => s.Date >= startOfMonthUtc && s.Date < nextMonthUtc && s.Status != SalesOrderStatus.Cancelled);
                MonthlyJobsCompleted = await context.ServiceJobs.AsNoTracking()
                    .CountAsync(j => j.CreatedDate >= startOfMonthUtc && j.CreatedDate < nextMonthUtc &&
                                     (j.Status == JobStatus.Completed || j.Status == JobStatus.Delivered));
                ActiveJobsCount = await context.ServiceJobs.AsNoTracking()
                    .CountAsync(j => j.Status == JobStatus.Pending || j.Status == JobStatus.InProgress);

                // 5. Customer Stats
                TotalCustomers = await context.Customers.AsNoTracking().CountAsync();
                NewCustomersThisMonth = await context.Customers.AsNoTracking()
                    .CountAsync(c => c.CreatedDate >= startOfMonthUtc && c.CreatedDate < nextMonthUtc);
                VipCustomers = await context.Customers.AsNoTracking()
                    .CountAsync(c => c.Type == CustomerType.Corporate);
                UpcomingBirthdays = 0;

                // 6. Sales Reports (Bugünkü gerçek POS satış adedi)
                TodaySalesTotal = DailyIncome;
                TodaySalesCount = await context.SalesOrders.AsNoTracking()
                    .CountAsync(s => s.Date >= todayUtc && s.Date < tomorrowUtc && s.Status != SalesOrderStatus.Cancelled);
                WeekSalesTotal = await context.CashTransactions.AsNoTracking()
                    .Where(c => c.Date >= startOfWeekUtc && c.Date < tomorrowUtc && FinancialTransactionPolicy.CashIncomeTypes.Contains(c.TransactionType))
                    .SumAsync(c => c.Amount);
                AverageSaleAmount = MonthlySalesCount > 0 ? MonthlySalesTotal / MonthlySalesCount : 0;

                // 7. 7-Day Trend Chart Population
                var trendStartDate = todayUtc.AddDays(-6);
                var trendEndDate = tomorrowUtc;
                var incomeByDate = await context.CashTransactions.AsNoTracking()
                    .Where(c => c.Date >= trendStartDate &&
                                c.Date < trendEndDate &&
                                FinancialTransactionPolicy.CashIncomeTypes.Contains(c.TransactionType))
                    .GroupBy(c => c.Date.Date)
                    .Select(group => new { Date = group.Key, Total = group.Sum(item => item.Amount) })
                    .ToDictionaryAsync(item => item.Date, item => item.Total);

                var completedJobsByDate = await context.ServiceJobs.AsNoTracking()
                    .Where(j => (j.CompletedDate ?? j.CreatedDate) >= trendStartDate &&
                                (j.CompletedDate ?? j.CreatedDate) < trendEndDate &&
                                (j.Status == JobStatus.Completed || j.Status == JobStatus.Delivered))
                    .GroupBy(j => (j.CompletedDate ?? j.CreatedDate).Date)
                    .Select(group => new { Date = group.Key, Count = group.Count() })
                    .ToDictionaryAsync(item => item.Date, item => item.Count);

                var weeklyTrendList = new List<WeeklyTrendItemDto>(capacity: 7);
                for (int i = 6; i >= 0; i--)
                {
                    var targetDate = todayUtc.AddDays(-i);

                    weeklyTrendList.Add(new WeeklyTrendItemDto
                    {
                        DayName = targetDate.ToString("ddd", new System.Globalization.CultureInfo("tr-TR")),
                        Income = incomeByDate.GetValueOrDefault(targetDate),
                        CompletedJobs = completedJobsByDate.GetValueOrDefault(targetDate)
                    });
                }
                LoadWeeklyTrendChart(weeklyTrendList);

                // 8. Job Category Distribution
                var categoryGroups = await context.ServiceJobs.AsNoTracking()
                    .GroupBy(j => j.JobCategory)
                    .Select(g => new JobCategoryItemDto { Category = g.Key.ToString(), Count = g.Count() })
                    .ToListAsync();
                LoadJobCategoryPieChart(categoryGroups);
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
        /// 7 günlük gelir ve tamamlanan iş çift Y eksenli trend grafiği
        /// </summary>
        private void LoadWeeklyTrendChart(List<WeeklyTrendItemDto> trendData)
        {
            var labels = new List<string>();
            var incomeData = new List<double>();
            var jobsData = new List<double>();

            if (trendData != null)
            {
                foreach (var day in trendData)
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
                    ScalesYAt = 0,
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
                    ScalesYAt = 1,
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
                    Name = "Gelir (₺)",
                    NameTextSize = 11,
                    NamePaint = new SolidColorPaint(SKColors.DodgerBlue),
                    LabelsPaint = new SolidColorPaint(SKColors.DodgerBlue),
                    TextSize = 11,
                    MinLimit = 0,
                    Labeler = value => $"{value:N0} ₺"
                },
                new Axis
                {
                    Name = "İş Adedi",
                    NameTextSize = 11,
                    NamePaint = new SolidColorPaint(SKColors.MediumSeaGreen),
                    LabelsPaint = new SolidColorPaint(SKColors.MediumSeaGreen),
                    Position = LiveChartsCore.Measure.AxisPosition.End,
                    TextSize = 11,
                    MinLimit = 0,
                    MinStep = 1,
                    ForceStepToMin = true,
                    ShowSeparatorLines = false,
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

