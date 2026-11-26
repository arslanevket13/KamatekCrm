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
        /// Girilen parolanın saklanan BCrypt hash'i ile eşleşip eşleşmediğini doğrular.
        /// </summary>
        public static bool VerifyPassword(string password, string? storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
                return false;

            if (storedHash.StartsWith("$2a$", StringComparison.Ordinal) ||
                storedHash.StartsWith("$2b$", StringComparison.Ordinal) ||
                storedHash.StartsWith("$2y$", StringComparison.Ordinal))
            {
                return BCrypt.Net.BCrypt.Verify(password, storedHash);
            }

            return VerifyLegacyPbkdf2(password, storedHash);
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
                else if (false && user.PasswordHash == password)
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
#if DEBUG
            _hasLoggedOutManually = false;
#endif
            user.LastLoginDate = DateTime.UtcNow;
            await context.SaveChangesAsync();

            Log.Information("DB Login başarılı: {Username} (Role: {Role})", user.Username, user.Role);
            return true;
        }

#if DEBUG
        private bool _hasLoggedOutManually;

        public bool HasLoggedOutManually => _hasLoggedOutManually;

        /// <summary>
        /// Yalnızca geliştirme (DEBUG) ortamında aktif Admin hesabıyla şifresiz hızlı oturum açar.
        /// </summary>
        public async Task<bool> DevQuickLoginAsAdminAsync(string? preferredUsername = null)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            User? user = null;
            if (!string.IsNullOrWhiteSpace(preferredUsername))
            {
                var normalizedPreferred = preferredUsername.Trim().ToLowerInvariant();
                user = await context.Users
                    .OrderBy(u => u.Id)
                    .FirstOrDefaultAsync(u =>
                        u.IsActive
                        && u.Role.ToLower() == "admin"
                        && u.Username.ToLower() == normalizedPreferred);
            }

            user ??= await context.Users
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync(u => u.IsActive && u.Role.ToLower() == "admin");

            if (user is null)
            {
                Log.Warning("DEV-ONLY quick admin login failed: no active Admin account found.");
                throw new InvalidOperationException("Veritabanında aktif bir Admin kullanıcısı bulunamadı.");
            }

            _currentUser = user;
            _hasLoggedOutManually = false;
            user.LastLoginDate = DateTime.UtcNow;
            await context.SaveChangesAsync();

            Log.Warning("DEV-ONLY quick admin login used for {Username}", user.Username);
            return true;
        }
