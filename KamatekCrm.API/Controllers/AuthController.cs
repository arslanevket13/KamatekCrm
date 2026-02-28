using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KamatekCrm.Data;
using KamatekCrm.Shared.DTOs;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            AppDbContext context,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Kullanıcı girişi - JWT token üretir
        /// </summary>
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new LoginResponseDto
                    {
                        Success = false,
                        Message = "Geçersiz istek. Kullanıcı adı ve şifre gereklidir."
                    });
                }

                // Kullanıcıyı veritabanından bul
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

                if (user == null)
                {
                    _logger.LogWarning("Login başarısız: Kullanıcı bulunamadı - {Username}", request.Username);
                    return Unauthorized(new LoginResponseDto
                    {
                        Success = false,
                        Message = "Kullanıcı adı veya şifre hatalı."
                    });
                }

                // Şifre doğrulama (PBKDF2 - WPF AuthService ile aynı algoritma)
                if (!VerifyPassword(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Login başarısız: Hatalı şifre - {Username}", request.Username);
                    return Unauthorized(new LoginResponseDto
                    {
                        Success = false,
                        Message = "Kullanıcı adı veya şifre hatalı."
                    });
                }

                // JWT token oluştur
                var token = GenerateJwtToken(user);
                var expireDays = _configuration.GetValue<int>("Jwt:ExpireDays", 7);

                // Son giriş tarihini güncelle
                user.LastLoginDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Login başarılı: {Username} (UserId: {UserId})", user.Username, user.Id);

                return Ok(new LoginResponseDto
                {
                    Success = true,
                    Token = token,
                    Username = user.Username,
                    FullName = user.AdSoyad,
                    Role = user.Role,
                    UserId = user.Id,
                    Message = "Giriş başarılı."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login sırasında beklenmeyen hata - {Username}", request.Username);
                return StatusCode(500, new LoginResponseDto
                {
                    Success = false,
                    Message = "Sunucu hatası. Lütfen daha sonra tekrar deneyin."
                });
            }
        }

        #region Private Helpers

        /// <summary>
        /// JWT Bearer token üretir.
        /// </summary>
        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"]!;
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "KamatekCRM";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "KamatekCRM-Users";
            var expireDays = _configuration.GetValue<int>("Jwt:ExpireDays", 7);

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("UserId", user.Id.ToString()),
                new Claim("FullName", user.AdSoyad)
            };

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(expireDays),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// PBKDF2 ile hash'lenmiş şifreyi doğrular.
        /// WPF AuthService.VerifyPassword ile birebir aynı algoritmadır:
        /// - 16 byte salt + 32 byte hash = 48 byte Base64
        /// - 100,000 iterasyon, SHA256
        /// </summary>
        private static bool VerifyPassword(string password, string storedHash)
        {
            byte[] hashBytes;
            try
            {
                hashBytes = Convert.FromBase64String(storedHash);
            }
            catch (FormatException)
            {
                return false;
            }

            if (hashBytes.Length != 48)
                return false;

            // Salt'i ayır (ilk 16 byte)
            byte[] salt = new byte[16];
            Array.Copy(hashBytes, 0, salt, 0, 16);

            // Girilen şifreyi aynı salt ile hashle
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32);

            // Sabit zamanlı karşılaştırma (timing attack koruması)
            return CryptographicOperations.FixedTimeEquals(
                hashBytes.AsSpan(16, 32),
                hash.AsSpan(0, 32)
            );
        }

        #endregion
    }
}
