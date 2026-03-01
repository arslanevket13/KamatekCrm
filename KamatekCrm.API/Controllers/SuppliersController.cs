using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Models;
using KamatekCrm.Data;
using KamatekCrm.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace KamatekCrm.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SuppliersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SuppliersController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>Tedarikçi listesi</summary>
        [HttpGet]
        public async Task<IActionResult> GetSuppliers([FromQuery] string? search, [FromQuery] bool? isActive)
        {
            var query = _context.Suppliers.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(search) ||
                                         s.CompanyName.ToLower().Contains(search));
            }
            if (isActive.HasValue) query = query.Where(s => s.IsActive == isActive.Value);

            var suppliers = await query.OrderBy(s => s.Name).ToListAsync();
            return Ok(ApiResponse<object>.Ok(suppliers));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSupplier(int id)
        {
            var supplier = await _context.Suppliers
                .Include(s => s.PurchaseOrders)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (supplier == null) return NotFound();
            return Ok(ApiResponse<Supplier>.Ok(supplier));
        }

        [HttpPost]
        public async Task<IActionResult> CreateSupplier(Supplier supplier)
        {
            supplier.CreatedDate = DateTime.UtcNow;
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetSupplier), new { id = supplier.Id }, ApiResponse<Supplier>.Ok(supplier));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSupplier(int id, Supplier supplier)
        {
            if (id != supplier.Id) return BadRequest();
            var existing = await _context.Suppliers.FindAsync(id);
            if (existing == null) return NotFound();
            _context.Entry(existing).CurrentValues.SetValues(supplier);
            existing.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSupplier(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();
            supplier.IsActive = false; // soft deactivate
            supplier.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>Tedarikçi bakiye özeti</summary>
        [HttpGet("balances")]
        public async Task<IActionResult> GetSupplierBalances()
        {
            var balances = await _context.Suppliers
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.Balance)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.CompanyName,
                    s.Balance,
                    OpenOrders = s.PurchaseOrders.Count(po =>
                        po.Status != KamatekCrm.Shared.Enums.PurchaseStatus.Received &&
                        po.Status != KamatekCrm.Shared.Enums.PurchaseStatus.Cancelled)
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(balances));
        }
    }
}
