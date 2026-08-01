using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace KamatekCrm.Services
{
    public class SmsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SmsService>? _logger;
        
        private const string ApiUrl = "https://api.netgsm.com.tr/sms/send/json";
        private const string ApiKey = "YOUR_API_KEY_HERE";
        private const string ApiSecret = "YOUR_API_SECRET_HERE";
        private const string SenderTitle = "KAMATEK";

        public SmsService(IHttpClientFactory httpClientFactory, ILogger<SmsService>? logger = null)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger;
        }

        /// <summary>
        /// SMS gönderir
        /// </summary>
        /// <param name="phoneNumber">Alıcı telefon numarası (5xxxxxxxxx formatı)</param>
        /// <param name="message">Gönderilecek mesaj</param>
        public async Task SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                // Telefon numarasını temizle (boşluk, parantez, tire kaldır)
                var cleanNumber = phoneNumber?
                    .Replace(" ", "")
                    .Replace("(", "")
                    .Replace(")", "")
                    .Replace("-", "")
                    .Replace("+90", "")
                    .TrimStart('0');

                if (string.IsNullOrEmpty(cleanNumber) || cleanNumber.Length < 10)
                    throw new ArgumentException("Telefon numarası geçersiz.");

                // NetGSM API formatı (diğer sağlayıcılar için değiştirilmeli)
                var payload = new
                {
                    usercode = ApiKey,
                    password = ApiSecret,
                    gsmno = cleanNumber,
                    message = message,
                    msgheader = SenderTitle
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // ═══════════════════════════════════════════════════════════════════
                // PRODUCTION: API çağrısı aktif
                // Not: ApiKey/ApiSecret "YOUR_API_..." ise demo mod'da çalışır
                // ═══════════════════════════════════════════════════════════════════
                if (ApiKey.StartsWith("YOUR_") || ApiSecret.StartsWith("YOUR_"))
                {
                    await Task.Delay(300);
                    _logger?.LogInformation("[SMS DEMO] → {CleanNumber}: {Message}", cleanNumber, message);
                }
                else
                {
                    var client = _httpClientFactory.CreateClient("SmsClient");
                    var response = await client.PostAsync(ApiUrl, content);
                    response.EnsureSuccessStatusCode();
                    
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger?.LogInformation("[SMS SENT] → {CleanNumber} | Response: {ResponseBody}", cleanNumber, responseBody);
                }
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"SMS API bağlantı hatası: {ex.Message}", ex);
            }
            catch (TaskCanceledException)
            {
                throw new Exception("SMS gönderimi zaman aşımına uğradı.");
            }
            catch (Exception ex)
            {
                throw new Exception($"SMS gönderilemedi: {ex.Message}", ex);
            }
        }
    }
}
