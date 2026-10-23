using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using BCrypt.Net;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Veritabanı tabanlı kimlik doğrulama servisi (BCrypt Password Hashing, Brute-Force Koruması, RBAC)
    /// </summary>
    public class AuthService : IAuthService
    {
        private const int MaxFailedAccessAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
        private static readonly ConcurrentDictionary<string, AccountLockoutInfo> _lockoutTracker = new(StringComparer.OrdinalIgnoreCase);

        private sealed class AccountLockoutInfo
        {
            public int FailedAttempts { get; set; }
            public DateTime? LockoutUntil { get; set; }
        }

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

        public bool HasRole(string role)
        {
            if (_currentUser == null) return false;
            return string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(_currentUser.Role, role, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsAdmin => HasRole("Admin");

        public void EnsureAuthorized(ApplicationPermission permission)
        {
            if (!IsLoggedIn)
                throw new UnauthorizedAccessException("Bu işlem için oturum açılmalıdır.");

            if (IsAdmin)
                return;

            bool isAllowed = permission switch
            {
                ApplicationPermission.ManageServiceJobs => HasRole("Technician") || HasRole("Personel"),
                ApplicationPermission.ExecuteSales => HasRole("Technician"),
                ApplicationPermission.AdjustInventory => CanDeleteRecords,
                ApplicationPermission.ApprovePurchase => CanApprovePurchase,
                ApplicationPermission.ViewFinance => CanViewFinance,
                ApplicationPermission.DeleteRecords => CanDeleteRecords,
                ApplicationPermission.AccessSettings => CanAccessSettings,
                ApplicationPermission.ManageUsers => false,
                ApplicationPermission.ViewCustomerContactData => HasRole("Technician") || HasRole("Personel"),
                ApplicationPermission.ViewCustomerIdentityData => CanViewFinance,
                ApplicationPermission.ProcessReturns => CanDeleteRecords,
                ApplicationPermission.ManageQuotes => HasRole("Technician") || HasRole("Personel"),
                _ => false
            };

            if (!isAllowed)
                throw new UnauthorizedAccessException($"Bu işlem için yetkiniz bulunmuyor ({permission}).");
        }

        #endregion

        /// <summary>
        /// Şifre hashleme yardımcısı (BCrypt - WorkFactor: 11)
        /// </summary>
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
        }

        /// <summary>
        /// Veritabanı üzerinden doğrudan login (BCrypt Hash kontrolü, Brute-Force koruması ve şeffaf migration ile)
        /// </summary>
        public async Task<bool> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Kullanıcı adı veya şifre boş olamaz.");

            string normalizedUsername = username.Trim().ToLowerInvariant();

            // 1. Brute-Force / Hesap Kilitleme Kontrolü
            if (_lockoutTracker.TryGetValue(normalizedUsername, out var lockoutInfo) && lockoutInfo.LockoutUntil.HasValue)
            {
                if (DateTime.UtcNow < lockoutInfo.LockoutUntil.Value)
                {
                    var remaining = lockoutInfo.LockoutUntil.Value - DateTime.UtcNow;
                    int remainingMinutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
                    Log.Warning("Kilitli hesapla giriş denemesi: {Username}. Kalan süre: {Minutes} dakika.", username, remainingMinutes);
                    throw new InvalidOperationException($"Çok sayıda başarısız giriş denemesi nedeniyle hesabınız geçici olarak kilitlendi. Lütfen {remainingMinutes} dakika sonra tekrar deneyiniz.");
                }
                else
                {
                    // Kilit süresi dolmuş, sayacı sıfırla
                    lockoutInfo.FailedAttempts = 0;
                    lockoutInfo.LockoutUntil = null;
                }
            }

            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername);

            bool isPasswordValid = false;
            bool needsMigration = false;

            if (user != null)
            {
                // BCrypt Hash Kontrolü
                if (!string.IsNullOrEmpty(user.PasswordHash) &&
                    (user.PasswordHash.StartsWith("$2a$") || user.PasswordHash.StartsWith("$2b$") || user.PasswordHash.StartsWith("$2y$")))
                {
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                }
                else if (!string.IsNullOrEmpty(user.PasswordHash) && VerifyLegacyPbkdf2(password, user.PasswordHash))
                {
                    // Eski PBKDF2 algoritmasından şeffaf geçiş
                    isPasswordValid = true;
                    needsMigration = true;
                }
                else if (user.PasswordHash == password)
                {
                    // Eski düz metin (plain text) kontrolü & şeffaf geçiş
                    isPasswordValid = true;
                    needsMigration = true;
                }
            }

            if (!isPasswordValid || user == null)
            {
                // Başarısız deneme sayacını artır (Kullanıcı bulunsun veya bulunmasın - Account Enumeration Koruması)
                var state = _lockoutTracker.GetOrAdd(normalizedUsername, _ => new AccountLockoutInfo());
                state.FailedAttempts++;

                if (state.FailedAttempts >= MaxFailedAccessAttempts)
                {
                    state.LockoutUntil = DateTime.UtcNow.Add(LockoutDuration);
                    Log.Warning("Kullanıcı hesabı 5 başarısız deneme nedeniyle 15 dakika kilitlendi: {Username}", username);
                    throw new InvalidOperationException("Çok sayıda başarısız giriş denemesi nedeniyle hesabınız 15 dakika boyunca geçici olarak kilitlendi.");
                }

                Log.Warning("Başarısız giriş denemesi: {Username} (Deneme: {Attempts}/{Max})", username, state.FailedAttempts, MaxFailedAccessAttempts);
                throw new UnauthorizedAccessException("Kullanıcı adı veya parola hatalı.");
            }

            if (!user.IsActive)
                throw new InvalidOperationException("Hesabınız pasif duruma alınmıştır.");

            // Başarılı giriş - Kilit sayacını temizle
            _lockoutTracker.TryRemove(normalizedUsername, out _);

            // Şeffaf parola yükseltme (Migration)
            if (needsMigration)
            {
                user.PasswordHash = HashPassword(password);
                Log.Information("Kullanıcı parolası şeffaf olarak güvenli BCrypt formatına yükseltildi: {Username}", user.Username);
            }

            _currentUser = user;
            user.LastLoginDate = DateTime.UtcNow;
            await context.SaveChangesAsync();

            Log.Information("DB Login başarılı: {Username} (Role: {Role})", user.Username, user.Role);
            return true;
        }

        /// <summary>
        /// Eski PBKDF2 (16-byte salt, 32-byte hash SHA256) doğrulaması
        /// </summary>
        private static bool VerifyLegacyPbkdf2(string password, string storedHash)
        {
            try
            {
                byte[] hashBytes = Convert.FromBase64String(storedHash);
                if (hashBytes.Length != 48)
                    return false;

                byte[] salt = new byte[16];
                Array.Copy(hashBytes, 0, salt, 0, 16);

                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
                byte[] hash = pbkdf2.GetBytes(32);

                return CryptographicOperations.FixedTimeEquals(
                    hashBytes.AsSpan(16, 32),
                    hash.AsSpan(0, 32)
                );
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Oturumu kapat
        /// </summary>
        public void Logout()
        {
            _currentUser = null;
            Log.Information("Kullanıcı oturumu kapatıldı.");
        }
    }
}
