using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Data;
using KamatekCrm.API.Services;
using Microsoft.AspNetCore.Authorization;

namespace KamatekCrm.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ExportController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IExcelService _excel;

        public ExportController(AppDbContext context, IExcelService excel)
        {
            _context = context;
            _excel = excel;
        }

        /// <summary>
        /// Servis işlerini Excel'e export et
        /// </summary>
        [HttpGet("service-jobs")]
        public async Task<IActionResult> ExportServiceJobs(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var query = _context.ServiceJobs
                .Include(j => j.Customer)
                .Include(j => j.AssignedUser)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(j => j.CreatedDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(j => j.CreatedDate <= endDate.Value);

            var data = await query
                .OrderByDescending(j => j.CreatedDate)
                .Select(j => new ServiceJobExportRow
                {
                    Id = j.Id,
                    Title = j.Title,
                    CustomerName = j.Customer != null ? j.Customer.FullName : "",
                    Status = j.Status.ToString(),
                    Priority = j.Priority.ToString(),
                    AssignedTechnician = j.AssignedTechnician ?? "",
                    CreatedDate = j.CreatedDate,
                    CompletedDate = j.CompletedDate,
                    Price = j.Price,
                    LaborCost = j.LaborCost
                })
                .ToListAsync();

            var bytes = _excel.ExportServiceJobs(data);
            var fileName = $"servis_isleri_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        /// <summary>
        /// Müşterileri Excel'e export et
        /// </summary>
        [HttpGet("customers")]
        public async Task<IActionResult> ExportCustomers()
        {
            var data = await _context.Customers
                .OrderBy(c => c.FullName)
                .Select(c => new CustomerExportRow
                {
                    Id = c.Id,
                    FullName = c.FullName,
                    PhoneNumber = c.PhoneNumber,
                    Email = c.Email,
                    Address = c.FullAddress,
                    CreatedDate = c.CreatedDate,
                    TotalJobs = _context.ServiceJobs.Count(j => j.CustomerId == c.Id)
                })
                .ToListAsync();

            var bytes = _excel.ExportCustomers(data);
            var fileName = $"musteriler_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        /// <summary>
        /// Kategorileri Excel'e export et
        /// </summary>
        [HttpGet("categories")]
        public async Task<IActionResult> ExportCategories()
        {
            var data = await _context.Categories
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    Kategori = c.Name
                })
                .ToListAsync();

            var bytes = _excel.ExportToExcel(data, "Kategoriler");
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"kategoriler_{DateTime.UtcNow:yyyyMMdd}.xlsx");
        }
    }
}
