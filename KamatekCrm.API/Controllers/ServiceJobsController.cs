using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Data;
using Microsoft.AspNetCore.Authorization;

namespace KamatekCrm.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ServiceJobsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ServiceJobsController> _logger;

        public ServiceJobsController(AppDbContext context, ILogger<ServiceJobsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Tüm servis işlerini listele (sayfalı, filtrelenebilir, sıralanabilir)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceJob>>> GetServiceJobs(
            [FromQuery] string? search,
            [FromQuery] JobStatus? status,
            [FromQuery] WorkOrderType? type,
            [FromQuery] int? customerId,
            [FromQuery] int? assignedUserId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? sortBy = "CreatedDate",
            [FromQuery] string? sortDir = "desc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _context.ServiceJobs
                .Include(s => s.Customer)
                .Include(s => s.AssignedUser)
                .AsQueryable();

            if (type.HasValue)
                query = query.Where(s => s.WorkOrderType == type.Value);

            // Arama filtresi
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(s =>
                    s.Title.ToLower().Contains(search) ||
                    s.Description.ToLower().Contains(search) ||
                    (s.Customer != null && s.Customer.FullName.ToLower().Contains(search)));
            }

            // Durum filtresi
            if (status.HasValue)
                query = query.Where(s => s.Status == status.Value);

            // Müşteri filtresi
            if (customerId.HasValue)
                query = query.Where(s => s.CustomerId == customerId.Value);

            // Teknisyen filtresi
            if (assignedUserId.HasValue)
                query = query.Where(s => s.AssignedUserId == assignedUserId.Value);

            // Tarih aralığı
            if (startDate.HasValue)
                query = query.Where(s => s.CreatedDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(s => s.CreatedDate <= endDate.Value);

            // Sıralama
            query = sortBy?.ToLower() switch
            {
                "title" => sortDir == "asc" ? query.OrderBy(s => s.Title) : query.OrderByDescending(s => s.Title),
                "status" => sortDir == "asc" ? query.OrderBy(s => s.Status) : query.OrderByDescending(s => s.Status),
                "customer" => sortDir == "asc" ? query.OrderBy(s => s.Customer!.FullName) : query.OrderByDescending(s => s.Customer!.FullName),
                _ => sortDir == "asc" ? query.OrderBy(s => s.CreatedDate) : query.OrderByDescending(s => s.CreatedDate)
            };

            var total = await query.CountAsync();
            var jobs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            Response.Headers.Append("X-Total-Count", total.ToString());
            Response.Headers.Append("X-Page", page.ToString());
            Response.Headers.Append("X-PageSize", pageSize.ToString());

            return Ok(jobs);
        }

        /// <summary>
        /// Tek servis işi detayı
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceJob>> GetServiceJob(int id)
        {
            var job = await _context.ServiceJobs
                .Include(s => s.Customer)
                .Include(s => s.AssignedUser)
                .Include(s => s.ServiceJobItems)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (job == null) return NotFound(new { Message = $"ServiceJob #{id} bulunamadı." });
            return Ok(job);
        }

        /// <summary>
        /// Servis işi tarihçesini getir
        /// </summary>
        [HttpGet("{id}/history")]
        public async Task<ActionResult<IEnumerable<ServiceJobHistory>>> GetServiceJobHistory(int id)
        {
            var histories = await _context.ServiceJobHistories
                .Where(h => h.ServiceJobId == id)
                .OrderByDescending(h => h.Date)
                .ToListAsync();

            return Ok(histories);
        }

        /// <summary>
        /// Servis işine tarihçe / not ekle
        /// </summary>
        [HttpPost("{id}/history")]
        public async Task<IActionResult> AddServiceJobHistory(int id, ServiceJobHistory history)
        {
            if (id != history.ServiceJobId) history.ServiceJobId = id;

            var job = await _context.ServiceJobs.FindAsync(id);
            if (job == null) return NotFound();

            if (history.Date == default) history.Date = DateTime.UtcNow;
            if (history.PerformedAt == default) history.PerformedAt = DateTime.UtcNow;
            history.PerformedBy = GetCurrentUserId();

            _context.ServiceJobHistories.Add(history);
            await _context.SaveChangesAsync();

            return Ok(history);
        }

        /// <summary>
        /// Yeni servis işi oluştur
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ServiceJob>> CreateServiceJob(ServiceJob serviceJob)
        {
            try
            {
                serviceJob.CreatedDate = DateTime.UtcNow;
                serviceJob.Status = JobStatus.Pending;

                _context.ServiceJobs.Add(serviceJob);
                await _context.SaveChangesAsync();

                // Tarihçe kaydı
                _context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = serviceJob.Id,
                    JobStatusChange = JobStatus.Pending,
                    TechnicianNote = "İş kaydı oluşturuldu",
                    Action = "Created",
                    Date = DateTime.UtcNow,
                    PerformedAt = DateTime.UtcNow,
                    PerformedBy = GetCurrentUserId(),
                    UserId = GetCurrentUserId().ToString()
                });
                await _context.SaveChangesAsync();

                _logger.LogInformation("ServiceJob #{Id} oluşturuldu: {Title}", serviceJob.Id, serviceJob.Title);
                return CreatedAtAction(nameof(GetServiceJob), new { id = serviceJob.Id }, serviceJob);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ServiceJob oluşturma hatası");
                return StatusCode(500, new { Message = "Servis işi oluşturulurken bir hata oluştu." });
            }
        }

        /// <summary>
        /// Servis işi güncelle
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateServiceJob(int id, ServiceJob serviceJob)
        {
            if (id != serviceJob.Id) return BadRequest(new { Message = "ID uyuşmazlığı" });

            var existing = await _context.ServiceJobs
                .Include(s => s.ServiceJobItems)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (existing == null) return NotFound();

            var oldStatus = existing.Status;

            _context.Entry(existing).CurrentValues.SetValues(serviceJob);
            existing.ModifiedDate = DateTime.UtcNow;

            var existingItems = existing.ServiceJobItems.ToList();
            var incomingItems = serviceJob.ServiceJobItems?.ToList() ?? new List<ServiceJobItem>();

            var itemsToAdd = incomingItems.Where(i => i.Id == 0).ToList();
            var itemsToUpdate = incomingItems.Where(i => i.Id > 0).ToList();
            var itemsToRemove = existingItems.Where(e => !incomingItems.Any(i => i.Id == e.Id)).ToList();

            foreach (var item in itemsToRemove)
            {
                _context.ServiceJobItems.Remove(item);
            }

            foreach (var incoming in itemsToUpdate)
            {
                var existingItem = existingItems.FirstOrDefault(e => e.Id == incoming.Id);
                if (existingItem != null)
                {
                    _context.Entry(existingItem).CurrentValues.SetValues(incoming);
                }
            }

            foreach (var newItem in itemsToAdd)
            {
                newItem.ServiceJobId = id;
                _context.ServiceJobItems.Add(newItem);
            }

            var finalItems = await _context.ServiceJobItems.Where(i => i.ServiceJobId == id).ToListAsync();

            if (serviceJob.Status == JobStatus.Completed && oldStatus != JobStatus.Completed)
            {
                foreach (var item in finalItems)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.TotalStockQuantity -= item.QuantityUsed;
                        product.ModifiedDate = DateTime.UtcNow;
                    }
                }
            }
            else if (oldStatus == JobStatus.Completed && serviceJob.Status != JobStatus.Completed)
            {
                foreach (var item in finalItems)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.TotalStockQuantity += item.QuantityUsed;
                        product.ModifiedDate = DateTime.UtcNow;
                    }
                }
            }

            if (oldStatus != serviceJob.Status)
            {
                _context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = id,
                    JobStatusChange = serviceJob.Status,
                    TechnicianNote = $"Durum güncellendi: {oldStatus} → {serviceJob.Status}",
                    Action = "StatusChanged",
                    Date = DateTime.UtcNow,
                    PerformedAt = DateTime.UtcNow,
                    PerformedBy = GetCurrentUserId(),
                    UserId = GetCurrentUserId().ToString()
                });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("ServiceJob #{Id} güncellendi", id);
            return NoContent();
        }

        /// <summary>
        /// Durum hızlı güncelleme
        /// </summary>
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateJobStatusRequest request)
        {
            var job = await _context.ServiceJobs.FindAsync(id);
            if (job == null) return NotFound();

            var oldStatus = job.Status;
            job.Status = request.Status;
            job.ModifiedDate = DateTime.UtcNow;

            if (request.Status == JobStatus.Completed && oldStatus != JobStatus.Completed)
            {
                job.CompletedDate = DateTime.UtcNow;

                var jobItems = await _context.ServiceJobItems.Where(i => i.ServiceJobId == id).ToListAsync();
                foreach (var item in jobItems)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.TotalStockQuantity -= item.QuantityUsed;
                        product.ModifiedDate = DateTime.UtcNow;
                    }
                }
            }
            // Stok İade İşlemi (Durum Completed'dan çıkarıldığında)
            else if (oldStatus == JobStatus.Completed && request.Status != JobStatus.Completed)
            {
                var jobItems = await _context.ServiceJobItems.Where(i => i.ServiceJobId == id).ToListAsync();
                foreach (var item in jobItems)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.TotalStockQuantity += item.QuantityUsed; // Stok iadesi eklendi
                        product.ModifiedDate = DateTime.UtcNow;
                    }
                }
            }

            _context.ServiceJobHistories.Add(new ServiceJobHistory
            {
                ServiceJobId = id,
                JobStatusChange = request.Status,
                TechnicianNote = request.Notes ?? $"Durum: {oldStatus} → {request.Status}",
                Action = "StatusChanged",
                Date = DateTime.UtcNow,
                PerformedAt = DateTime.UtcNow,
                PerformedBy = GetCurrentUserId(),
                UserId = GetCurrentUserId().ToString()
            });

            await _context.SaveChangesAsync();
            return Ok(new { Success = true, NewStatus = request.Status.ToString() });
        }

        /// <summary>
        /// Servis işi sil
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteServiceJob(int id)
        {
            var job = await _context.ServiceJobs.FindAsync(id);
            if (job == null) return NotFound();

            _context.ServiceJobs.Remove(job);
            await _context.SaveChangesAsync();

            _logger.LogInformation("ServiceJob #{Id} silindi", id);
            return NoContent();
        }

        /// <summary>
        /// Servis işine parça (ürün) ekle
        /// </summary>
        [HttpPost("{id}/items")]
        public async Task<ActionResult<ServiceJobItem>> AddJobItem(int id, ServiceJobItem item)
        {
            if (id != item.ServiceJobId) item.ServiceJobId = id;

            var job = await _context.ServiceJobs.FindAsync(id);
            if (job == null) return NotFound(new { Message = "İş kaydı bulunamadı." });

            _context.ServiceJobItems.Add(item);
            await _context.SaveChangesAsync();

            // Ürünü Include ile dönmek isterseniz
            var savedItem = await _context.ServiceJobItems
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.Id == item.Id);

            return Ok(savedItem);
        }

        /// <summary>
        /// Servis işinden parça çıkar
        /// </summary>
        [HttpDelete("{id}/items/{itemId}")]
        public async Task<IActionResult> RemoveJobItem(int id, int itemId)
        {
            var item = await _context.ServiceJobItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ServiceJobId == id);
            if (item == null) return NotFound();

            _context.ServiceJobItems.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Dashboard istatistikleri
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<ServiceJobStatsResponse>> GetStats()
        {
            try
            {
                var now = DateTime.UtcNow;
                var todayStart = now.Date;

                var allJobs = await _context.ServiceJobs
                    .AsNoTracking()
                    .Select(j => new { j.Status, j.SlaDeadline, j.CreatedDate, j.CompletedDate })
                    .ToListAsync();

                var completedJobs = allJobs.Where(j => j.Status == JobStatus.Completed && j.CompletedDate.HasValue).ToList();
                double avgHours = 0;
                if (completedJobs.Any())
                {
                    avgHours = completedJobs
                        .Select(j => (j.CompletedDate!.Value - j.CreatedDate).TotalHours)
                        .Average();
                }

                var stats = new ServiceJobStatsResponse
                {
                    TotalJobs = allJobs.Count,
                    PendingJobs = allJobs.Count(j => j.Status == JobStatus.Pending),
                    InProgressJobs = allJobs.Count(j => j.Status == JobStatus.InProgress),
                    CompletedJobs = allJobs.Count(j => j.Status == JobStatus.Completed),
                    CancelledJobs = allJobs.Count(j => j.Status == JobStatus.Cancelled),
                    WaitingForPartsJobs = allJobs.Count(j => j.Status == JobStatus.WaitingForParts),
                    SlaBreachedJobs = allJobs.Count(j => j.SlaDeadline.HasValue && j.SlaDeadline.Value < now && j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled),
                    TodayCreated = allJobs.Count(j => j.CreatedDate >= todayStart),
                    AvgCompletionHours = Math.Round(avgHours, 1)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ServiceJob istatistik alma hatası");
                return StatusCode(500, new { Message = "İstatistikler alınırken hata oluştu." });
            }
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }
    }

    public class UpdateJobStatusRequest
    {
        public JobStatus Status { get; set; }
        public string? Notes { get; set; }
    }

    public class ServiceJobStatsResponse
    {
        public int TotalJobs { get; set; }
        public int PendingJobs { get; set; }
        public int InProgressJobs { get; set; }
        public int CompletedJobs { get; set; }
        public int CancelledJobs { get; set; }
        public int WaitingForPartsJobs { get; set; }
        public int SlaBreachedJobs { get; set; }
        public int TodayCreated { get; set; }
        public double AvgCompletionHours { get; set; }
    }
}
