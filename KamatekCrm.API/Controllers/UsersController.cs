using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Models;
using KamatekCrm.Data;
using Microsoft.AspNetCore.Authorization;

namespace KamatekCrm.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Tüm kullanıcıları listele (teknisyen atama dropdown'ları için)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers(
            [FromQuery] string? search,
            [FromQuery] string? role,
            [FromQuery] bool? isTechnician)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(u =>
                    u.Username.ToLower().Contains(search) ||
                    u.Ad.ToLower().Contains(search) ||
                    u.Soyad.ToLower().Contains(search));
            }

            if (!string.IsNullOrEmpty(role))
                query = query.Where(u => u.Role == role);

            if (isTechnician.HasValue)
                query = query.Where(u => u.IsTechnician == isTechnician.Value);

            var users = await query
                .OrderBy(u => u.Ad).ThenBy(u => u.Soyad)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    FullName = u.Ad + " " + u.Soyad,
                    u.Ad,
                    u.Soyad,
                    u.Role,
                    u.IsActive,
                    u.IsTechnician,
                    u.Phone,
                    u.ServiceArea
                })
                .ToListAsync();

            return Ok(users);
        }

        /// <summary>
        /// Tek kullanıcı detayı
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    FullName = u.Ad + " " + u.Soyad,
                    u.Ad,
                    u.Soyad,
                    u.Role,
                    u.IsActive,
                    u.IsTechnician,
                    u.Phone,
                    u.ServiceArea,
                    u.ExpertiseAreas,
                    u.VehiclePlate,
                    u.CreatedDate,
                    u.TotalJobsCompleted,
                    u.AverageRating
                })
                .FirstOrDefaultAsync();

            if (user == null) return NotFound();
            return Ok(user);
        }

        /// <summary>
        /// Kullanıcı güncelle (şifre hariç)
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (!string.IsNullOrEmpty(request.Ad)) user.Ad = request.Ad;
            if (!string.IsNullOrEmpty(request.Soyad)) user.Soyad = request.Soyad;
            if (!string.IsNullOrEmpty(request.Phone)) user.Phone = request.Phone;
            if (!string.IsNullOrEmpty(request.Role)) user.Role = request.Role;
            if (!string.IsNullOrEmpty(request.ServiceArea)) user.ServiceArea = request.ServiceArea;
            if (!string.IsNullOrEmpty(request.ExpertiseAreas)) user.ExpertiseAreas = request.ExpertiseAreas;
            if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;
            if (request.IsTechnician.HasValue) user.IsTechnician = request.IsTechnician.Value;

            user.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Kullanıcıyı deaktif et (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeactivateUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.IsActive = false;
            user.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public class UpdateUserRequest
    {
        public string? Ad { get; set; }
        public string? Soyad { get; set; }
        public string? Phone { get; set; }
        public string? Role { get; set; }
        public string? ServiceArea { get; set; }
        public string? ExpertiseAreas { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsTechnician { get; set; }
    }
}
