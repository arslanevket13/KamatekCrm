using KamatekCrm.Shared.Models;

namespace KamatekCrm.Services
{
    public interface IAuthService
    {
        User? CurrentUser { get; }
        bool IsLoggedIn { get; }
        bool IsAdmin { get; }
        
        /// <summary>
        /// API uzerinden asenkron login (JWT tabanli)
        /// </summary>
        Task<bool> LoginAsync(string username, string password);

#if DEBUG
        /// <summary>
        /// Kullanıcı mevcut uygulama oturumunda manuel olarak çıkış yaptı mı?
        /// </summary>
        bool HasLoggedOutManually => false;

        /// <summary>
        /// Yalnızca geliştirme (DEBUG) ortamında aktif Admin hesabıyla parola doğrulaması olmadan hızlı oturum açar.
        /// </summary>
        Task<bool> DevQuickLoginAsAdminAsync(string? preferredUsername = null) => Task.FromResult(false);
#endif

        /// <summary>
        /// DPAPI ile saklanan oturum bilgisini doğrulayarak sessiz otomatik giriş yapar.
        /// </summary>
        Task<bool> TryAutoLoginAsync(int userId, string username);

        /// <summary>
        /// İlk kurulum sihirbazı üzerinden işletme yöneticisi (Admin) hesabını oluşturur ve oturumu açar.
        /// </summary>
        Task<(bool Success, string? ErrorMessage, User? CreatedUser)> CreateInitialAdminAsync(
            string fullName,
            string username,
            string password);

        /// <summary>
        /// Çevrimdışı kurtarma anahtarı doğrulandıktan sonra aktif yönetici parolasını sıfırlar.
        /// </summary>
        Task<(bool Success, string? ErrorMessage)> ResetAdminPasswordAsync(string newPassword);
        
        /// <summary>
        /// Oturumu kapat
        /// </summary>
        void Logout();

        // RBAC
        bool CanViewFinance { get; }
        bool CanViewAnalytics { get; }
        bool CanDeleteRecords { get; }
        bool CanApprovePurchase { get; }
        bool CanAccessSettings { get; }
        bool HasRole(string role);
        void EnsureAuthorized(KamatekCrm.ApplicationCore.Security.ApplicationPermission permission);
    }
}
