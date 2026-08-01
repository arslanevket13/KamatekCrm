using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;

namespace KamatekCrm.Tests;

internal sealed class TestAuthorizationService : IApplicationAuthorizationService
{
    private readonly bool _isAuthorized;
    private readonly string _error;

    public TestAuthorizationService(bool isAuthorized = true, string error = "Test kullanıcısı yetkisiz.")
    {
        _isAuthorized = isAuthorized;
        _error = error;
    }

    public bool IsAuthorized(ApplicationPermission permission) => _isAuthorized;

    public Result Authorize(ApplicationPermission permission) =>
        _isAuthorized ? Result.Success() : Result.Failure(_error);
}
