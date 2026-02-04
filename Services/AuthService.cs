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
    public static class AuthService
    {
        /// <summary>
        /// Şu anda oturum açmış kullanıcı
        /// </summary>
        public static User? CurrentUser { get; private set; }

        /// <summary>
        /// Oturum açık mı?
        /// </summary>
        public static bool IsLoggedIn => CurrentUser != null;

        #region RBAC - Granular Permissions

        /// <summary>
        /// Finans modülünü görme yetkisi
        /// </summary>
        public static bool CanViewFinance => CurrentUser?.CanViewFinance == true || IsAdmin;

        /// <summary>
        /// Analitik dashboard görme yetkisi
        /// </summary>
        public static bool CanViewAnalytics => CurrentUser?.CanViewAnalytics == true || IsAdmin;

        /// <summary>
        /// Kayıt silme yetkisi
        /// </summary>
        public static bool CanDeleteRecords => CurrentUser?.CanDeleteRecords == true || IsAdmin;

        /// <summary>
        /// Satın alma onaylama yetkisi
        /// </summary>
        public static bool CanApprovePurchase => CurrentUser?.CanApprovePurchase == true || IsAdmin;

        /// <summary>
        /// Ayarlara erişim yetkisi
        /// </summary>
        public static bool CanAccessSettings => CurrentUser?.CanAccessSettings == true || IsAdmin;

        #endregion

        /// <summary>
        /// Kullanıcı adı ve şifre ile giriş yap
        /// </summary>
        /// <param name="username">Kullanıcı adı</param>
        /// <param name="password">Şifre (plain text)</param>
        /// <returns>Başarılı ise true</returns>
        public static bool Login(string username, string password)
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
            CurrentUser = targetUser;
            targetUser.LastLoginDate = DateTime.Now;
            context.SaveChanges();

            return true;
        }

        /// <summary>
        /// Oturumu kapat
        /// </summary>
        public static void Logout()
        {
            CurrentUser = null;
        }

        /// <summary>
        /// Varsayılan admin kullanıcısını oluştur (eğer yoksa)
        /// </summary>
        public static void CreateDefaultUser()
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
                    CreatedDate = DateTime.Now,
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
            // Eski 'admin.user' varsa ve şifresi '1234' ise, onu da güncel veya yedek olarak tutabiliriz
            // Ama şimdilik sadece 'admin' garantisi veriyoruz.
        }

        /// <summary>
        /// Şifreyi SHA256 ile hashle
        /// </summary>
        /// <param name="password">Plain text şifre</param>
        /// <returns>Hash string</returns>
        public static string HashPassword(string password)
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
        public static bool HasRole(string role)
        {
            return CurrentUser?.Role?.Equals(role, StringComparison.OrdinalIgnoreCase) == true;
        }

        /// <summary>
        /// Kullanıcının Admin olup olmadığını kontrol et
        /// </summary>
        public static bool IsAdmin => HasRole("Admin");
    }
}
