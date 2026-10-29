using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Configuration;

namespace KamatekCrm.Services;

/// <summary>
/// WPF oturumunu Application katmanının kullanıcı-bağımsız yetki sözleşmesine uyarlar.
/// </summary>
public sealed class DesktopCurrentUserContext : ICurrentUserContext
{
    private readonly IAuthService _authService;
    private readonly DiagnosticOptions? _diagnosticOptions;

    public DesktopCurrentUserContext(IAuthService authService, DiagnosticOptions? diagnosticOptions = null)
    {
        _authService = authService;
        _diagnosticOptions = diagnosticOptions;
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
            // Geliştirici Merkezi yalnızca appsettings → Diagnostics:DeveloperAccounts listesindekilere açıktır.
            // Admin rolü tek başına yeterli değildir; bu ayrım bilinçli olarak yapılmıştır.
            ApplicationPermission.AccessDiagnostics => IsListedDeveloperAccount(),
            _ => false
        };
    }

    private bool IsListedDeveloperAccount()
    {
        var username = _authService.CurrentUser?.Username;
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        var accounts = _diagnosticOptions?.DeveloperAccounts;
        return accounts is { Count: > 0 } &&
               accounts.Any(account => string.Equals(account, username, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasRole(string role) =>
        string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Role, role, StringComparison.OrdinalIgnoreCase);

    public void EnsureAuthorized(ApplicationPermission permission)
    {
        if (!IsAuthenticated)
            throw new UnauthorizedAccessException("Bu işlem için oturum açılmalıdır.");

        if (!HasPermission(permission))
            throw new UnauthorizedAccessException($"Bu işlem için yetkiniz bulunmuyor ({permission}).");
    }

    public void EnsureRole(params string[] allowedRoles)
    {
        if (!IsAuthenticated)
            throw new UnauthorizedAccessException("Bu işlem için oturum açılmalıdır.");

        if (string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase))
            return;

        if (allowedRoles.Any(r => string.Equals(Role, r, StringComparison.OrdinalIgnoreCase)))
            return;

        throw new UnauthorizedAccessException("Bu işlem için yetkiniz bulunmuyor.");
    }
}
