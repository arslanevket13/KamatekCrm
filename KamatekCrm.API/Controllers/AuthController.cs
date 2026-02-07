using KamatekCrm.API.Data;
using KamatekCrm.Shared.DTOs;
using KamatekCrm.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace KamatekCrm.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApiDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(ApiDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null || user.PasswordHash != HashPassword(request.Password))
            {
                return Unauthorized(new LoginResponse { Success = false, Message = "Geçersiz kullanıcı adı veya şifre." });
            }

            if (!user.IsActive)
            {
                return Unauthorized(new LoginResponse { Success = false, Message = "Kullanıcı hesabı pasif." });
            }

            var token = GenerateJwtToken(user);

            return Ok(new LoginResponse
            {
                Success = true,
                Message = "Giriş başarılı.",
                Token = token,
                UserId = user.Id,
                FullName = $"{user.Ad} {user.Soyad}",
                Role = user.Role
            });
        }

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "super_secret_key_123456789_at_least_32_chars";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("UserId", user.Id.ToString()),
                new Claim("FullName", $"{user.Ad} {user.Soyad}")
            };

            var token = new JwtSecurityToken(
                issuer: "https://localhost:5050",
                audience: "https://localhost:5050",
                claims: claims,
                expires: DateTime.Now.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
