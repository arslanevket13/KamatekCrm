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
    }
}
