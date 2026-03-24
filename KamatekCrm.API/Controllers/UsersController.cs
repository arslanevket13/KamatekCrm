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
                    u.ServiceArea,
                    u.CanViewFinance,
                    u.CanViewAnalytics,
                    u.CanDeleteRecords,
                    u.CanApprovePurchase,
                    u.CanAccessSettings,
                    u.VehiclePlate
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
                    u.AverageRating,
                    u.CanViewFinance,
                    u.CanViewAnalytics,
                    u.CanDeleteRecords,
                    u.CanApprovePurchase,
                    u.CanAccessSettings
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
            if (!string.IsNullOrEmpty(request.VehiclePlate)) user.VehiclePlate = request.VehiclePlate;
            if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;
            if (request.IsTechnician.HasValue) user.IsTechnician = request.IsTechnician.Value;

            // RBAC Permissions
            if (request.CanViewFinance.HasValue) user.CanViewFinance = request.CanViewFinance.Value;
            if (request.CanViewAnalytics.HasValue) user.CanViewAnalytics = request.CanViewAnalytics.Value;
            if (request.CanDeleteRecords.HasValue) user.CanDeleteRecords = request.CanDeleteRecords.Value;
            if (request.CanApprovePurchase.HasValue) user.CanApprovePurchase = request.CanApprovePurchase.Value;
            if (request.CanAccessSettings.HasValue) user.CanAccessSettings = request.CanAccessSettings.Value;

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

        /// <summary>
        /// Yeni kullanıcı oluştur
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                return BadRequest(new { Message = "Bu kullanıcı adı zaten mevcut." });

            var user = new User
            {
                Username = request.Username,
                PasswordHash = HashPassword(request.Password),
                Role = request.Role,
                Ad = request.Ad,
                Soyad = request.Soyad,
                IsActive = request.IsActive,
                CanViewFinance = request.CanViewFinance,
                CanViewAnalytics = request.CanViewAnalytics,
                CanDeleteRecords = request.CanDeleteRecords,
                CanApprovePurchase = request.CanApprovePurchase,
                CanAccessSettings = request.CanAccessSettings,
                IsTechnician = request.IsTechnician,
                Phone = request.Phone,
                VehiclePlate = request.VehiclePlate,
                ServiceArea = request.ServiceArea,
                ExpertiseAreas = request.ExpertiseAreas,
                CreatedDate = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new { user.Id, user.Username });
        }

        /// <summary>
        /// Şifre değiştir / sıfırla
        /// </summary>
        [HttpPost("{id}/change-password")]
        public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordRequest request)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.PasswordHash = HashPassword(request.NewPassword);
            user.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Şifre başarıyla güncellendi." });
        }

        #region Helpers
        private string HashPassword(string password)
        {
            byte[] salt = new byte[16];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(password, salt, 100000, System.Security.Cryptography.HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32);
            byte[] hashBytes = new byte[48];
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 32);
            return Convert.ToBase64String(hashBytes);
        }
        #endregion
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Viewer";
        public string Ad { get; set; } = string.Empty;
        public string Soyad { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool CanViewFinance { get; set; } = false;
        public bool CanViewAnalytics { get; set; } = false;
        public bool CanDeleteRecords { get; set; } = false;
        public bool CanApprovePurchase { get; set; } = false;
        public bool CanAccessSettings { get; set; } = false;
        public bool IsTechnician { get; set; }
        public string? Phone { get; set; }
        public string? VehiclePlate { get; set; }
        public string? ServiceArea { get; set; }
        public string? ExpertiseAreas { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
    }

    public class UpdateUserRequest
    {
        public string? Ad { get; set; }
        public string? Soyad { get; set; }
        public string? Phone { get; set; }
        public string? Role { get; set; }
        public string? ServiceArea { get; set; }
        public string? ExpertiseAreas { get; set; }
        public string? VehiclePlate { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsTechnician { get; set; }
        
        // RBAC
        public bool? CanViewFinance { get; set; }
        public bool? CanViewAnalytics { get; set; }
        public bool? CanDeleteRecords { get; set; }
        public bool? CanApprovePurchase { get; set; }
        public bool? CanAccessSettings { get; set; }
    }
}
