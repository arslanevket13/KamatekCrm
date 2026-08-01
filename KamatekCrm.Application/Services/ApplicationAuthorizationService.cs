using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;

namespace KamatekCrm.ApplicationCore.Services;

public sealed class ApplicationAuthorizationService : IApplicationAuthorizationService
{
    private readonly ICurrentUserContext _currentUser;

    public ApplicationAuthorizationService(ICurrentUserContext currentUser)
    {
        _currentUser = currentUser;
    }

    public bool IsAuthorized(ApplicationPermission permission) =>
        _currentUser.IsAuthenticated && _currentUser.HasPermission(permission);

    public Result Authorize(ApplicationPermission permission)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure("Bu işlem için oturum açılmalıdır.");
        }

        return _currentUser.HasPermission(permission)
            ? Result.Success()
            : Result.Failure($"'{GetDisplayName(permission)}' işlemi için yetkiniz bulunmuyor.");
    }

    private static string GetDisplayName(ApplicationPermission permission) => permission switch
    {
        ApplicationPermission.ManageServiceJobs => "Servis işi yönetimi",
        ApplicationPermission.ExecuteSales => "Satış tamamlama",
        ApplicationPermission.AdjustInventory => "Stok değiştirme",
        ApplicationPermission.ApprovePurchase => "Satın alma onayı",
        ApplicationPermission.ViewFinance => "Finans erişimi",
        ApplicationPermission.DeleteRecords => "Kayıt silme",
        ApplicationPermission.AccessSettings => "Sistem ayarları",
        ApplicationPermission.ManageUsers => "Kullanıcı yönetimi",
        ApplicationPermission.ViewCustomerContactData => "Müşteri iletişim bilgileri",
        ApplicationPermission.ViewCustomerIdentityData => "Müşteri kimlik ve vergi bilgileri",
        _ => permission.ToString()
    };
}
