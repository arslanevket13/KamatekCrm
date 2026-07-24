using System;
using System.Linq;
using System.Threading.Tasks;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using BCrypt.Net;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Veritabanı tabanlı kimlik doğrulama servisi (BCrypt Password Hashing destekli)
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private User? _currentUser;
        
        public AuthService(IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        public User? CurrentUser => _currentUser;
        public bool IsLoggedIn => _currentUser != null;

        #region RBAC - Granular Permissions

        public bool CanViewFinance => _currentUser?.CanViewFinance == true || IsAdmin;
        public bool CanViewAnalytics => _currentUser?.CanViewAnalytics == true || IsAdmin;
        public bool CanDeleteRecords => _currentUser?.CanDeleteRecords == true || IsAdmin;
        public bool CanApprovePurchase => _currentUser?.CanApprovePurchase == true || IsAdmin;
        public bool CanAccessSettings => _currentUser?.CanAccessSettings == true || IsAdmin;

        #endregion

        /// <summary>
        /// Veritabanı üzerinden doğrudan login (BCrypt Hash kontrolü ile)
        /// </summary>
        public async Task<bool> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new Exception("Kullanıcı adı veya şifre boş olamaz.");

            using var context = await _dbContextFactory.CreateDbContextAsync();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user != null)
            {
                bool isPasswordValid = false;

                // BCrypt Hash Kontrolü
                if (!string.IsNullOrEmpty(user.PasswordHash) &&
                    (user.PasswordHash.StartsWith("$2a$") || user.PasswordHash.StartsWith("$2b$") || user.PasswordHash.StartsWith("$2y$")))
                {
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                }
                else
                {
                    // Eski düz metin (plain text) kontrolü & Otomatik BCrypt'e Yükseltme (Migration)
                    if (user.PasswordHash == password)
                    {
                        isPasswordValid = true;
                        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                        await context.SaveChangesAsync();
                    }
                }

                if (isPasswordValid)
                {
                    if (!user.IsActive)
                        throw new Exception("Hesabınız pasif duruma alınmıştır.");

                    _currentUser = user;
                    user.LastLoginDate = DateTime.UtcNow;
                    await context.SaveChangesAsync();

                    Log.Information("DB Login başarılı: {Username} (Role: {Role})", user.Username, user.Role);
                    return true;
                }
            }

            Log.Warning("DB Login başarısız: {Username}", username);
            throw new Exception("Geçersiz kullanıcı adı veya şifre.");
        }

        /// <summary>
        /// Oturumu kapat
        /// </summary>
        public void Logout()
        {
            _currentUser = null;
            Log.Information("Kullanıcı oturumu kapatıldı.");
        }

        public bool HasRole(string role)
        {
            return _currentUser?.Role?.Equals(role, StringComparison.OrdinalIgnoreCase) == true;
        }

        public bool IsAdmin => HasRole("Admin");
    }
}
