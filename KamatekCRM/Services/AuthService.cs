using System;
using System.Linq;
using System.Threading.Tasks;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Veritabanı tabanlı kimlik dogrulama servisi. (API bagimliligi kaldirildi)
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
        /// Veritabani uzerinden dogrudan login
        /// </summary>
        public async Task<bool> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new Exception("Kullanici adi veya sifre bos olamaz.");

            using var context = await _dbContextFactory.CreateDbContextAsync();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user != null && user.PasswordHash == password) // Note: In a real system, use Hash comparison!
            {
                if (!user.IsActive)
                    throw new Exception("Hesabiniz pasif duruma alinmistir.");

                _currentUser = user;
                
                // Update last login date
                user.LastLoginDate = DateTime.UtcNow;
                await context.SaveChangesAsync();

                Log.Information("DB Login basarili: {Username} (Role: {Role})", user.Username, user.Role);
                return true;
            }

            Log.Warning("DB Login basarisiz: {Username}", username);
            throw new Exception("Gecersiz kullanici adi veya sifre.");
        }

        /// <summary>
        /// Oturumu kapat ve token'i temizle
        /// </summary>
        public void Logout()
        {
            _currentUser = null;
            Log.Information("Kullanici oturumu kapatildi.");
        }

        public bool HasRole(string role)
        {
            return _currentUser?.Role?.Equals(role, StringComparison.OrdinalIgnoreCase) == true;
        }

        public bool IsAdmin => HasRole("Admin");
    }
}
