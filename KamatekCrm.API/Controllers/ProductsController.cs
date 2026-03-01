using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Models;
using KamatekCrm.Data;
using KamatekCrm.API.Services;
using KamatekCrm.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace KamatekCrm.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICacheService _cache;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(AppDbContext context, ICacheService cache, ILogger<ProductsController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Ürünleri listele (aranabilir, filtrelenebilir, sayfalı)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProducts(
            [FromQuery] string? search,
            [FromQuery] int? categoryId,
            [FromQuery] int? brandId,
            [FromQuery] bool? lowStock,
            [FromQuery] string? sortBy = "ProductName",
            [FromQuery] string? sortDir = "asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(p =>
                    p.ProductName.ToLower().Contains(search) ||
                    p.SKU.ToLower().Contains(search) ||
                    p.Barcode.ToLower().Contains(search));
            }

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);
            if (brandId.HasValue)
                query = query.Where(p => p.BrandId == brandId.Value);
            if (lowStock == true)
                query = query.Where(p => p.TotalStockQuantity <= p.MinStockLevel);

            query = sortBy?.ToLower() switch
            {
                "price" => sortDir == "asc" ? query.OrderBy(p => p.SalePrice) : query.OrderByDescending(p => p.SalePrice),
                "stock" => sortDir == "asc" ? query.OrderBy(p => p.TotalStockQuantity) : query.OrderByDescending(p => p.TotalStockQuantity),
                "sku" => sortDir == "asc" ? query.OrderBy(p => p.SKU) : query.OrderByDescending(p => p.SKU),
                _ => sortDir == "asc" ? query.OrderBy(p => p.ProductName) : query.OrderByDescending(p => p.ProductName)
            };

            var total = await query.CountAsync();
            var products = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var pagination = new PaginationMeta { Page = page, PageSize = pageSize, TotalCount = total };
            return Ok(ApiResponse<object>.Ok(products, pagination));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(int id)
        {
            var product = await _context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.Inventories).ThenInclude(i => i.Warehouse)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();
            return Ok(ApiResponse<Product>.Ok(product));
        }

        [HttpPost]
        public async Task<IActionResult> CreateProduct(Product product)
        {
            product.CreatedDate = DateTime.UtcNow;
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            _cache.RemoveByPrefix("lists:products");
            _logger.LogInformation("Product #{Id} created: {Name}", product.Id, product.ProductName);
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, ApiResponse<Product>.Ok(product));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, Product product)
        {
            if (id != product.Id) return BadRequest();
            var existing = await _context.Products.FindAsync(id);
            if (existing == null) return NotFound();

            _context.Entry(existing).CurrentValues.SetValues(product);
            existing.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _cache.RemoveByPrefix("lists:products");
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            _cache.RemoveByPrefix("lists:products");
            return NoContent();
        }

        /// <summary>Düşük stoklu ürünleri getir (stok uyarısı)</summary>
        [HttpGet("low-stock")]
        public async Task<IActionResult> GetLowStockProducts()
        {
            var products = await _context.Products
                .Where(p => p.TotalStockQuantity <= p.MinStockLevel)
                .OrderBy(p => p.TotalStockQuantity)
                .Select(p => new
                {
                    p.Id,
                    p.ProductName,
                    p.SKU,
                    p.TotalStockQuantity,
                    p.MinStockLevel,
                    Deficit = p.MinStockLevel - p.TotalStockQuantity,
                    Severity = p.TotalStockQuantity == 0 ? "critical" : "warning"
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(products));
        }

        /// <summary>Markalar dropdown listesi (cached)</summary>
        [HttpGet("brands")]
        public async Task<IActionResult> GetBrands()
        {
            var brands = await _cache.GetOrCreateAsync("lists:brands", async () =>
                await _context.Brands.OrderBy(b => b.BrandName).ToListAsync(),
                CacheService.ListTtl);

            return Ok(ApiResponse<object>.Ok(brands!));
        }
    }
}
