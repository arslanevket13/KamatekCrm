using KamatekCrm.Shared.Models;

namespace KamatekCrm.Services;

public interface IForcedPasswordChangeService
{
    Task<bool> RequireChangeAsync(User user);
}
