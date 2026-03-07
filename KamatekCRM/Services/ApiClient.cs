using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using KamatekCrm.Shared.DTOs;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Global message sent when ApiClient encounters a 401 Unauthorized response.
    /// MainWindowViewModel should register to this to show login screen.
    /// </summary>
    public class UnauthorizedMessage { }

    /// <summary>
    /// Merkezi API İstemci Servisi
    /// Tüm HTTP isteklerini sarmalar, ApiResponse<T> parse eder ve 401 hatalarını yakalar.
    /// </summary>
    public class ApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public void SetBaseUrl(string url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                _httpClient.BaseAddress = new Uri(url.TrimEnd('/') + "/");
            }
        }

        public async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
        {
            try
            {
                var response = await _httpClient.GetAsync(endpoint);
                return await ProcessResponseAsync<T>(response);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse<T>(ex);
            }
        }

        public async Task<ApiResponse<T>> PostAsync<T>(string endpoint, object data)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(endpoint, data, _jsonOptions);
                return await ProcessResponseAsync<T>(response);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse<T>(ex);
            }
        }

        public async Task<ApiResponse<T>> PutAsync<T>(string endpoint, object data)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync(endpoint, data, _jsonOptions);
                return await ProcessResponseAsync<T>(response);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse<T>(ex);
            }
        }

        public async Task<ApiResponse<T>> PatchAsync<T>(string endpoint, object data)
        {
            try
            {
                // PatchAsJsonAsync is not always available in older .NET Standard, but we can do SendAsync
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), endpoint)
                {
                    Content = JsonContent.Create(data, null, _jsonOptions)
                };
                var response = await _httpClient.SendAsync(request);
                return await ProcessResponseAsync<T>(response);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse<T>(ex);
            }
        }

        public async Task<ApiResponse<T>> DeleteAsync<T>(string endpoint)
        {
            try
            {
                var response = await _httpClient.DeleteAsync(endpoint);
                return await ProcessResponseAsync<T>(response);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse<T>(ex);
            }
        }

        private async Task<ApiResponse<T>> ProcessResponseAsync<T>(HttpResponseMessage response)
        {
            // 401 Unauthorized Intercept
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                WeakReferenceMessenger.Default.Send(new UnauthorizedMessage());
                return new ApiResponse<T>
                {
                    Success = false,
                    Message = "Oturum süreniz doldu. Lütfen tekrar giriş yapın.",
                    Errors = new List<string> { "401 Unauthorized" }
                };
            }

            try
            {
                var content = await response.Content.ReadAsStringAsync();
                
                // Try parse as ApiResponse<T>
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(content, _jsonOptions);
                
                if (apiResponse != null)
                {
                    // Ensure Success flag matches HTTP status if not explicitly set by API
                    if (!response.IsSuccessStatusCode && apiResponse.Success)
                    {
                        apiResponse.Success = false;
                    }
                    return apiResponse;
                }
            }
            catch 
            {
                // Fallback parsing failed
            }

            // Fallback for non-standard responses
            if (response.IsSuccessStatusCode)
            {
                 return new ApiResponse<T>
                 {
                     Success = true,
                     Message = "İşlem başarılı"
                 };
            }

            return new ApiResponse<T>
            {
                Success = false,
                Message = $"Sunucu hatası: {response.StatusCode}",
                Errors = new List<string> { response.ReasonPhrase ?? "Unknown Error" }
            };
        }

        private ApiResponse<T> CreateErrorResponse<T>(Exception ex)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = "Sunucuya bağlanılamadı. İnternet bağlantınızı kontrol edin.",
                Errors = new List<string> { ex.Message }
            };
        }
    }
}
