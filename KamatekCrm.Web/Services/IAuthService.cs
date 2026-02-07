using KamatekCrm.Shared.DTOs;
using KamatekCrm.Shared.Models;
using System.Threading.Tasks;

namespace KamatekCrm.Web.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest loginRequest);
        Task LogoutAsync();
    }
}
