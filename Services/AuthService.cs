using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Kimlik doğrulama servisi
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private User? _currentUser;

        public AuthService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Şu anda oturum açmış kullanıcı
        /// </summary>
        public User? CurrentUser => _currentUser;

        /// <summary>
        /// Oturum açık mı?
        /// </summary>
        public bool IsLoggedIn => _currentUser != null;

        #region RBAC - Granular Permissions

        /// <summary>
        /// Finans modülünü görme yetkisi
        /// </summary>
        public bool CanViewFinance => _currentUser?.CanViewFinance == true || IsAdmin;

        /// <summary>
        /// Analitik dashboard görme yetkisi
        /// </summary>
        public bool CanViewAnalytics => _currentUser?.CanViewAnalytics == true || IsAdmin;

        /// <summary>
        /// Kayıt silme yetkisi
        /// </summary>
        public bool CanDeleteRecords => _currentUser?.CanDeleteRecords == true || IsAdmin;

        /// <summary>
        /// Satın alma onaylama yetkisi
        /// </summary>
        public bool CanApprovePurchase => _currentUser?.CanApprovePurchase == true || IsAdmin;

        /// <summary>
        /// Ayarlara erişim yetkisi
        /// </summary>
        public bool CanAccessSettings => _currentUser?.CanAccessSettings == true || IsAdmin;

        #endregion

        /// <summary>
        /// Kullanıcı adı ve şifre ile giriş yap
        /// </summary>
        /// <param name="username">Kullanıcı adı</param>
        /// <param name="password">Şifre (plain text)</param>
        /// <returns>Başarılı ise true</returns>
        public bool Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new Exception("Kullanıcı adı veya şifre boş olamaz.");

            // Kullanıcıyı bul (case-insensitive)
            var targetUser = _context.Users
                .AsNoTracking()
                .FirstOrDefault(u => EF.Functions.ILike(u.Username, username));

            if (targetUser == null)
            {
                // Güvenlik: Kullanıcı yoksa bile aynı mesajı göster (user enumeration önlemi)
                throw new Exception("Kullanıcı adı veya şifre hatalı.");
            }

            // Şifre kontrolü
            if (!VerifyPassword(password, targetUser.PasswordHash))
            {
                throw new Exception("Kullanıcı adı veya şifre hatalı.");
            }

            // Aktiflik kontrolü
            if (!targetUser.IsActive)
            {
                throw new Exception("Kullanıcı hesabı pasif durumda.");
            }

            // Başarılı Giriş
            _currentUser = targetUser;
            
            // Son giriş tarihini güncelle (tracking gerekli)
            var userToUpdate = _context.Users.Find(targetUser.Id);
            if (userToUpdate != null)
            {
                userToUpdate.LastLoginDate = DateTime.UtcNow;
                _context.SaveChanges();
            }

            return true;
        }

        /// <summary>
        /// Oturumu kapat
        /// </summary>
        public void Logout()
        {
            _currentUser = null;
        }

        /// <summary>
        /// Varsayılan admin kullanıcısını oluştur veya şifresini resetle
        /// </summary>
        public void CreateDefaultUser()
        {
            // Admin kullanıcısı var mı diye özel olarak bak
            var adminUser = _context.Users.FirstOrDefault(u => u.Username == "admin");

            if (adminUser == null)
            {
                // Admin kullanıcısı yoksa oluştur
                adminUser = new User
                {
                    Username = "admin",
                    PasswordHash = HashPassword("123"), // Varsayılan şifre: 123
                    Role = "Admin",
                    Ad = "Admin",
                    Soyad = "User",
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    // Admin için tüm izinler aktif
                    CanViewFinance = true,
                    CanViewAnalytics = true,
                    CanDeleteRecords = true,
                    CanApprovePurchase = true,
                    CanAccessSettings = true
                };

                _context.Users.Add(adminUser);
            }
            else
            {
                // Admin kullanıcısı varsa şifresini her zaman "123" olarak güncelle
                adminUser.PasswordHash = HashPassword("123");
                
                // Eksik izinleri ekle
                adminUser.CanViewFinance = true;
                adminUser.CanViewAnalytics = true;
                adminUser.CanDeleteRecords = true;
                adminUser.CanApprovePurchase = true;
                adminUser.CanAccessSettings = true;
            }

            _context.SaveChanges();
        }

        /// <summary>
        /// Şifreyi PBKDF2 ile hashle (güvenli)
        /// </summary>
        /// <param name="password">Plain text şifre</param>
        /// <returns>Hash string (salt:hash formatında)</returns>
        public string HashPassword(string password)
        {
            // 16 byte rastgele salt
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // PBKDF2 ile hash (100,000 iterasyon)
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32);

            // Salt ve hash'i birleştir: salt(16) + hash(32) = 48 byte
            byte[] hashBytes = new byte[48];
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 32);

            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Şifreyi doğrula
        /// </summary>
        /// <param name="password">Plain text şifre</param>
        /// <param name="storedHash">Kayıtlı hash</param>
        /// <returns>Doğru ise true</returns>
        private bool VerifyPassword(string password, string storedHash)
        {
            byte[] hashBytes = Convert.FromBase64String(storedHash);
            
            // Salt'i ayır
            byte[] salt = new byte[16];
            Array.Copy(hashBytes, 0, salt, 0, 16);

            // Şifreyi hashle
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32);

            // Hash'leri karşılaştır
            for (int i = 0; i < 32; i++)
            {
                if (hashBytes[i + 16] != hash[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Kullanıcının belirli bir role sahip olup olmadığını kontrol et
        /// </summary>
        /// <param name="role">Kontrol edilecek rol</param>
        /// <returns>Role sahipse true</returns>
        public bool HasRole(string role)
        {
            return _currentUser?.Role?.Equals(role, StringComparison.OrdinalIgnoreCase) == true;
        }

        /// <summary>
        /// Kullanıcının Admin olup olmadığını kontrol et
        /// </summary>
        public bool IsAdmin => HasRole("Admin");
    }
}
