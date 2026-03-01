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
    public class FinanceController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICacheService _cache;

        public FinanceController(AppDbContext context, ICacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        /// <summary>Kasa durumu — anlık nakit/kart/çek toplamları</summary>
        [HttpGet("cash-position")]
        public async Task<IActionResult> GetCashPosition()
        {
            var result = await _cache.GetOrCreateAsync("dashboard:cashposition", async () =>
            {
                var transactions = await _context.CashTransactions.ToListAsync();

                var cash = transactions.Where(t => t.PaymentMethod == PaymentMethod.Cash)
                    .Sum(t => t.TransactionType == CashTransactionType.Income ? t.Amount : -t.Amount);
                var card = transactions.Where(t => t.PaymentMethod == PaymentMethod.CreditCard)
                    .Sum(t => t.TransactionType == CashTransactionType.Income ? t.Amount : -t.Amount);

                return new
                {
                    CashBalance = cash,
                    CardBalance = card,
                    TotalBalance = cash + card,
                    TodaysIncome = transactions
                        .Where(t => t.Date.Date == DateTime.UtcNow.Date && t.TransactionType == CashTransactionType.Income)
                        .Sum(t => t.Amount),
                    TodaysExpense = transactions
                        .Where(t => t.Date.Date == DateTime.UtcNow.Date && t.TransactionType == CashTransactionType.Expense)
                        .Sum(t => t.Amount),
                    GeneratedAt = DateTime.UtcNow
                };
            }, CacheService.DashboardTtl);

            return Ok(ApiResponse<object>.Ok(result!));
        }

        /// <summary>Kasa hareketleri (filtrelenebilir)</summary>
        [HttpGet("cash-transactions")]
        public async Task<IActionResult> GetCashTransactions(
            [FromQuery] CashTransactionType? type,
            [FromQuery] PaymentMethod? paymentMethod,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _context.CashTransactions
                .Include(t => t.Customer)
                .AsQueryable();

            if (type.HasValue) query = query.Where(t => t.TransactionType == type.Value);
            if (paymentMethod.HasValue) query = query.Where(t => t.PaymentMethod == paymentMethod.Value);
            if (startDate.HasValue) query = query.Where(t => t.Date >= startDate.Value);
            if (endDate.HasValue) query = query.Where(t => t.Date <= endDate.Value);

            var total = await query.CountAsync();
            var transactions = await query
                .OrderByDescending(t => t.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pagination = new PaginationMeta { Page = page, PageSize = pageSize, TotalCount = total };
            return Ok(ApiResponse<object>.Ok(transactions, pagination));
        }

        /// <summary>Kasa hareketi oluştur</summary>
        [HttpPost("cash-transactions")]
        public async Task<IActionResult> CreateCashTransaction(CashTransaction transaction)
        {
            transaction.CreatedAt = DateTime.UtcNow;
            transaction.Date = DateTime.UtcNow;
            _context.CashTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            _cache.RemoveByPrefix("dashboard:cashposition");
            return Ok(ApiResponse<CashTransaction>.Ok(transaction));
        }

        /// <summary>Alacak-verecek özeti — tedarikçi ve müşteri bakiye</summary>
        [HttpGet("accounts-summary")]
        public async Task<IActionResult> GetAccountsSummary()
        {
            var receivable = await _context.Customers
                .Where(c => c.TotalSpent > 0)
                .SumAsync(c => c.TotalSpent);

            var payable = await _context.Suppliers
                .Where(s => s.Balance > 0)
                .SumAsync(s => s.Balance);

            var unpaidInvoices = await _context.PurchaseInvoices
                .Where(pi => pi.PaymentStatus != PurchaseInvoicePaymentStatus.Paid)
                .SumAsync(pi => pi.RemainingAmount);

            return Ok(ApiResponse<object>.Ok(new
            {
                AccountsReceivable = receivable,
                AccountsPayable = payable,
                UnpaidInvoices = unpaidInvoices,
                NetPosition = receivable - payable - unpaidInvoices
            }));
        }
    }
}
