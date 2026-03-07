using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Data;
using KamatekCrm.API.Models;
using KamatekCrm.API.Hubs;
using Microsoft.AspNetCore.Authorization;

namespace KamatekCrm.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SalesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notifications;
        private readonly KamatekCrm.API.Services.ISalesDomainService _salesDomainService;
        private readonly ILogger<SalesController> _logger;

        public SalesController(
            AppDbContext context, 
            INotificationService notifications, 
            ILogger<SalesController> logger,
            KamatekCrm.API.Services.ISalesDomainService salesDomainService)
        {
            _context = context;
            _notifications = notifications;
            _logger = logger;
            _salesDomainService = salesDomainService;
        }

        /// <summary>Satış siparişleri (filtrelenebilir, sayfalı)</summary>
        [HttpGet]
        public async Task<IActionResult> GetSalesOrders(
            [FromQuery] string? search,
            [FromQuery] int? customerId,
            [FromQuery] SalesOrderStatus? status,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _context.SalesOrders
                .Include(s => s.Customer)
                .Include(s => s.Items)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(s => s.OrderNumber.Contains(search) || s.CustomerName.Contains(search));
            if (customerId.HasValue) query = query.Where(s => s.CustomerId == customerId.Value);
            if (status.HasValue) query = query.Where(s => s.Status == status.Value);
            if (startDate.HasValue) query = query.Where(s => s.Date >= startDate.Value);
            if (endDate.HasValue) query = query.Where(s => s.Date <= endDate.Value);

            var total = await query.CountAsync();
            var orders = await query
                .OrderByDescending(s => s.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pagination = new PaginationMeta { Page = page, PageSize = pageSize, TotalCount = total };
            return Ok(ApiResponse<object>.Ok(orders, pagination));
        }

        [HttpPost]
        public async Task<IActionResult> ProcessSale([FromBody] SaleRequest request)
        {
            var result = await _salesDomainService.ProcessSaleAsync(request);
            if (result.Success)
            {
                return Ok(ApiResponse<SaleResult>.Ok(result));
            }
            
            return BadRequest(ApiResponse.Fail(result.ErrorMessage));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSalesOrder(int id)
        {
            var order = await _context.SalesOrders
                .Include(s => s.Customer)
                .Include(s => s.Items)
                .Include(s => s.Payments)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (order == null) return NotFound();
            return Ok(ApiResponse<SalesOrder>.Ok(order));
        }

        /// <summary>Günlük satış özeti (cached)</summary>
        [HttpGet("daily-summary")]
        public async Task<IActionResult> GetDailySummary([FromQuery] DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.UtcNow.Date;
            var nextDay = targetDate.AddDays(1);

            var orders = await _context.SalesOrders
                .Where(s => s.Date >= targetDate && s.Date < nextDay)
                .ToListAsync();

            var cashTx = await _context.CashTransactions
                .Where(t => t.Date >= targetDate && t.Date < nextDay)
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(new
            {
                Date = targetDate,
                TotalOrders = orders.Count,
                TotalRevenue = orders.Sum(o => o.TotalAmount),
                TotalDiscount = orders.Sum(o => o.DiscountTotal),
                TotalTax = orders.Sum(o => o.TaxTotal),
                CashIn = cashTx.Where(t => t.TransactionType == CashTransactionType.Income).Sum(t => t.Amount),
                CashOut = cashTx.Where(t => t.TransactionType == CashTransactionType.Expense).Sum(t => t.Amount),
                PaymentBreakdown = orders.GroupBy(o => o.PaymentMethod)
                    .Select(g => new { Method = g.Key, Total = g.Sum(o => o.TotalAmount) })
                    .ToList()
            }));
        }

        /// <summary>Excel export — Satış siparişleri</summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportSales(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var query = _context.SalesOrders.Include(s => s.Items).AsQueryable();
            if (startDate.HasValue) query = query.Where(s => s.Date >= startDate.Value);
            if (endDate.HasValue) query = query.Where(s => s.Date <= endDate.Value);

            var data = await query
                .OrderByDescending(s => s.Date)
                .Select(s => new
                {
                    SiparisNo = s.OrderNumber,
                    Musteri = s.CustomerName,
                    Tarih = s.Date.ToString("dd.MM.yyyy"),
                    Durum = s.Status.ToString(),
                    AraToplam = s.SubTotal,
                    Indirim = s.DiscountTotal,
                    KDV = s.TaxTotal,
                    Toplam = s.TotalAmount,
                    Kalem = s.Items.Count
                })
                .ToListAsync();

            var excelService = HttpContext.RequestServices.GetRequiredService<KamatekCrm.API.Services.IExcelService>();
            var bytes = excelService.ExportToExcel(data, "Satış Sipariş Raporu");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"satis_raporu_{DateTime.UtcNow:yyyyMMdd}.xlsx");
        }
    }
}
