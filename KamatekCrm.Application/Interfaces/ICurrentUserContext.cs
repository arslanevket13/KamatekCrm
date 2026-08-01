using KamatekCrm.ApplicationCore.Security;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    int? UserId { get; }
    string Username { get; }
    string Role { get; }
    bool HasPermission(ApplicationPermission permission);
}
