using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Kimlik doğrulama servisi
    /// </summary>
    public class AuthService : IAuthService
    {
        private User? _currentUser;

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

            using var context = new AppDbContext();

            // 1. ADIM: Kullanıcıyı sadece ismine göre bul (Debug için)
            var targetUser = context.Users.FirstOrDefault(u => u.Username.ToLower() == username.ToLower());

            if (targetUser == null)
            {
                // Veritabanındaki tüm kullanıcıları listele (Debug için)
                var allUsers = string.Join(", ", context.Users.Select(u => u.Username).ToList());
                throw new Exception($"Kullanıcı '{username}' bulunamadı.\nVeritabanındaki Kullanıcılar: [{allUsers}]");
            }

            // 2. ADIM: Şifre kontrolü
            var inputHash = HashPassword(password);
            if (targetUser.PasswordHash != inputHash)
            {
                throw new Exception($"Şifre hatalı!\n\nDB Hash: {targetUser.PasswordHash}\nGirdi Hash: {inputHash}");
            }

            // 3. ADIM: Aktiflik kontrolü
            if (!targetUser.IsActive)
            {
                throw new Exception("Kullanıcı hesabı pasif durumda.");
            }

            // Başarılı Giriş
            _currentUser = targetUser;
            targetUser.LastLoginDate = DateTime.UtcNow;
            context.SaveChanges();

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
        /// Varsayılan admin kullanıcısını oluştur (eğer yoksa)
        /// </summary>
        public void CreateDefaultUser()
        {
            using var context = new AppDbContext();

            // Admin kullanıcısı var mı diye özel olarak bak
            var adminUser = context.Users.FirstOrDefault(u => u.Username == "admin");

            if (adminUser == null)
            {
                adminUser = new User
                {
                    Username = "admin",
                    PasswordHash = HashPassword("123"),
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

                context.Users.Add(adminUser);
                context.SaveChanges();
            }
            else if (!adminUser.CanViewFinance)
            {
                // Mevcut admin kullanıcısına izinleri ekle (migration sonrası)
                adminUser.CanViewFinance = true;
                adminUser.CanViewAnalytics = true;
                adminUser.CanDeleteRecords = true;
                adminUser.CanApprovePurchase = true;
                adminUser.CanAccessSettings = true;
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Şifreyi SHA256 ile hashle
        /// </summary>
        /// <param name="password">Plain text şifre</param>
        /// <returns>Hash string</returns>
        public string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
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
