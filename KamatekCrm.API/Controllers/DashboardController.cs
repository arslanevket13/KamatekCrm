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