#endif

        public async Task<bool> TryAutoLoginAsync(int userId, string username)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(username))
                return false;

            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var user = await context.Users.FirstOrDefaultAsync(
                    u => u.Id == userId && u.Username == username && u.IsActive);

                if (user is null)
                    return false;

                _currentUser = user;
                user.LastLoginDate = DateTime.UtcNow;
                await context.SaveChangesAsync();

                Log.Information("Otomatik oturum açıldı: {Username} (Role: {Role})", user.Username, user.Role);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Otomatik oturum açma denemesi başarısız oldu: {Username}", username);
                return false;
            }
        }

        public async Task<(bool Success, string? ErrorMessage, User? CreatedUser)> CreateInitialAdminAsync(
            string fullName,
            string username,
            string password)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return (false, "Ad Soyad alanı zorunludur.", null);

            if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3)
                return (false, "Kullanıcı adı en az 3 karakter olmalıdır.", null);

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
                return (false, "Parola en az 6 karakter olmalıdır.", null);

            var cleanUsername = username.Trim();
            if (cleanUsername.Length > 50)
                cleanUsername = cleanUsername[..50];
            var normalized = cleanUsername.ToLowerInvariant();

            var nameParts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var firstName = nameParts.Length > 0 ? nameParts[0] : fullName.Trim();
            var lastName = nameParts.Length > 1 ? nameParts[1] : "-";
            if (firstName.Length > 50) firstName = firstName[..50];
            if (lastName.Length > 50) lastName = lastName[..50];

            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var existingUser = await context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized);

                User adminUser;
                var nowUtc = DateTime.UtcNow;

                if (existingUser is not null)
                {
                    if (KamatekCrm.Settings.AppSettings.IsFirstRunCompleted && !existingUser.IsDeleted)
                    {
                        return (false, $"'{cleanUsername}' kullanıcı adı zaten kayıtlı.", null);
                    }

                    existingUser.Ad = firstName;
                    existingUser.Soyad = lastName;
                    existingUser.PasswordHash = HashPassword(password);
                    existingUser.Role = "Admin";
                    existingUser.IsActive = true;
                    existingUser.IsDeleted = false;
                    existingUser.DeletedAt = null;
                    existingUser.DeletedBy = null;
                    existingUser.MustChangePassword = false;
                    existingUser.CanViewFinance = true;
                    existingUser.CanViewAnalytics = true;
                    existingUser.CanDeleteRecords = true;
                    existingUser.CanApprovePurchase = true;
                    existingUser.CanAccessSettings = true;
                    existingUser.LastLoginDate = nowUtc;
                    existingUser.ModifiedDate = nowUtc;
                    existingUser.ModifiedBy = "System";
                    adminUser = existingUser;
                }
                else
                {
                    adminUser = new User
                    {
                        Ad = firstName,
                        Soyad = lastName,
                        Username = cleanUsername,
                        PasswordHash = HashPassword(password),
                        Role = "Admin",
                        IsActive = true,
                        IsDeleted = false,
                        IsTechnician = false,
                        IsOnDuty = false,
                        TotalJobsCompleted = 0,
                        MustChangePassword = false,
                        CanViewFinance = true,
                        CanViewAnalytics = true,
                        CanDeleteRecords = true,
                        CanApprovePurchase = true,
                        CanAccessSettings = true,
                        CreatedBy = "System",
                        CreatedDate = nowUtc,
                        LastLoginDate = nowUtc
                    };

                    context.Users.Add(adminUser);
                }

                await context.SaveChangesAsync();

                _currentUser = adminUser;
                Log.Information("İlk kurulum yönetici hesabı oluşturuldu ve oturum açıldı: {Username}", adminUser.Username);
                return (true, null, adminUser);
            }
            catch (Exception ex)
            {
                var baseEx = ex.GetBaseException();
                if (baseEx is Npgsql.PostgresException pgEx)
                {
                    Log.Error(
                        ex,
                        "İlk kurulum yönetici hesabı oluşturulamadı (SqlState={SqlState}, Column={Column}, Constraint={Constraint}, Detail={Detail})",
                        pgEx.SqlState,
                        pgEx.ColumnName,
                        pgEx.ConstraintName,
                        pgEx.Detail);

                    var detailParts = new System.Collections.Generic.List<string> { $"Kod: {pgEx.SqlState}" };
                    if (!string.IsNullOrWhiteSpace(pgEx.ColumnName)) detailParts.Add($"Sütun: {pgEx.ColumnName}");
                    if (!string.IsNullOrWhiteSpace(pgEx.ConstraintName)) detailParts.Add($"Kısıt: {pgEx.ConstraintName}");
                    return (false, $"Yönetici hesabı oluşturulamadı ({string.Join(", ", detailParts)}): {pgEx.MessageText}", null);
                }

                Log.Error(ex, "İlk kurulum yönetici hesabı oluşturulamadı: {BaseMessage}", baseEx.Message);
                return (false, $"Yönetici hesabı oluşturulamadı: {baseEx.Message}", null);
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> ResetAdminPasswordAsync(string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return (false, "Yeni parola en az 6 karakter olmalıdır.");

            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var admin = await context.Users
                    .OrderBy(u => u.Id)
                    .FirstOrDefaultAsync(u => u.IsActive && u.Role.ToLower() == "admin");

                if (admin is null)
                    return (false, "Sistemde aktif bir yönetici hesabı bulunamadı.");

                admin.PasswordHash = HashPassword(newPassword);
                admin.MustChangePassword = false;
                await context.SaveChangesAsync();

                _lockoutTracker.TryRemove(admin.Username.ToLowerInvariant(), out _);
                Log.Information("Yönetici parolası kurtarma anahtarı ile sıfırlandı: {Username}", admin.Username);
                return (true, null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Yönetici parolası sıfırlanırken hata oluştu.");
                return (false, $"Parola sıfırlanamadı: {ex.Message}");
            }
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
#if DEBUG
            _hasLoggedOutManually = true;
#endif
            Log.Information("Kullanıcı oturumu kapatıldı.");
        }
    }
}
