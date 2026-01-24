using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace KamatekCrm.Services
{
    public class SmsService
    {
        private readonly HttpClient _httpClient;
        
        // Demo API settings (örnek olarak NetGSM veya benzeri yapı)
        private const string ApiUrl = "https://api.sms-provider.com/v1/send";
        private const string ApiKey = "demo_key_12345";
        private const string SenderTitle = "KAMATEK";

        public SmsService()
        {
            _httpClient = new HttpClient();
        }

        public async Task SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                // Telefon numarasını temizle
                var cleanNumber = phoneNumber?.Replace(" ", "").Replace("(", "").Replace(")", "").Replace("-", "");
                if (string.IsNullOrEmpty(cleanNumber)) throw new ArgumentException("Telefon numarası geçersiz.");

                // Demo payload
                var payload = new
                {
                    key = ApiKey,
                    phone = cleanNumber,
                    message = message,
                    sender = SenderTitle
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Gerçek API olmadığı için başarılı gibi davranıyoruz (Loglama yapılabilir)
                // var response = await _httpClient.PostAsync(ApiUrl, content);
                // response.EnsureSuccessStatusCode();

                // Simüle edilmiş gecikme ve başarı
                await Task.Delay(500); 
                
                // NOT: Gerçek entegrasyonda burası aktif edilmeli.
                // Şimdilik sadece konsol çıktısı veya debug log.
                System.Diagnostics.Debug.WriteLine($"SMS SENT to {cleanNumber}: {message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"SMS gönderilemedi: {ex.Message}", ex);
            }
        }
    }
}
