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
using KamatekCrm.ApplicationCore.Services;

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
            AgingBreakdownSeries = Array.Empty<ISeries>();
            
            _ = LoadDataAsync();
        }

        #region KPI Properties

        private decimal _totalRevenue;
        public decimal TotalRevenue { get => _totalRevenue; set => SetProperty(ref _totalRevenue, value); }

        private decimal _totalCost;
        public decimal TotalCost { get => _totalCost; set => SetProperty(ref _totalCost, value); }

        private decimal _netProfit;
        public decimal NetProfit { get => _netProfit; set => SetProperty(ref _netProfit, value); }

        private decimal _totalReceivables;
        public decimal TotalReceivables { get => _totalReceivables; set => SetProperty(ref _totalReceivables, value); }

        private decimal _aging0To30;
        public decimal Aging0To30 { get => _aging0To30; set => SetProperty(ref _aging0To30, value); }

        private decimal _aging31To60;
        public decimal Aging31To60 { get => _aging31To60; set => SetProperty(ref _aging31To60, value); }

        private decimal _aging61To90;
        public decimal Aging61To90 { get => _aging61To90; set => SetProperty(ref _aging61To90, value); }

        private decimal _aging90Plus;
        public decimal Aging90Plus { get => _aging90Plus; set => SetProperty(ref _aging90Plus, value); }

        #endregion

        #region Chart Properties

        public ISeries[] MonthlyFinancialSeries { get; set; }
        public Axis[] MonthlyXAxes { get; set; }

        public ISeries[] CostBreakdownSeries { get; set; }
        public ISeries[] AgingBreakdownSeries { get; set; }

        #endregion

        #region DataGrid Properties (Project Profitability)

        public ObservableCollection<ProjectProfitItem> ProjectProfits { get; set; } = new();
        public ObservableCollection<CustomerAgingSummary> CustomerAgings { get; set; } = new();

        #endregion

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var projects = await context.ServiceProjects
                    .Include(p => p.Customer)
                    .Where(p => p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Completed)
                    .ToListAsync();

                // Load Cash Transactions for expenses & income consolidation
                var cashTransactions = await context.CashTransactions
                    .Where(t => FinancialTransactionPolicy.CashExpenseTypes.Contains(t.TransactionType))
                    .ToListAsync();

                var salesOrders = await context.SalesOrders.Where(s => s.Status != SalesOrderStatus.Cancelled && s.Status != SalesOrderStatus.Draft).ToListAsync();
                var salesReturns = await context.SalesReturns.AsNoTracking().ToListAsync();

                // --- KPI Consolidation ---
                var projectRevenue = projects.Sum(p => p.TotalCost + p.TotalProfit);
                var posRevenue = salesOrders.Sum(s => s.TotalAmount) - salesReturns.Sum(item => item.TotalAmount);
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
                    var monthlyReturns = salesReturns.Where(s => s.Date.Month == date.Month && s.Date.Year == date.Year).ToList();
                    var monthlyExp = cashTransactions.Where(t => t.Date.Month == date.Month && t.Date.Year == date.Year).ToList();

                    var mRev = monthlyProjects.Sum(p => p.TotalCost + p.TotalProfit) + monthlySales.Sum(s => s.TotalAmount) - monthlyReturns.Sum(s => s.TotalAmount);
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

                // --- 4. Cari Yaşlandırma Analizi (Accounts Receivable Aging) ---
                var customerTransactions = await context.Transactions
                    .Include(t => t.Customer)
                    .AsNoTracking()
                    .ToListAsync();

                var customerGroups = customerTransactions
                    .Where(t => t.CustomerId > 0)
                    .GroupBy(t => t.CustomerId);

                decimal total0To30 = 0m;
                decimal total31To60 = 0m;
                decimal total61To90 = 0m;
                decimal total90Plus = 0m;
                decimal sumReceivables = 0m;
                var agingSummaries = new List<CustomerAgingSummary>();
                var now = DateTime.UtcNow;

                foreach (var group in customerGroups)
                {
                    var balance = FinancialTransactionPolicy.CalculateCustomerBalance(group);
                    if (balance <= 0) continue;

                    sumReceivables += balance;
                    var customerName = group.FirstOrDefault()?.Customer?.FullName ?? $"Müşteri #{group.Key}";

                    var debtTxList = group.Where(t => t.Type == TransactionType.Debt).OrderBy(t => t.Date).ToList();
                    decimal d0To30 = 0, d31To60 = 0, d61To90 = 0, d90Plus = 0;

                    if (debtTxList.Count == 0)
                    {
                        d0To30 = balance;
                    }
                    else
                    {
                        decimal remainingToClassify = balance;
                        foreach (var debt in debtTxList.OrderByDescending(t => t.Date))
                        {
                            var days = (now - debt.Date).TotalDays;
                            var debtAmount = Math.Min(debt.Amount, remainingToClassify);
                            if (debtAmount <= 0) break;

                            if (days <= 30) d0To30 += debtAmount;
                            else if (days <= 60) d31To60 += debtAmount;
                            else if (days <= 90) d61To90 += debtAmount;
                            else d90Plus += debtAmount;

                            remainingToClassify -= debtAmount;
                        }

                        if (remainingToClassify > 0)
                        {
                            d90Plus += remainingToClassify;
                        }
                    }

                    total0To30 += d0To30;
                    total31To60 += d31To60;
                    total61To90 += d61To90;
                    total90Plus += d90Plus;

                    agingSummaries.Add(new CustomerAgingSummary
                    {
                        CustomerId = group.Key ?? 0,
                        CustomerName = customerName,
                        TotalBalance = balance,
                        Days0To30 = d0To30,
                        Days31To60 = d31To60,
                        Days61To90 = d61To90,
                        Days90Plus = d90Plus
                    });
                }

                TotalReceivables = sumReceivables;
                Aging0To30 = total0To30;
                Aging31To60 = total31To60;
                Aging61To90 = total61To90;
                Aging90Plus = total90Plus;

                CustomerAgings = new ObservableCollection<CustomerAgingSummary>(agingSummaries.OrderByDescending(a => a.TotalBalance));

                var agingSeriesList = new List<ISeries>
                {
                    new PieSeries<decimal> { Values = new[] { total0To30 }, Name = "0-30 Gün (Güncel)", InnerRadius = 40 },
                    new PieSeries<decimal> { Values = new[] { total31To60 }, Name = "31-60 Gün", InnerRadius = 40 },
                    new PieSeries<decimal> { Values = new[] { total61To90 }, Name = "61-90 Gün", InnerRadius = 40 },
                    new PieSeries<decimal> { Values = new[] { total90Plus }, Name = "90+ Gün (Gecikmiş)", InnerRadius = 40 }
                };
                AgingBreakdownSeries = agingSeriesList.ToArray();

                OnPropertyChanged(nameof(TotalReceivables));
                OnPropertyChanged(nameof(Aging0To30));
                OnPropertyChanged(nameof(Aging31To60));
                OnPropertyChanged(nameof(Aging61To90));
                OnPropertyChanged(nameof(Aging90Plus));
                OnPropertyChanged(nameof(CustomerAgings));
                OnPropertyChanged(nameof(AgingBreakdownSeries));
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

    public class CustomerAgingSummary
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalBalance { get; set; }
        public decimal Days0To30 { get; set; }
        public decimal Days31To60 { get; set; }
        public decimal Days61To90 { get; set; }
        public decimal Days90Plus { get; set; }
    }
}

