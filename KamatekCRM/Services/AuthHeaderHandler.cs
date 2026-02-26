using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace KamatekCrm.Services
{
    /// <summary>
    /// HTTP Message Handler - Automatically attaches JWT Bearer token to every outgoing request
    /// </summary>
    public class AuthHeaderHandler : DelegatingHandler
    {
        private readonly ITokenStorageService _tokenStorage;

        public AuthHeaderHandler(ITokenStorageService tokenStorage)
        {
            _tokenStorage = tokenStorage ?? throw new ArgumentNullException(nameof(tokenStorage));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            // Get token from secure storage
            var token = await _tokenStorage.GetTokenAsync();

            // Attach token if available
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = 
                    new AuthenticationHeaderValue("Bearer", token);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// Token Storage Interface - Secure storage for JWT tokens
    /// </summary>
    public interface ITokenStorageService
    {
        Task<string?> GetTokenAsync();
        Task SaveTokenAsync(string token);
        Task ClearTokenAsync();
    }

    /// <summary>
    /// Simple file-based token storage (for desktop app)
    /// Uses Windows Data Protection API for encryption
    /// </summary>
    public class FileTokenStorageService : ITokenStorageService
    {
        private readonly string _tokenFilePath;
        private readonly object _lock = new();

        public FileTokenStorageService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KamatekCRM");
            
            Directory.CreateDirectory(appDataPath);
            _tokenFilePath = Path.Combine(appDataPath, ".token");
        }

        public Task<string?> GetTokenAsync()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_tokenFilePath))
                    {
                        var encryptedToken = File.ReadAllText(_tokenFilePath);
                        var tokenBytes = Convert.FromBase64String(encryptedToken);
                        var decryptedBytes = ProtectedData.Unprotect(
                            tokenBytes,
                            null,
                            DataProtectionScope.CurrentUser);
                        return Task.FromResult<string?>(System.Text.Encoding.UTF8.GetString(decryptedBytes));
                    }
                }
                catch (Exception)
                {
                    // Token invalid or expired - return null
                }
                return Task.FromResult<string?>(null);
            }
        }

        public Task SaveTokenAsync(string token)
        {
            lock (_lock)
            {
                try
                {
                    var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
                    var encryptedToken = ProtectedData.Protect(
                        tokenBytes,
                        null,
                        DataProtectionScope.CurrentUser);
                    File.WriteAllText(_tokenFilePath, Convert.ToBase64String(encryptedToken));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Token kaydetme hatası: {ex.Message}");
                }
            }
            return Task.CompletedTask;
        }

        public Task ClearTokenAsync()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_tokenFilePath))
                    {
                        File.Delete(_tokenFilePath);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Token silme hatası: {ex.Message}");
                }
            }
            return Task.CompletedTask;
        }
    }
}
