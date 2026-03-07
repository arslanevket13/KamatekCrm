using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Models;
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
    public class CustomersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICacheService _cache;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(AppDbContext context, ICacheService cache, ILogger<CustomersController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>Müşteri listesi — aranabilir, filtrelenebilir, sayfalı</summary>
        [HttpGet]
        public async Task<IActionResult> GetCustomers(
            [FromQuery] string? search,
            [FromQuery] CustomerType? type,
            [FromQuery] CustomerSegment? segment,
            [FromQuery] string? city,
            [FromQuery] string? sortBy = "FullName",
            [FromQuery] string? sortDir = "asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _context.Customers.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(c =>
                    c.FullName.ToLower().Contains(search) ||
                    c.PhoneNumber.Contains(search) ||
                    (c.Email != null && c.Email.ToLower().Contains(search)) ||
                    c.City.ToLower().Contains(search));
            }

            if (segment.HasValue) query = query.Where(c => c.Segment == segment.Value);
            if (type.HasValue) query = query.Where(c => c.Type == type.Value);
            if (!string.IsNullOrEmpty(city)) query = query.Where(c => c.City == city);

            query = sortBy?.ToLower() switch
            {
                "totalspent" => sortDir == "asc" ? query.OrderBy(c => c.TotalSpent) : query.OrderByDescending(c => c.TotalSpent),
                "createdate" => sortDir == "asc" ? query.OrderBy(c => c.CreatedDate) : query.OrderByDescending(c => c.CreatedDate),
                "lastservice" => sortDir == "asc" ? query.OrderBy(c => c.LastInteractionDate) : query.OrderByDescending(c => c.LastInteractionDate),
                _ => sortDir == "asc" ? query.OrderBy(c => c.FullName) : query.OrderByDescending(c => c.FullName)
            };

            var total = await query.CountAsync();
            var customers = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var pagination = new PaginationMeta { Page = page, PageSize = pageSize, TotalCount = total };
            return Ok(ApiResponse<object>.Ok(customers, pagination));
        }

        /// <summary>Genel müşteri istatistiklerini getirir</summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetCustomerStats()
        {
            var total = await _context.Customers.CountAsync();
            var individual = await _context.Customers.CountAsync(c => c.Type == CustomerType.Individual);
            var corporate = await _context.Customers.CountAsync(c => c.Type == CustomerType.Corporate);
            var walkIn = await _context.Customers.CountAsync(c => c.Type == CustomerType.WalkIn);

            return Ok(ApiResponse<object>.Ok(new
            {
                TotalCustomers = total,
                IndividualCount = individual,
                CorporateCount = corporate,
                WalkInCount = walkIn
            }));
        }

        /// <summary>Müşteri detay — iş geçmişi dahil</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCustomer(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();

            var jobs = await _context.ServiceJobs
                .Where(j => j.CustomerId == id)
                .OrderByDescending(j => j.CreatedDate)
                .Take(10)
                .Select(j => new
                {
                    j.Id, j.Title, j.Status, j.Priority,
                    j.CreatedDate, j.CompletedDate,
                    j.Price, j.LaborCost
                })
                .ToListAsync();

            var salesOrders = await _context.SalesOrders
                .Where(s => s.CustomerId == id)
                .OrderByDescending(s => s.Date)
                .Take(10)
                .Select(s => new { s.Id, s.OrderNumber, s.Date, s.TotalAmount, s.Status })
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(new
            {
                Customer = customer,
                RecentJobs = jobs,
                RecentOrders = salesOrders,
                Statistics = new
                {
                    customer.TotalSpent,
                    TotalJobs = await _context.ServiceJobs.CountAsync(j => j.CustomerId == id),
                    TotalOrders = await _context.SalesOrders.CountAsync(s => s.CustomerId == id),
                    OpenJobs = await _context.ServiceJobs.CountAsync(j =>
                        j.CustomerId == id && j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled)
                }
            }));
        }

        [HttpPost]
        public async Task<IActionResult> CreateCustomer(Customer customer)
        {
            if (string.IsNullOrWhiteSpace(customer.CustomerCode))
            {
                int year = DateTime.UtcNow.Year;
                int count = await _context.Customers.CountAsync(c => c.CustomerCode != null && c.CustomerCode.StartsWith($"MŞ-{year}-"));
                customer.CustomerCode = $"MŞ-{year}-{(count + 1):D4}";
            }

            customer.CreatedDate = DateTime.UtcNow;
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            _cache.RemoveByPrefix("lists:customers");
            _logger.LogInformation("Customer #{Id} created: {Name}", customer.Id, customer.FullName);
            return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, ApiResponse<Customer>.Ok(customer));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, Customer customer)
        {
            if (id != customer.Id) return BadRequest();
            var existing = await _context.Customers.FindAsync(id);
            if (existing == null) return NotFound();
            _context.Entry(existing).CurrentValues.SetValues(customer);
            existing.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _cache.RemoveByPrefix("lists:customers");
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();
            // soft-delete handled by global query filter
            customer.IsDeleted = true;
            customer.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _cache.RemoveByPrefix("lists:customers");
            return NoContent();
        }

        /// <summary>Müşterinin cihazları</summary>
        [HttpGet("{id}/assets")]
        public async Task<IActionResult> GetCustomerAssets(int id)
        {
            var assets = await _context.CustomerAssets
                .Where(a => a.CustomerId == id)
                .OrderBy(a => a.Category)
                .ThenBy(a => a.Brand)
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(assets));
        }

        /// <summary>Müşteriye yeni cihaz ekle</summary>
        [HttpPost("{id}/assets")]
        public async Task<IActionResult> AddCustomerAsset(int id, [FromBody] CustomerAsset asset)
        {
            if (id != asset.CustomerId)
            {
                asset.CustomerId = id;
            }

            try
            {
                if (asset.CreatedDate == default)
                {
                    asset.CreatedDate = DateTime.UtcNow;
                }

                _context.CustomerAssets.Add(asset);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Yeni cihaz eklendi: {Model} (Müşteri: {Id})", asset.Model, id);
                return Ok(ApiResponse<CustomerAsset>.Ok(asset));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cihaz eklenirken hata: {Id}", id);
                return StatusCode(500, ApiResponse<object>.Fail($"Cihaz eklenirken hata oluştu: {ex.Message}"));
            }
        }

        /// <summary>Müşterinin projeleri</summary>
        [HttpGet("{id}/projects")]
        public async Task<IActionResult> GetCustomerProjects(int id)
        {
            try
            {
                var projects = await _context.ServiceProjects
                    .Where(p => p.CustomerId == id &&
                               (p.Status == ProjectStatus.Draft ||
                                p.Status == ProjectStatus.Active ||
                                p.Status == ProjectStatus.PendingApproval))
                    .OrderByDescending(p => p.CreatedDate)
                    .ToListAsync();

                return Ok(ApiResponse<object>.Ok(projects));
            }
            catch
            {
                // Fallback for when ServiceProjects table might not be initialized
                return Ok(ApiResponse<object>.Ok(new List<ServiceProject>()));
            }
        }

        /// <summary>Müşteri notları / etkinlik timeline'ı</summary>
        [HttpGet("{id}/notes")]
        public async Task<IActionResult> GetNotes(int id)
        {
            var notes = await _context.CustomerNotes
                .Where(n => n.CustomerId == id)
                .OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.CreatedAt)
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(notes));
        }

        /// <summary>Müşteri notu ekle</summary>
        [HttpPost("{id}/notes")]
        public async Task<IActionResult> AddNote(int id, [FromBody] CustomerNote note)
        {
            note.CustomerId = id;
            note.CreatedAt = DateTime.UtcNow;
            _context.CustomerNotes.Add(note);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Note added to Customer #{Id}: {Type}", id, note.ActivityType);
            return Ok(ApiResponse<CustomerNote>.Ok(note));
        }

        /// <summary>Müşteri segmentasyon analizi — RFM scoring</summary>
        [HttpGet("analytics/rfm")]
        public async Task<IActionResult> GetRfmAnalysis()
        {
            var now = DateTime.UtcNow;
            var customers = await _context.Customers
                .Select(c => new
                {
                    c.Id,
                    c.FullName,
                    c.Segment,
                    c.TotalSpent,
                    c.LastInteractionDate,
                    JobCount = _context.ServiceJobs.Count(j => j.CustomerId == c.Id)
                })
                .ToListAsync();

            var rfmResults = customers.Select(c =>
            {
                // Recency (son işlem ne kadar yeni) — 1-5 puan
                var daysSinceLastService = c.LastInteractionDate.HasValue
                    ? (now - c.LastInteractionDate.Value).TotalDays : 365;
                var recency = daysSinceLastService < 30 ? 5
                    : daysSinceLastService < 90 ? 4
                    : daysSinceLastService < 180 ? 3
                    : daysSinceLastService < 365 ? 2 : 1;

                // Frequency (sıklık) — 1-5 puan
                var frequency = c.JobCount >= 10 ? 5
                    : c.JobCount >= 5 ? 4
                    : c.JobCount >= 3 ? 3
                    : c.JobCount >= 1 ? 2 : 1;

                // Monetary (harcama) — 1-5 puan
                var monetary = c.TotalSpent >= 50000 ? 5
                    : c.TotalSpent >= 20000 ? 4
                    : c.TotalSpent >= 5000 ? 3
                    : c.TotalSpent >= 1000 ? 2 : 1;

                var rfmScore = recency + frequency + monetary;
                var rfmSegment = rfmScore >= 13 ? "Champions"
                    : rfmScore >= 10 ? "Loyal"
                    : rfmScore >= 7 ? "Potential"
                    : rfmScore >= 4 ? "At Risk"
                    : "Hibernating";

                return new
                {
                    c.Id,
                    c.FullName,
                    Recency = recency,
                    Frequency = frequency,
                    Monetary = monetary,
                    RfmScore = rfmScore,
                    RfmSegment = rfmSegment,
                    c.TotalSpent,
                    c.JobCount,
                    DaysSinceLastService = (int)daysSinceLastService
                };
            })
            .OrderByDescending(x => x.RfmScore)
            .ToList();

            var segmentSummary = rfmResults.GroupBy(r => r.RfmSegment)
                .Select(g => new
                {
                    Segment = g.Key,
                    Count = g.Count(),
                    AvgSpent = g.Average(x => x.TotalSpent),
                    TotalSpent = g.Sum(x => x.TotalSpent)
                })
                .OrderByDescending(x => x.TotalSpent)
                .ToList();

            return Ok(ApiResponse<object>.Ok(new
            {
                Customers = rfmResults,
                SegmentSummary = segmentSummary,
                TotalCustomers = rfmResults.Count
            }));
        }

        /// <summary>Müşteri şehir dağılımı</summary>
        [HttpGet("analytics/cities")]
        public async Task<IActionResult> GetCityDistribution()
        {
            var distribution = await _context.Customers
                .Where(c => !string.IsNullOrEmpty(c.City))
                .GroupBy(c => c.City)
                .Select(g => new
                {
                    City = g.Key,
                    Count = g.Count(),
                    TotalSpent = g.Sum(c => c.TotalSpent)
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(distribution));
        }

        /// <summary>Kayıp müşteri tespiti (churn risk)</summary>
        [HttpGet("analytics/churn-risk")]
        public async Task<IActionResult> GetChurnRisk()
        {
            var cutoff = DateTime.UtcNow.AddMonths(-6);

            var atRisk = await _context.Customers
                .Where(c => c.TotalSpent > 0
                    && (c.LastInteractionDate == null || c.LastInteractionDate < cutoff))
                .OrderByDescending(c => c.TotalSpent)
                .Select(c => new
                {
                    c.Id,
                    c.FullName,
                    c.PhoneNumber,
                    c.TotalSpent,
                    c.LastInteractionDate,
                    DaysSinceLastService = c.LastInteractionDate.HasValue
                        ? (int)(DateTime.UtcNow - c.LastInteractionDate.Value).TotalDays : 999,
                    RiskLevel = !c.LastInteractionDate.HasValue || c.LastInteractionDate < DateTime.UtcNow.AddYears(-1)
                        ? "Yüksek" : "Orta"
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(new
            {
                AtRiskCustomers = atRisk,
                TotalAtRisk = atRisk.Count,
                HighRisk = atRisk.Count(x => x.RiskLevel == "Yüksek"),
                PotentialLostRevenue = atRisk.Sum(x => x.TotalSpent)
            }));
        }
    }
}
