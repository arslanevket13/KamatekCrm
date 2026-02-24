using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace KamatekCrm.Services
{
    /// <summary>
    /// SMS Gönderim Servisi
    /// HTTP API üzerinden SMS providera bağlanarak mesaj gönderir.
    /// 
    /// ÖNEMLİ PRODUCTION AYARLARI:
    /// 1. ApiUrl: SMS sağlayıcınızın API endpoint URL'i (NetGSM, Twilio, vb.)
    /// 2. ApiKey: SMS sağlayıcınızdan aldığınız API anahtarı
    /// 3. SenderTitle: BTK'ya kayıtlı başlık (alfanumerik, max 11 karakter)
    /// 
    /// Örnek providers:
    /// - NetGSM: https://www.netgsm.com.tr/
    /// - Twilio: https://www.twilio.com/
    /// - Vatansms: https://www.vatansms.com/
    /// </summary>
    public class SmsService
    {
        private readonly HttpClient _httpClient;
        
        // ═══════════════════════════════════════════════════════════════════
        // ⚠️ PRODUCTION: Aşağıdaki değerleri gerçek API bilgileriyle değiştirin
        // ═══════════════════════════════════════════════════════════════════
        private const string ApiUrl = "https://api.netgsm.com.tr/sms/send/json"; // NetGSM örneği
        private const string ApiKey = "YOUR_API_KEY_HERE"; // Gerçek API anahtarınız
        private const string ApiSecret = "YOUR_API_SECRET_HERE"; // Gerçek API secret
        private const string SenderTitle = "KAMATEK"; // BTK kayıtlı başlık

        public SmsService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
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
                    // Demo mod - gerçek API çağrısı yapma
                    await Task.Delay(300);
                    System.Diagnostics.Debug.WriteLine($"[SMS DEMO] → {cleanNumber}: {message}");
                }
                else
                {
                    // Production mod - gerçek API çağrısı
                    var response = await _httpClient.PostAsync(ApiUrl, content);
                    response.EnsureSuccessStatusCode();
                    
                    var responseBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[SMS SENT] → {cleanNumber} | Response: {responseBody}");
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
