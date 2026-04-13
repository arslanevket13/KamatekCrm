using System;
using System.Linq;
using System.Threading.Tasks;
using KamatekCrm.Shared.DTOs;
using KamatekCrm.Shared.Models;
using Serilog;

namespace KamatekCrm.Services
{
    /// <summary>
    /// API tabanli kimlik dogrulama servisi.
    /// Dogrudan veritabanina baglanmaz - tum islemler API uzerinden yapilir.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly ApiClient _apiClient;
        private readonly ITokenStorageService _tokenStorage;
        private User? _currentUser;

        public AuthService(ApiClient apiClient, ITokenStorageService tokenStorage)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _tokenStorage = tokenStorage ?? throw new ArgumentNullException(nameof(tokenStorage));
        }

        public User? CurrentUser => _currentUser;
        public bool IsLoggedIn => _currentUser != null;

        #region RBAC - Granular Permissions

        public bool CanViewFinance => _currentUser?.CanViewFinance == true || IsAdmin;
        public bool CanViewAnalytics => _currentUser?.CanViewAnalytics == true || IsAdmin;
        public bool CanDeleteRecords => _currentUser?.CanDeleteRecords == true || IsAdmin;
        public bool CanApprovePurchase => _currentUser?.CanApprovePurchase == true || IsAdmin;
        public bool CanAccessSettings => _currentUser?.CanAccessSettings == true || IsAdmin;

        #endregion

        /// <summary>
        /// API uzerinden login - JWT token alir ve saklar.
        /// </summary>
        public async Task<bool> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new Exception("Kullanici adi veya sifre bos olamaz.");

            var loginRequest = new LoginRequestDto
            {
                Username = username,
                Password = password
            };

            var response = await _apiClient.PostAsync<LoginResponseDto>("api/auth/login", loginRequest);

            if (response.Success && response.Data != null && response.Data.Success)
            {
                var loginData = response.Data;

                // JWT token'i guvenli sekilde sakla
                if (!string.IsNullOrEmpty(loginData.Token))
                {
                    await _tokenStorage.SaveTokenAsync(loginData.Token);
                }

                // CurrentUser'i olustur (API response'dan)
                _currentUser = new User
                {
                    Id = loginData.UserId,
                    Username = loginData.Username,
                    Role = loginData.Role,
                    Ad = loginData.FullName?.Split(' ').FirstOrDefault() ?? loginData.Username,
                    Soyad = loginData.FullName?.Contains(' ') == true 
                        ? string.Join(" ", loginData.FullName.Split(' ').Skip(1)) 
                        : "",
                    IsActive = true,
                    LastLoginDate = DateTime.UtcNow,
                    // Admin kullanicilari icin tum izinler aktif
                    CanViewFinance = loginData.Role == "Admin",
                    CanViewAnalytics = loginData.Role == "Admin",
                    CanDeleteRecords = loginData.Role == "Admin",
                    CanApprovePurchase = loginData.Role == "Admin",
                    CanAccessSettings = loginData.Role == "Admin"
                };

                Log.Information("API Login basarili: {Username} (Role: {Role})", loginData.Username, loginData.Role);
                return true;
            }

            // Hata mesajini logla
            var errorMsg = response.Data?.Message ?? response.Message ?? "Bilinmeyen hata";
            Log.Warning("API Login basarisiz: {Username} - {Error}", username, errorMsg);
            throw new Exception(errorMsg);
        }

        /// <summary>
        /// Oturumu kapat ve token'i temizle
        /// </summary>
        public void Logout()
        {
            _currentUser = null;
            _ = _tokenStorage.ClearTokenAsync();
            Log.Information("Kullanici oturumu kapatildi.");
        }

        public bool HasRole(string role)
        {
            return _currentUser?.Role?.Equals(role, StringComparison.OrdinalIgnoreCase) == true;
        }

        public bool IsAdmin => HasRole("Admin");
    }
}
