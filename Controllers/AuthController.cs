using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KamatekCrm.Services;
using KamatekCrm.Shared.DTOs;

namespace KamatekCrm.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IJwtService _jwtService;

        public AuthController(IAuthService authService, IJwtService jwtService)
        {
            _authService = authService;
            _jwtService = jwtService;
        }

        [HttpPost("login")]
        public ActionResult<LoginResponseDto> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                if (_authService.Login(request.Username, request.Password))
                {
                    var user = _authService.CurrentUser;
                    if (user == null) return Unauthorized();

                    var token = _jwtService.GenerateToken(user);

                    return Ok(new LoginResponseDto
                    {
                        Token = token,
                        UserId = user.Id,
                        Username = user.Username,
                        FullName = $"{user.Ad} {user.Soyad}",
                        Role = user.Role
                    });
                }
                return Unauthorized("Kullanıcı adı veya şifre hatalı");
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }
}
