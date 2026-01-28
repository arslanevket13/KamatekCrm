using System;
using System.Collections.ObjectModel;
using System.Linq;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    public class FinancialHealthViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;

        public FinancialHealthViewModel()
        {
            _context = new AppDbContext();
            // Initialize non-nullable properties
            MonthlyFinancialSeries = Array.Empty<ISeries>();
            MonthlyXAxes = Array.Empty<Axis>();
            CostBreakdownSeries = Array.Empty<ISeries>();
            
            LoadData();
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

        private void LoadData()
        {
            try
            {
                var projects = _context.ServiceProjects
                    .Include(p => p.Customer)
                    .Where(p => p.Status != ProjectStatus.Cancelled)
                    .ToList();

                // --- KPI ---
                TotalRevenue = projects.Sum(p => p.TotalCost + p.TotalProfit); // Satış = Maliyet + Kar
                TotalCost = projects.Sum(p => p.TotalCost);     // Maliyet
                NetProfit = TotalRevenue - TotalCost;

                // --- 1. Aylık Gelir/Gider (Line Chart) ---
                var last6Months = Enumerable.Range(0, 6)
                    .Select(i => DateTime.Now.AddMonths(-i))
                    .OrderBy(d => d)
                    .ToList();

                var revenueValues = new List<decimal>();
                var costValues = new List<decimal>();
                var labels = new List<string>();

                foreach (var date in last6Months)
                {
                    // Basitlik için: Projenin oluşturulduğu tarihe göre finansal veriyi alıyoruz
                    var monthlyProjects = projects.Where(p => p.CreatedDate.Month == date.Month && p.CreatedDate.Year == date.Year).ToList();
                    
                    revenueValues.Add(monthlyProjects.Sum(p => p.TotalCost + p.TotalProfit));
                    costValues.Add(monthlyProjects.Sum(p => p.TotalCost));
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


                // --- 2. Maliyet Dağılımı (Pie Chart - Simülasyon) ---
                CostBreakdownSeries = new ISeries[]
                {
                    new PieSeries<decimal> { Values = new[] { TotalCost * 0.7m }, Name = "Malzeme", InnerRadius = 50 },
                    new PieSeries<decimal> { Values = new[] { TotalCost * 0.3m }, Name = "İşçilik", InnerRadius = 50 }
                };


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
