using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// İş Zekası (BI) Analytics Dashboard ViewModel
    /// LiveCharts ile finansal ve operasyonel analizler
    /// </summary>
    public class AnalyticsViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;

        #region Properties

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        // ═══════════════════════════════════════════════════════════════════
        // CHART 1: Finansal Trend (6 Aylık Gelir vs Gider)
        // ═══════════════════════════════════════════════════════════════════

        private ISeries[] _financialSeries = Array.Empty<ISeries>();
        public ISeries[] FinancialSeries
        {
            get => _financialSeries;
            set => SetProperty(ref _financialSeries, value);
        }

        private Axis[] _financialXAxes = Array.Empty<Axis>();
        public Axis[] FinancialXAxes
        {
            get => _financialXAxes;
            set => SetProperty(ref _financialXAxes, value);
        }

        // ═══════════════════════════════════════════════════════════════════
        // CHART 2: Servis Kategori Dağılımı (Pie)
        // ═══════════════════════════════════════════════════════════════════

        private ISeries[] _categorySeries = Array.Empty<ISeries>();
        public ISeries[] CategorySeries
        {
            get => _categorySeries;
            set => SetProperty(ref _categorySeries, value);
        }

        // ═══════════════════════════════════════════════════════════════════
        // CHART 3: En Çok Satan 5 Ürün (Bar)
        // ═══════════════════════════════════════════════════════════════════

        private ISeries[] _topProductsSeries = Array.Empty<ISeries>();
        public ISeries[] TopProductsSeries
        {
            get => _topProductsSeries;
            set => SetProperty(ref _topProductsSeries, value);
        }

        private Axis[] _topProductsYAxes = Array.Empty<Axis>();
        public Axis[] TopProductsYAxes
        {
            get => _topProductsYAxes;
            set => SetProperty(ref _topProductsYAxes, value);
        }

        // ═══════════════════════════════════════════════════════════════════
        // KPI KARTLARI
        // ═══════════════════════════════════════════════════════════════════

        private decimal _totalRevenue;
        public decimal TotalRevenue
        {
            get => _totalRevenue;
            set => SetProperty(ref _totalRevenue, value);
        }

        private decimal _totalExpense;
        public decimal TotalExpense
        {
            get => _totalExpense;
            set => SetProperty(ref _totalExpense, value);
        }

        private int _activeJobs;
        public int ActiveJobs
        {
            get => _activeJobs;
            set => SetProperty(ref _activeJobs, value);
        }

        private int _totalCustomers;
        public int TotalCustomers
        {
            get => _totalCustomers;
            set => SetProperty(ref _totalCustomers, value);
        }

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; }

        #endregion

        #region Constructor

        public AnalyticsViewModel()
        {
            _context = new AppDbContext();
            RefreshCommand = new RelayCommand(_ => LoadData());
            LoadData();
        }

        #endregion

        #region Methods

        private void LoadData()
        {
            IsLoading = true;

            try
            {
                LoadKPIs();
                LoadFinancialTrend();
                LoadCategoryDistribution();
                LoadTopProducts();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadKPIs()
        {
            var sixMonthsAgo = DateTime.Today.AddMonths(-6);

            // Toplam Gelir (6 ay)
            TotalRevenue = _context.CashTransactions
                .Where(t => t.Date >= sixMonthsAgo && 
                           (t.TransactionType == CashTransactionType.CashIncome || 
                            t.TransactionType == CashTransactionType.CardIncome || 
                            t.TransactionType == CashTransactionType.TransferIncome))
                .Sum(t => (decimal?)t.Amount) ?? 0;

            // Toplam Gider (6 ay)
            TotalExpense = _context.CashTransactions
                .Where(t => t.Date >= sixMonthsAgo && 
                           (t.TransactionType == CashTransactionType.Expense || 
                            t.TransactionType == CashTransactionType.TransferExpense))
                .Sum(t => (decimal?)t.Amount) ?? 0;

            // Aktif İşler
            ActiveJobs = _context.ServiceJobs
                .Count(j => j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled);

            // Toplam Müşteri
            TotalCustomers = _context.Customers.Count();
        }

        private void LoadFinancialTrend()
        {
            var months = Enumerable.Range(0, 6)
                .Select(i => DateTime.Today.AddMonths(-5 + i))
                .Select(d => new DateTime(d.Year, d.Month, 1))
                .ToList();

            var incomeData = new double[6];
            var expenseData = new double[6];

            for (int i = 0; i < 6; i++)
            {
                var startDate = months[i];
                var endDate = startDate.AddMonths(1);

                incomeData[i] = (double)(_context.CashTransactions
                    .Where(t => t.Date >= startDate && t.Date < endDate && 
                               (t.TransactionType == CashTransactionType.CashIncome || 
                                t.TransactionType == CashTransactionType.CardIncome || 
                                t.TransactionType == CashTransactionType.TransferIncome))
                    .Sum(t => (decimal?)t.Amount) ?? 0);

                expenseData[i] = (double)(_context.CashTransactions
                    .Where(t => t.Date >= startDate && t.Date < endDate && 
                               (t.TransactionType == CashTransactionType.Expense || 
                                t.TransactionType == CashTransactionType.TransferExpense))
                    .Sum(t => (decimal?)t.Amount) ?? 0);
            }

            FinancialSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name = "Gelir",
                    Values = incomeData,
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColors.LimeGreen, 3),
                    GeometryFill = new SolidColorPaint(SKColors.LimeGreen),
                    GeometrySize = 10
                },
                new LineSeries<double>
                {
                    Name = "Gider",
                    Values = expenseData,
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColors.OrangeRed, 3),
                    GeometryFill = new SolidColorPaint(SKColors.OrangeRed),
                    GeometrySize = 10
                }
            };

            FinancialXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = months.Select(m => m.ToString("MMM yy")).ToArray(),
                    LabelsPaint = new SolidColorPaint(SKColors.White),
                    SeparatorsPaint = new SolidColorPaint(new SKColor(100, 100, 100))
                }
            };
        }

        private void LoadCategoryDistribution()
        {
            var categories = _context.ServiceJobs
                .GroupBy(j => j.JobCategory)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToList();

            if (!categories.Any())
            {
                CategorySeries = Array.Empty<ISeries>();
                return;
            }

            var colors = new SKColor[]
            {
                SKColors.DodgerBlue,
                SKColors.Orange,
                SKColors.LimeGreen,
                SKColors.Crimson,
                SKColors.MediumPurple,
                SKColors.Gold,
                SKColors.Teal
            };

            CategorySeries = categories.Select((c, i) => new PieSeries<int>
            {
                Name = c.Category.ToString(),
                Values = new[] { c.Count },
                Fill = new SolidColorPaint(colors[i % colors.Length]),
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue}"
            }).ToArray<ISeries>();
        }

        private void LoadTopProducts()
        {
            var topProducts = _context.SalesOrderItems
                .GroupBy(i => i.ProductName)
                .Select(g => new { Name = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .OrderByDescending(x => x.Quantity)
                .Take(5)
                .ToList();

            if (!topProducts.Any())
            {
                TopProductsSeries = Array.Empty<ISeries>();
                return;
            }

            TopProductsSeries = new ISeries[]
            {
                new RowSeries<double>
                {
                    Name = "Satış Adedi",
                    Values = topProducts.Select(p => (double)p.Quantity).ToArray(),
                    Fill = new SolidColorPaint(SKColors.DodgerBlue),
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:N0}"
                }
            };

            TopProductsYAxes = new Axis[]
            {
                new Axis
                {
                    Labels = topProducts.Select(p => p.Name ?? "Bilinmiyor").ToArray(),
                    LabelsPaint = new SolidColorPaint(SKColors.White)
                }
            };
        }

        #endregion
    }
}
