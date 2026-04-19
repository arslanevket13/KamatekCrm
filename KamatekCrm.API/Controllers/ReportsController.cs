using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Data;
using KamatekCrm.API.Models;
using KamatekCrm.API.Services;
using Microsoft.AspNetCore.Authorization;

namespace KamatekCrm.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICacheService _cache;
        private readonly IExcelService _excel;

        public ReportsController(AppDbContext context, ICacheService cache, IExcelService excel)
        {
            _context = context;
            _cache = cache;
            _excel = excel;
        }

        /// <summary>Teknisyen performans raporu</summary>
        [HttpGet("technician-performance")]
        public async Task<IActionResult> GetTechnicianPerformance(
            [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
            var end = endDate ?? DateTime.UtcNow;

            var report = await _context.Users
                .Where(u => u.IsTechnician && u.IsActive)
                .Select(u => new
                {
                    TechnicianId = u.Id,
                    Name = u.Ad + " " + u.Soyad,
                    u.ServiceArea,
                    TotalJobs = _context.ServiceJobs.Count(j => j.AssignedUserId == u.Id && j.CreatedDate >= start && j.CreatedDate <= end),
                    CompletedJobs = _context.ServiceJobs.Count(j => j.AssignedUserId == u.Id && j.Status == JobStatus.Completed && j.CreatedDate >= start),
                    u.AverageRating,
                    u.TotalJobsCompleted
                })
                .OrderByDescending(x => x.CompletedJobs)
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(report));
        }

        /// <summary>Aylık gelir-gider özeti</summary>
        [HttpGet("monthly-revenue")]
        public async Task<IActionResult> GetMonthlyRevenue([FromQuery] int? year, [FromQuery] int? month)
        {
            var targetYear = year ?? DateTime.UtcNow.Year;
            var targetMonth = month ?? DateTime.UtcNow.Month;
            var monthStart = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);

            var revenueTask = _context.SalesOrders
                .Where(s => s.Date >= monthStart && s.Date < monthEnd && s.Status == SalesOrderStatus.Completed)
                .SumAsync(s => s.TotalAmount);

            var expenseTask = _context.PurchaseInvoices
                .Where(p => p.Date >= monthStart && p.Date < monthEnd)
                .SumAsync(p => p.GrandTotal);

            var serviceRevenueTask = _context.ServiceJobs
                .Where(j => j.CompletedDate >= monthStart && j.CompletedDate < monthEnd && j.Status == JobStatus.Completed)
                .SumAsync(j => j.Price + j.LaborCost - j.DiscountAmount);

            await Task.WhenAll(revenueTask, expenseTask, serviceRevenueTask);

            var salesRevenue = revenueTask.Result;
            var expenses = expenseTask.Result;
            var serviceRevenue = serviceRevenueTask.Result;

            return Ok(ApiResponse<object>.Ok(new
            {
                Year = targetYear,
                Month = targetMonth,
                SalesRevenue = salesRevenue,
                ServiceRevenue = serviceRevenue,
                TotalRevenue = salesRevenue + serviceRevenue,
                TotalExpenses = expenses,
                NetProfit = salesRevenue + serviceRevenue - expenses,
                ProfitMargin = (salesRevenue + serviceRevenue) > 0
                    ? Math.Round((salesRevenue + serviceRevenue - expenses) / (salesRevenue + serviceRevenue) * 100, 2) : 0
            }));
        }

        /// <summary>Stok değerleme raporu</summary>
        [HttpGet("stock-valuation")]
        public async Task<IActionResult> GetStockValuation()
        {
            var report = await _context.Products
                .Where(p => p.TotalStockQuantity > 0)
                .OrderByDescending(p => p.TotalStockQuantity * p.AverageCost)
                .Select(p => new
                {
                    p.Id,
                    p.ProductName,
                    p.SKU,
                    p.TotalStockQuantity,
                    p.AverageCost,
                    p.SalePrice,
                    StockValue = p.TotalStockQuantity * p.AverageCost,
                    PotentialRevenue = p.TotalStockQuantity * p.SalePrice,
                    PotentialProfit = p.TotalStockQuantity * (p.SalePrice - p.AverageCost)
                })
                .ToListAsync();

            var totalValue = report.Sum(r => r.StockValue);
            var totalPotential = report.Sum(r => r.PotentialRevenue);

            return Ok(ApiResponse<object>.Ok(new
            {
                Items = report,
                Summary = new
                {
                    TotalProducts = report.Count,
                    TotalStockValue = totalValue,
                    TotalPotentialRevenue = totalPotential,
                    TotalPotentialProfit = totalPotential - totalValue
                }
            }));
        }

        /// <summary>Müşteri segmentasyon raporu</summary>
        [HttpGet("customer-segments")]
        public async Task<IActionResult> GetCustomerSegments()
        {
            var segments = await _context.Customers
                .GroupBy(c => c.Segment)
                .Select(g => new
                {
                    Segment = g.Key.ToString(),
                    Count = g.Count(),
                    TotalSpent = g.Sum(c => c.TotalSpent),
                    AvgSpent = g.Average(c => c.TotalSpent)
                })
                .OrderByDescending(x => x.TotalSpent)
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(segments));
        }

        /// <summary>SLA performans raporu</summary>
        [HttpGet("sla-performance")]
        public async Task<IActionResult> GetSlaPerformance([FromQuery] int days = 30)
        {
            var since = DateTime.UtcNow.AddDays(-days);

            var jobs = await _context.ServiceJobs
                .Where(j => j.CreatedDate >= since && j.SlaDeadline.HasValue)
                .ToListAsync();

            var total = jobs.Count;
            var onTime = jobs.Count(j => j.Status == JobStatus.Completed && j.CompletedDate <= j.SlaDeadline);
            var breached = jobs.Count(j => j.IsSlaBreached);
            var atRisk = jobs.Count(j => !j.IsSlaBreached && j.SlaDeadline.HasValue
                && j.SlaDeadline.Value < DateTime.UtcNow.AddHours(8)
                && j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled);

            return Ok(ApiResponse<object>.Ok(new
            {
                Period = $"Son {days} gün",
                TotalWithSla = total,
                OnTime = onTime,
                OnTimeRate = total > 0 ? Math.Round((double)onTime / total * 100, 1) : 0,
                Breached = breached,
                AtRisk = atRisk
            }));
        }
    }
}
