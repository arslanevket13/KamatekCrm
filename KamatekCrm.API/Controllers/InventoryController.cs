using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Models;
using KamatekCrm.Data;
using KamatekCrm.API.Models;
using KamatekCrm.API.Hubs;
using Microsoft.AspNetCore.Authorization;

namespace KamatekCrm.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InventoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notifications;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(AppDbContext context, INotificationService notifications, ILogger<InventoryController> logger)
        {
            _context = context;
            _notifications = notifications;
            _logger = logger;
        }

        /// <summary>Tüm depo stokları</summary>
        [HttpGet]
        public async Task<IActionResult> GetInventory(
            [FromQuery] int? warehouseId,
            [FromQuery] int? productId,
            [FromQuery] bool? lowStock)
        {
            var query = _context.Inventories
                .Include(i => i.Product)
                .Include(i => i.Warehouse)
                .AsQueryable();

            if (warehouseId.HasValue)
                query = query.Where(i => i.WarehouseId == warehouseId.Value);
            if (productId.HasValue)
                query = query.Where(i => i.ProductId == productId.Value);
            if (lowStock == true)
                query = query.Where(i => i.Quantity <= i.Product.MinStockLevel);

            var inventory = await query.OrderBy(i => i.Product.ProductName).ToListAsync();
            return Ok(ApiResponse<object>.Ok(inventory));
        }

        /// <summary>Depolar listesi</summary>
        [HttpGet("warehouses")]
        public async Task<IActionResult> GetWarehouses()
        {
            var warehouses = await _context.Warehouses
                .Where(w => w.IsActive)
                .OrderBy(w => w.Name)
                .ToListAsync();
            return Ok(ApiResponse<object>.Ok(warehouses));
        }

        /// <summary>Stok hareketi oluştur (giriş/çıkış/transfer)</summary>
        [HttpPost("transactions")]
        public async Task<IActionResult> CreateTransaction(StockTransaction transaction)
        {
            try
            {
                transaction.Date = DateTime.UtcNow;
                _context.StockTransactions.Add(transaction);

                // Stok miktarını güncelle
                var inventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == transaction.ProductId
                        && i.WarehouseId == (transaction.TargetWarehouseId ?? transaction.SourceWarehouseId ?? 0));

                if (inventory != null)
                {
                    if (transaction.TransactionType == KamatekCrm.Shared.Enums.StockTransactionType.In)
                        inventory.Quantity += transaction.Quantity;
                    else if (transaction.TransactionType == KamatekCrm.Shared.Enums.StockTransactionType.Out)
                    {
                        inventory.Quantity -= transaction.Quantity;

                        // Stok uyarısı
                        var product = await _context.Products.FindAsync(transaction.ProductId);
                        if (product != null && inventory.Quantity <= product.MinStockLevel)
                        {
                            await _notifications.NotifyStockAlert(
                                product.ProductName, inventory.Quantity, product.MinStockLevel);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("StockTransaction created: Type={Type} Product={ProductId} Qty={Qty}",
                    transaction.TransactionType, transaction.ProductId, transaction.Quantity);

                return Ok(ApiResponse<StockTransaction>.Ok(transaction));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stok hareketi oluşturma hatası");
                return StatusCode(500, ApiResponse.Fail("Stok hareketi oluşturulurken hata oluştu."));
            }
        }

        /// <summary>Stok hareketleri (filtrelenebilir)</summary>
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] int? productId,
            [FromQuery] int? warehouseId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _context.StockTransactions
                .Include(t => t.Product)
                .AsQueryable();

            if (productId.HasValue) query = query.Where(t => t.ProductId == productId.Value);
            if (warehouseId.HasValue)
                query = query.Where(t => t.SourceWarehouseId == warehouseId.Value || t.TargetWarehouseId == warehouseId.Value);
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
    }
}
