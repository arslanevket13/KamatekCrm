using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;

namespace KamatekCrm.Services;

/// <summary>
/// WPF oturumunu Application katmanının kullanıcı-bağımsız yetki sözleşmesine uyarlar.
/// </summary>
public sealed class DesktopCurrentUserContext : ICurrentUserContext
{
    private readonly IAuthService _authService;

    public DesktopCurrentUserContext(IAuthService authService)
    {
        _authService = authService;
    }

    public bool IsAuthenticated => _authService.IsLoggedIn;
    public int? UserId => _authService.CurrentUser?.Id;
    public string Username => _authService.CurrentUser?.Username ?? "Sistem";
    public string Role => _authService.CurrentUser?.Role ?? string.Empty;

    public bool HasPermission(ApplicationPermission permission)
    {
        if (_authService.IsAdmin) return true;

        return permission switch
        {
            ApplicationPermission.ManageServiceJobs => HasRole("Technician") || HasRole("Personel"),
            ApplicationPermission.ExecuteSales => HasRole("Technician"),
            ApplicationPermission.AdjustInventory => _authService.CanDeleteRecords,
            ApplicationPermission.ApprovePurchase => _authService.CanApprovePurchase,
            ApplicationPermission.ViewFinance => _authService.CanViewFinance,
            ApplicationPermission.DeleteRecords => _authService.CanDeleteRecords,
            ApplicationPermission.AccessSettings => _authService.CanAccessSettings,
            ApplicationPermission.ManageUsers => false,
            ApplicationPermission.ViewCustomerContactData => HasRole("Technician") || HasRole("Personel"),
            ApplicationPermission.ViewCustomerIdentityData => _authService.CanViewFinance,
            ApplicationPermission.ProcessReturns => _authService.CanDeleteRecords,
            ApplicationPermission.ManageQuotes => HasRole("Technician") || HasRole("Personel"),
            _ => false
        };
    }

    private bool HasRole(string role) =>
        string.Equals(Role, role, StringComparison.OrdinalIgnoreCase);
}
