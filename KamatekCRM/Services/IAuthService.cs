using KamatekCrm.Shared.Models;

namespace KamatekCrm.Services
{
    public interface IAuthService
    {
        User? CurrentUser { get; }
        bool IsLoggedIn { get; }
        bool IsAdmin { get; }
        bool Login(string username, string password);
        void Logout();
        void CreateDefaultUser();
        string HashPassword(string password);

        // RBAC
        bool CanViewFinance { get; }
        bool CanViewAnalytics { get; }
        bool CanDeleteRecords { get; }
        bool CanApprovePurchase { get; }
        bool CanAccessSettings { get; }
    }
}
