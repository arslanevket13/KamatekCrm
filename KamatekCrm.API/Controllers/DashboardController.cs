using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Data;
using KamatekCrm.API.Services;
using KamatekCrm.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace KamatekCrm.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICacheService _cache;

        public DashboardController(AppDbContext context, ICacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        /// <summary>
        /// Dashboard özet istatistikleri — 30s cache, paralel sorgular
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var stats = await _cache.GetOrCreateAsync(CacheKeys.DashboardStats, async () =>
            {
                var today = DateTime.UtcNow.Date;
                var weekAgo = today.AddDays(-7);
                var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

                // Paralel sorgulama
                var todaysJobsTask = _context.ServiceJobs.CountAsync(j => j.CreatedDate >= today);
                var activeJobsTask = _context.ServiceJobs.CountAsync(j =>
                    j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled);
                var readyRepairsTask = _context.ServiceJobs.CountAsync(j => j.Status == JobStatus.Completed && j.CompletedDate >= weekAgo);
                var totalCustomersTask = _context.Customers.CountAsync();
                var totalJobsTask = _context.ServiceJobs.CountAsync();
                var monthlyCompletedTask = _context.ServiceJobs.CountAsync(j =>
                    j.Status == JobStatus.Completed && j.CompletedDate >= monthStart);

                await Task.WhenAll(
                    todaysJobsTask, activeJobsTask, readyRepairsTask,
                    totalCustomersTask, totalJobsTask, monthlyCompletedTask
                );

                return new
                {
                    TodaysJobs = todaysJobsTask.Result,
                    ActiveJobs = activeJobsTask.Result,
                    ReadyRepairs = readyRepairsTask.Result,
                    MonthlyCompleted = monthlyCompletedTask.Result,
                    TotalCustomers = totalCustomersTask.Result,
                    TotalJobs = totalJobsTask.Result,
                    GeneratedAt = DateTime.UtcNow
                };
            }, CacheService.DashboardTtl);

            return Ok(ApiResponse<object>.Ok(stats!));
        }

        /// <summary>
        /// Dashboard için kapsamlı masaüstü UI verilerini getirir
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetDashboardSummary()
        {
            var summary = await _cache.GetOrCreateAsync("DashboardSummary", async () =>
            {
                var today = DateTime.UtcNow.Date;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1);

                var dto = new KamatekCrm.Shared.Models.DashboardSummaryDto();

                // 1. Low Stock Products
                var lowStockThreshold = 5;
                dto.LowStockProducts = await _context.Products
                    .Where(p => p.TotalStockQuantity <= lowStockThreshold && p.TotalStockQuantity >= 0)
                    .OrderBy(p => p.TotalStockQuantity)
                    .Take(10)
                    .Select(p => new KamatekCrm.Shared.Models.LowStockItemDto
                    {
                        ProductId = p.Id,
                        ProductName = p.ProductName ?? "Bilinmeyen Ürün",
                        CurrentStock = p.TotalStockQuantity,
                        MinStockLevel = p.MinStockLevel,
                        UrgencyLevel = p.TotalStockQuantity == 0 ? "Kritik" : 
                                       p.TotalStockQuantity <= 2 ? "Çok Düşük" : "Düşük"
                    })
                    .ToListAsync();

                // 2. Today's Jobs
                dto.TodaysJobs = await _context.ServiceJobs
                    .Include(j => j.Customer)
                    .Where(j => j.ScheduledDate.HasValue && 
                               j.ScheduledDate.Value.Date == today &&
                               j.Status != JobStatus.Completed)
                    .OrderBy(j => j.ScheduledDate)
                    .Take(10)
                    .Select(j => new KamatekCrm.Shared.Models.TodayJobItemDto
                    {
                        JobId = j.Id,
                        CustomerName = j.Customer != null ? j.Customer.FullName : "Bilinmeyen Müşteri",
                        Category = j.JobCategory.ToString(), // UI will translate enum to icon
                        ScheduledTime = j.ScheduledDate.HasValue ? j.ScheduledDate.Value.ToString("HH:mm") : "--:--",
                        Priority = j.Priority.ToString(),
                        Address = j.Customer != null ? j.Customer.FullAddress ?? "" : ""
                    })
                    .ToListAsync();

                // 3. Ready Repairs
                dto.ReadyToDeliverRepairs = await _context.ServiceJobs
                    .Include(j => j.Customer)
                    .Where(j => j.WorkOrderType == WorkOrderType.Repair && 
                               j.RepairStatus == RepairStatus.ReadyForPickup)
                    .OrderBy(j => j.CreatedDate)
                    .Take(10)
                    .Select(j => new KamatekCrm.Shared.Models.ReadyRepairItemDto
                    {
                        JobId = j.Id,
                        TicketNo = $"T-{j.Id}",
                        CustomerName = j.Customer != null ? j.Customer.FullName : "Bilinmeyen Müşteri",
                        DeviceInfo = $"{j.DeviceBrand} {j.DeviceModel}",
                        DaysWaiting = j.CompletedDate.HasValue ? (DateTime.UtcNow - j.CompletedDate.Value).Days : (DateTime.UtcNow - j.CreatedDate).Days,
                        CustomerPhone = j.Customer != null ? j.Customer.PhoneNumber ?? "" : ""
                    })
                    .ToListAsync();

                // 4. Financials
                dto.Financials.MonthlySalesTotal = await _context.SalesOrders
                    .Where(o => o.Date >= startOfMonth && o.Date < endOfMonth)
                    .SumAsync(o => o.TotalAmount);
                    
                dto.Financials.MonthlySalesCount = await _context.SalesOrders
                    .CountAsync(o => o.Date >= startOfMonth && o.Date < endOfMonth);
                    
                dto.Financials.MonthlyJobsCompleted = await _context.ServiceJobs
                    .CountAsync(j => j.CompletedDate.HasValue && 
                               j.CompletedDate.Value >= startOfMonth && 
                               j.CompletedDate.Value < endOfMonth);
                               
                dto.Financials.ActiveJobsCount = await _context.ServiceJobs
                    .CountAsync(j => j.Status != JobStatus.Completed);

                dto.Financials.DailyIncome = await _context.CashTransactions
                    .Where(t => t.Date.Date >= today && 
                        (t.TransactionType == CashTransactionType.CashIncome || 
                         t.TransactionType == CashTransactionType.CardIncome || 
                         t.TransactionType == CashTransactionType.TransferIncome))
                    .SumAsync(t => t.Amount);

                dto.Financials.DailyExpense = await _context.CashTransactions
                    .Where(t => t.Date.Date >= today && 
                        (t.TransactionType == CashTransactionType.Expense || 
                         t.TransactionType == CashTransactionType.TransferExpense))
                    .SumAsync(t => t.Amount);

                // 5. Customer Stats
                dto.CustomerStats.TotalCustomers = await _context.Customers.CountAsync();
                dto.CustomerStats.NewCustomersThisMonth = await _context.Customers.CountAsync(c => c.CreatedDate >= startOfMonth);
                dto.CustomerStats.VipCustomers = await _context.Customers.CountAsync(c => c.LoyaltyPoints >= 500);

                var customersWithBday = await _context.Customers
                    .Where(c => c.BirthDate.HasValue)
                    .ToListAsync();
                    
                var upcomingList = customersWithBday
                    .Where(c =>
                    {
                        if (!c.BirthDate.HasValue) return false;
                        var bday = c.BirthDate.Value;
                        var thisYearBirthday = new DateTime(today.Year, bday.Month, bday.Day);
                        var daysUntil = (thisYearBirthday - today).Days;
                        if (daysUntil < 0) daysUntil += 365; // Next year's birthday if passed
                        return daysUntil >= 0 && daysUntil <= 30;
                    })
                    .OrderBy(c => {
                        if (!c.BirthDate.HasValue) return 999;
                        var bday = c.BirthDate.Value;
                        var thisYear = new DateTime(today.Year, bday.Month, bday.Day);
                        var days = (thisYear - today).Days;
                        return days < 0 ? days + 365 : days;
                    })
                    .Take(10)
                    .ToList();
                    
                dto.CustomerStats.BirthdayCustomers = upcomingList;
                dto.CustomerStats.UpcomingBirthdays = upcomingList.Count;

                // 6. Sales Reports
                var todaySales = await _context.SalesOrders.Where(o => o.Date.Date == today).ToListAsync();
                dto.SalesReports.TodaySalesTotal = todaySales.Sum(o => o.TotalAmount);
                dto.SalesReports.TodaySalesCount = todaySales.Count;
                
                dto.SalesReports.WeekSalesTotal = await _context.SalesOrders
                    .Where(o => o.Date.Date >= startOfWeek)
                    .SumAsync(o => o.TotalAmount);
                    
                var allSalesCount = await _context.SalesOrders.CountAsync();
                if (allSalesCount > 0)
                {
                    dto.SalesReports.AverageSaleAmount = await _context.SalesOrders.AverageAsync(o => o.TotalAmount);
                }

                // 7. Chart Data
                // Weekly Trend
                var weeklyTrendList = new List<KamatekCrm.Shared.Models.WeeklyTrendItemDto>();
                for (int i = 6; i >= 0; i--)
                {
                    var date = today.AddDays(-i);
                    var dailyIncome = await _context.CashTransactions
                        .Where(t => t.Date.Date == date && 
                            (t.TransactionType == CashTransactionType.CashIncome || 
                             t.TransactionType == CashTransactionType.CardIncome || 
                             t.TransactionType == CashTransactionType.TransferIncome))
                        .SumAsync(t => t.Amount);

                    var dailyJobs = await _context.ServiceJobs
                        .CountAsync(j => j.CompletedDate.HasValue && j.CompletedDate.Value.Date == date);

                    weeklyTrendList.Add(new KamatekCrm.Shared.Models.WeeklyTrendItemDto
                    {
                        DayName = date.ToString("ddd", new System.Globalization.CultureInfo("tr-TR")),
                        Income = dailyIncome,
                        CompletedJobs = dailyJobs
                    });
                }
                dto.ChartData.WeeklyTrend = weeklyTrendList;

                // Job Category Distribution
                dto.ChartData.JobCategoryDistribution = await _context.ServiceJobs
                    .Where(j => j.Status != JobStatus.Completed)
                    .GroupBy(j => j.JobCategory)
                    .Select(g => new KamatekCrm.Shared.Models.JobCategoryItemDto
                    {
                        Category = g.Key.ToString(),
                        Count = g.Count()
                    })
                    .ToListAsync();

                return dto;
            }, CacheService.DashboardTtl);

            return Ok(ApiResponse<KamatekCrm.Shared.Models.DashboardSummaryDto>.Ok(summary!));
        }

        /// <summary>
        /// Haftalık trend — 30s cache
        /// </summary>
        [HttpGet("weekly-trend")]
        public async Task<IActionResult> GetWeeklyTrend()
        {
            var result = await _cache.GetOrCreateAsync(CacheKeys.DashboardWeeklyTrend, async () =>
            {
                var today = DateTime.UtcNow.Date;
                var weekAgo = today.AddDays(-6);

                var dailyJobs = await _context.ServiceJobs
                    .Where(j => j.CreatedDate >= weekAgo)
                    .GroupBy(j => j.CreatedDate.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        Total = g.Count(),
                        Completed = g.Count(j => j.Status == JobStatus.Completed)
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                return Enumerable.Range(0, 7)
                    .Select(i =>
                    {
                        var date = weekAgo.AddDays(i);
                        var data = dailyJobs.FirstOrDefault(d => d.Date == date);
                        return new
                        {
                            Date = date.ToString("dd MMM"),
                            DayName = date.ToString("ddd"),
                            Total = data?.Total ?? 0,
                            Completed = data?.Completed ?? 0
                        };
                    })
                    .ToList();
            }, CacheService.DashboardTtl);

            return Ok(ApiResponse<object>.Ok(result!));
        }

        /// <summary>
        /// İş kategorisi dağılımı — 30s cache
        /// </summary>
        [HttpGet("job-distribution")]
        public async Task<IActionResult> GetJobDistribution()
        {
            var result = await _cache.GetOrCreateAsync(CacheKeys.DashboardJobDistribution, async () =>
            {
                return await _context.ServiceJobs
                    .Where(j => j.Status != JobStatus.Cancelled)
                    .GroupBy(j => j.JobCategory)
                    .Select(g => new
                    {
                        Category = g.Key.ToString(),
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync();
            }, CacheService.DashboardTtl);

            return Ok(ApiResponse<object>.Ok(result!));
        }

        /// <summary>
        /// Durum dağılımı — 30s cache
        /// </summary>
        [HttpGet("status-distribution")]
        public async Task<IActionResult> GetStatusDistribution()
        {
            var result = await _cache.GetOrCreateAsync(CacheKeys.DashboardStatusDistribution, async () =>
            {
                return await _context.ServiceJobs
                    .GroupBy(j => j.Status)
                    .Select(g => new
                    {
                        Status = g.Key.ToString(),
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync();
            }, CacheService.DashboardTtl);

            return Ok(ApiResponse<object>.Ok(result!));
        }

        /// <summary>
        /// Dashboard cache'ini temizle (admin yenile butonu)
        /// </summary>
        [HttpPost("invalidate-cache")]
        public IActionResult InvalidateCache()
        {
            _cache.RemoveByPrefix("dashboard:");
            return Ok(ApiResponse.Ok("Dashboard cache temizlendi."));
        }
    }
}
