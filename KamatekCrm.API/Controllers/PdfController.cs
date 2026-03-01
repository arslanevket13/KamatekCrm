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
    public class PdfController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPdfReportService _pdf;

        public PdfController(AppDbContext context, IPdfReportService pdf)
        {
            _context = context;
            _pdf = pdf;
        }

        /// <summary>Servis işi PDF raporu (iş formu)</summary>
        [HttpGet("service-job/{id}")]
        public async Task<IActionResult> GenerateServiceJobPdf(int id)
        {
            var job = await _context.ServiceJobs
                .Include(j => j.Customer)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null) return NotFound();

            var parts = await _context.ServiceJobItems
                .Where(si => si.ServiceJobId == id)
                .Include(si => si.Product)
                .ToListAsync();

            var data = new ServiceJobPdfData
            {
                JobId = job.Id,
                Title = job.Title,
                Description = job.Description ?? "",
                Status = job.Status.ToString(),
                Priority = job.Priority.ToString(),
                CreatedDate = job.CreatedDate,
                CompletedDate = job.CompletedDate,
                CustomerName = job.Customer?.FullName ?? "",
                CustomerPhone = job.Customer?.PhoneNumber ?? "",
                CustomerAddress = job.Customer?.FullAddress ?? "",
                TechnicianNote = null,
                Parts = parts.Select(p => new PartLineItem
                {
                    Name = p.Product?.ProductName ?? "Bilinmiyor",
                    Quantity = p.QuantityUsed,
                    UnitPrice = p.UnitPrice
                }).ToList(),
                PartsTotal = parts.Sum(p => p.QuantityUsed * p.UnitPrice),
                LaborCost = job.LaborCost,
                Discount = job.DiscountAmount,
                GrandTotal = job.Price + job.LaborCost - job.DiscountAmount
            };

            var bytes = _pdf.GenerateServiceJobReport(data);
            return File(bytes, "application/pdf", $"servis_is_{id}.pdf");
        }

        /// <summary>Fatura PDF</summary>
        [HttpGet("invoice/{orderId}")]
        public async Task<IActionResult> GenerateInvoicePdf(int orderId)
        {
            var order = await _context.SalesOrders
                .Include(s => s.Customer)
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == orderId);

            if (order == null) return NotFound();

            var data = new InvoicePdfData
            {
                InvoiceNumber = order.OrderNumber,
                Date = order.Date,
                DueDate = order.Date.AddDays(30),
                CustomerName = order.Customer?.FullName ?? order.CustomerName,
                CustomerAddress = order.Customer?.FullAddress ?? "",
                CustomerPhone = order.Customer?.PhoneNumber ?? "",
                TaxNumber = null,
                Lines = order.Items.Select(item => new InvoiceLine
                {
                    Description = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    VatRate = item.TaxRate,
                    VatAmount = item.UnitPrice * item.Quantity * item.TaxRate / 100m,
                    LineTotal = item.LineTotal
                }).ToList(),
                SubTotal = order.SubTotal,
                VatTotal = order.TaxTotal,
                DiscountTotal = order.DiscountTotal,
                GrandTotal = order.TotalAmount,
                Notes = order.Notes
            };

            var bytes = _pdf.GenerateInvoice(data);
            return File(bytes, "application/pdf", $"fatura_{order.OrderNumber}.pdf");
        }

        /// <summary>Aylık özet PDF raporu</summary>
        [HttpGet("monthly-summary")]
        public async Task<IActionResult> GenerateMonthlySummaryPdf(
            [FromQuery] int? year, [FromQuery] int? month)
        {
            var y = year ?? DateTime.UtcNow.Year;
            var m = month ?? DateTime.UtcNow.Month;
            var monthStart = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);
            var monthName = new System.Globalization.CultureInfo("tr-TR").DateTimeFormat.GetMonthName(m);

            var salesRevenue = await _context.SalesOrders
                .Where(s => s.Date >= monthStart && s.Date < monthEnd && s.Status == SalesOrderStatus.Completed)
                .SumAsync(s => s.TotalAmount);

            var serviceRevenue = await _context.ServiceJobs
                .Where(j => j.CompletedDate >= monthStart && j.CompletedDate < monthEnd && j.Status == JobStatus.Completed)
                .SumAsync(j => j.Price + j.LaborCost - j.DiscountAmount);

            var expenses = await _context.PurchaseInvoices
                .Where(p => p.Date >= monthStart && p.Date < monthEnd)
                .SumAsync(p => p.GrandTotal);

            var totalRevenue = salesRevenue + serviceRevenue;

            var topProducts = await _context.SalesOrderItems
                .Where(si => _context.SalesOrders.Any(s => s.Id == si.SalesOrderId && s.Date >= monthStart && s.Date < monthEnd))
                .GroupBy(si => si.ProductName)
                .Select(g => new ProductSummaryItem
                {
                    Name = g.Key,
                    Quantity = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.LineTotal)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(10)
                .ToListAsync();

            var data = new MonthlySummaryPdfData
            {
                Year = y,
                MonthName = monthName.ToUpperInvariant(),
                TotalRevenue = totalRevenue,
                TotalExpenses = expenses,
                NetProfit = totalRevenue - expenses,
                TotalJobs = await _context.ServiceJobs.CountAsync(j => j.CreatedDate >= monthStart && j.CreatedDate < monthEnd),
                CompletedJobs = await _context.ServiceJobs.CountAsync(j => j.Status == JobStatus.Completed && j.CompletedDate >= monthStart && j.CompletedDate < monthEnd),
                NewCustomers = await _context.Customers.CountAsync(c => c.CreatedDate >= monthStart && c.CreatedDate < monthEnd),
                TopProducts = topProducts
            };

            var bytes = _pdf.GenerateMonthlySummary(data);
            return File(bytes, "application/pdf", $"aylik_ozet_{y}_{m:D2}.pdf");
        }
    }
}
