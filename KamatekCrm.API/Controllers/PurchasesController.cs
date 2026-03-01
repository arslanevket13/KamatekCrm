using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Data;
using KamatekCrm.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace KamatekCrm.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PurchasesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PurchasesController> _logger;

        public PurchasesController(AppDbContext context, ILogger<PurchasesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>Satınalma siparişleri</summary>
        [HttpGet("orders")]
        public async Task<IActionResult> GetPurchaseOrders(
            [FromQuery] int? supplierId,
            [FromQuery] PurchaseStatus? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _context.PurchaseOrders
                .Include(po => po.Supplier)
                .Include(po => po.Items)
                .AsQueryable();

            if (supplierId.HasValue) query = query.Where(po => po.SupplierId == supplierId.Value);
            if (status.HasValue) query = query.Where(po => po.Status == status.Value);

            var total = await query.CountAsync();
            var orders = await query
                .OrderByDescending(po => po.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pagination = new PaginationMeta { Page = page, PageSize = pageSize, TotalCount = total };
            return Ok(ApiResponse<object>.Ok(orders, pagination));
        }

        [HttpGet("orders/{id}")]
        public async Task<IActionResult> GetPurchaseOrder(int id)
        {
            var order = await _context.PurchaseOrders
                .Include(po => po.Supplier)
                .Include(po => po.Items)
                .FirstOrDefaultAsync(po => po.Id == id);
            if (order == null) return NotFound();
            return Ok(ApiResponse<PurchaseOrder>.Ok(order));
        }

        /// <summary>Satınalma faturaları</summary>
        [HttpGet("invoices")]
        public async Task<IActionResult> GetInvoices(
            [FromQuery] int? supplierId,
            [FromQuery] PurchaseInvoicePaymentStatus? paymentStatus,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _context.PurchaseInvoices
                .Include(pi => pi.Supplier)
                .Include(pi => pi.Lines)
                .AsQueryable();

            if (supplierId.HasValue) query = query.Where(pi => pi.SupplierId == supplierId.Value);
            if (paymentStatus.HasValue) query = query.Where(pi => pi.PaymentStatus == paymentStatus.Value);

            var total = await query.CountAsync();
            var invoices = await query
                .OrderByDescending(pi => pi.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pagination = new PaginationMeta { Page = page, PageSize = pageSize, TotalCount = total };
            return Ok(ApiResponse<object>.Ok(invoices, pagination));
        }

        [HttpGet("invoices/{id}")]
        public async Task<IActionResult> GetInvoice(int id)
        {
            var invoice = await _context.PurchaseInvoices
                .Include(pi => pi.Supplier)
                .Include(pi => pi.Lines).ThenInclude(l => l.Product)
                .FirstOrDefaultAsync(pi => pi.Id == id);
            if (invoice == null) return NotFound();
            return Ok(ApiResponse<PurchaseInvoice>.Ok(invoice));
        }

        /// <summary>Ödeme kaydet — fatura ödeme durumunu güncelle</summary>
        [HttpPost("invoices/{id}/payment")]
        public async Task<IActionResult> RecordPayment(int id, [FromBody] PaymentRequest request)
        {
            var invoice = await _context.PurchaseInvoices.FindAsync(id);
            if (invoice == null) return NotFound();

            invoice.PaidAmount += request.Amount;
            invoice.RemainingAmount = invoice.GrandTotal - invoice.PaidAmount;

            if (invoice.RemainingAmount <= 0)
            {
                invoice.PaymentStatus = PurchaseInvoicePaymentStatus.Paid;
                invoice.RemainingAmount = 0;
            }
            else if (invoice.PaidAmount > 0)
            {
                invoice.PaymentStatus = PurchaseInvoicePaymentStatus.PartiallyPaid;
            }

            invoice.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment recorded for Invoice #{Id}: {Amount}", id, request.Amount);
            return Ok(ApiResponse<object>.Ok(new
            {
                InvoiceId = id,
                PaidAmount = invoice.PaidAmount,
                RemainingAmount = invoice.RemainingAmount,
                PaymentStatus = invoice.PaymentStatus.ToString()
            }));
        }

        /// <summary>Tedarikçi bazlı ödeme özeti</summary>
        [HttpGet("payment-summary")]
        public async Task<IActionResult> GetPaymentSummary()
        {
            var summary = await _context.PurchaseInvoices
                .GroupBy(pi => pi.Supplier.Name)
                .Select(g => new
                {
                    Supplier = g.Key,
                    TotalInvoiced = g.Sum(pi => pi.GrandTotal),
                    TotalPaid = g.Sum(pi => pi.PaidAmount),
                    TotalRemaining = g.Sum(pi => pi.RemainingAmount),
                    InvoiceCount = g.Count(),
                    UnpaidCount = g.Count(pi => pi.PaymentStatus != PurchaseInvoicePaymentStatus.Paid)
                })
                .OrderByDescending(x => x.TotalRemaining)
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(summary));
        }
    }

    public class PaymentRequest
    {
        public decimal Amount { get; set; }
        public string? Notes { get; set; }
    }
}
