using System;
using System.Collections.ObjectModel;
using System.Linq;
using KamatekCrm.Infrastructure.Data;
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
    public partial class FinancialHealthViewModel : ViewModelBase
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

        public FinancialHealthViewModel(IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
            // Initialize non-nullable properties
            MonthlyFinancialSeries = Array.Empty<ISeries>();
            MonthlyXAxes = Array.Empty<Axis>();
            CostBreakdownSeries = Array.Empty<ISeries>();
            
            _ = LoadDataAsync();
        }

        #region KPI Properties

        private decimal _totalRevenue;
        public decimal TotalRevenue { get => _totalRevenue; set => SetProperty(ref _totalRevenue, value); }

        private decimal _totalCost;
        public decimal TotalCost { get => _totalCost; set => SetProperty(ref _totalCost, value); }

        private decimal _netProfit;
        public decimal NetProfit { get => _netProfit; set => SetProperty(ref _netProfit, value); }

        #endregion

        #region Chart Properties

        public ISeries[] MonthlyFinancialSeries { get; set; }
        public Axis[] MonthlyXAxes { get; set; }

        public ISeries[] CostBreakdownSeries { get; set; }

        #endregion

        #region DataGrid Properties (Project Profitability)

        public ObservableCollection<ProjectProfitItem> ProjectProfits { get; set; } = new();

        #endregion

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var projects = await context.ServiceProjects
                    .Include(p => p.Customer)
                    .Where(p => p.Status != ProjectStatus.Cancelled)
                    .ToListAsync();

                // Load Cash Transactions for expenses & income consolidation
                var cashTransactions = await context.CashTransactions
                    .Where(t => t.TransactionType == CashTransactionType.Expense || t.TransactionType == CashTransactionType.TransferExpense)
                    .ToListAsync();

                var salesOrders = await context.SalesOrders.ToListAsync();

                // --- KPI Consolidation ---
                var projectRevenue = projects.Sum(p => p.TotalCost + p.TotalProfit);
                var posRevenue = salesOrders.Sum(s => s.TotalAmount);
                TotalRevenue = projectRevenue + posRevenue;

                var projectCost = projects.Sum(p => p.TotalCost);
                var directExpenses = cashTransactions.Sum(t => t.Amount);
                TotalCost = projectCost + directExpenses;
                NetProfit = TotalRevenue - TotalCost;

                // --- 1. Aylık Gelir/Gider (Line Chart) ---
                var last6Months = Enumerable.Range(0, 6)
                    .Select(i => DateTime.UtcNow.AddMonths(-i))
                    .OrderBy(d => d)
                    .ToList();

                var revenueValues = new List<decimal>();
                var costValues = new List<decimal>();
                var labels = new List<string>();

                foreach (var date in last6Months)
                {
                    var monthlyProjects = projects.Where(p => p.CreatedDate.Month == date.Month && p.CreatedDate.Year == date.Year).ToList();
                    var monthlySales = salesOrders.Where(s => s.Date.Month == date.Month && s.Date.Year == date.Year).ToList();
                    var monthlyExp = cashTransactions.Where(t => t.Date.Month == date.Month && t.Date.Year == date.Year).ToList();

                    var mRev = monthlyProjects.Sum(p => p.TotalCost + p.TotalProfit) + monthlySales.Sum(s => s.TotalAmount);
                    var mCost = monthlyProjects.Sum(p => p.TotalCost) + monthlyExp.Sum(t => t.Amount);

                    revenueValues.Add(mRev);
                    costValues.Add(mCost);
                    labels.Add(date.ToString("MMM"));
                }

                MonthlyFinancialSeries = new ISeries[]
                {
                    new LineSeries<decimal>
                    {
                        Values = revenueValues.ToArray(),
                        Name = "Gelir (Revenue)",
                        Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 3 },
                        Fill = null,
                        GeometrySize = 10
                    },
                    new LineSeries<decimal>
                    {
                        Values = costValues.ToArray(),
                        Name = "Gider (Cost)",
                        Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 3 },
                        Fill = null,
                        GeometrySize = 10
                    }
                };

                MonthlyXAxes = new Axis[] { new Axis { Labels = labels.ToArray() } };

                // --- 2. Gerçek Maliyet Dağılımı (Pie Chart - Kategori Bazlı) ---
                var categoryExpenses = cashTransactions
                    .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Genel Gider" : t.Category)
                    .Select(g => new { Category = g.Key, Total = g.Sum(x => x.Amount) })
                    .OrderByDescending(g => g.Total)
                    .Take(6)
                    .ToList();

                if (projectCost > 0)
                {
                    categoryExpenses.Insert(0, new { Category = "Proje Maliyeti", Total = projectCost });
                }

                var pieSeries = new List<ISeries>();
                foreach (var cat in categoryExpenses)
                {
                    pieSeries.Add(new PieSeries<decimal>
                    {
                        Values = new[] { cat.Total },
                        Name = cat.Category,
                        InnerRadius = 50
                    });
                }

                CostBreakdownSeries = pieSeries.ToArray();

                // --- 3. Proje Kârlılık Listesi (DataGrid) ---
                var profitList = projects.Select(p => new ProjectProfitItem
                {
                    ProjectName = p.Title,
                    CustomerName = p.Customer?.FullName ?? "-",
                    Revenue = p.TotalCost + p.TotalProfit,
                    Cost = p.TotalCost,
                    Profit = p.TotalProfit,
                    MarginPercent = (p.TotalCost + p.TotalProfit) > 0 ? (p.TotalProfit / (p.TotalCost + p.TotalProfit)) * 100 : 0
                })
                .OrderByDescending(x => x.Profit)
                .Take(20) // Top 20
                .ToList();

                ProjectProfits = new ObservableCollection<ProjectProfitItem>(profitList);

                OnPropertyChanged(nameof(MonthlyFinancialSeries));
                OnPropertyChanged(nameof(MonthlyXAxes));
                OnPropertyChanged(nameof(CostBreakdownSeries));
                OnPropertyChanged(nameof(ProjectProfits));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Finansal veriler yüklenirken hata oluştu: {ex.Message}", "Hata", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    public class ProjectProfitItem
    {
        public string ProjectName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit { get; set; }
        public decimal MarginPercent { get; set; }
    }
}

