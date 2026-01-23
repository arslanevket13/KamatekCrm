using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using KamatekCrm.Data;
using KamatekCrm.Models;

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

        /// <summary>
        /// Kullanıcı adı ve şifre ile giriş yap
        /// </summary>
        /// <param name="username">Kullanıcı adı</param>
        /// <param name="password">Şifre (plain text)</param>
        /// <returns>Başarılı ise true</returns>
        public static bool Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return false;

            using var context = new AppDbContext();

            var passwordHash = HashPassword(password);
            var user = context.Users.FirstOrDefault(u =>
                u.Username.ToLower() == username.ToLower() &&
                u.PasswordHash == passwordHash &&
                u.IsActive);

            if (user != null)
            {
                CurrentUser = user;

                // Son giriş tarihini güncelle
                user.LastLoginDate = DateTime.Now;
                context.SaveChanges();

                return true;
            }

            return false;
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

            // Eğer hiç kullanıcı yoksa admin oluştur
            if (!context.Users.Any())
            {
                var adminUser = new User
                {
                    Username = "admin.user",  // Ad.Soyad formatında
                    PasswordHash = HashPassword("1234"),
                    Role = "Admin",  // Arayüzde "Patron" olarak gösterilir
                    Ad = "Admin",
                    Soyad = "User",
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                context.Users.Add(adminUser);
                context.SaveChanges();
            }
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
