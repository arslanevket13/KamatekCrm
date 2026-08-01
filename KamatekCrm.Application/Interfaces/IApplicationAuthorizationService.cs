using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.Security;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IApplicationAuthorizationService
{
    bool IsAuthorized(ApplicationPermission permission);
    Result Authorize(ApplicationPermission permission);
}
