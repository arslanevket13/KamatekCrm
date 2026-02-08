using System;

namespace KamatekCrm.Shared.DTOs
{
    public class LoginRequestDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int UserId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }
}
