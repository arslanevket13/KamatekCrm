using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace KamatekCrm.Services
{
    /// <summary>
    /// E-Posta Gönderim Servisi (SMTP)
    /// Gmail veya diğer SMTP sunucuları üzerinden e-posta gönderir.
    /// 
    /// ═══════════════════════════════════════════════════════════════════════
    /// ⚠️ ÖNEMLİ GÜVENLİK BİLGİSİ - GOOGLE APP PASSWORD
    /// ═══════════════════════════════════════════════════════════════════════
    /// 
    /// Gmail kullanıyorsanız, normal Gmail şifrenizi KULLANAMAZSINIZ!
    /// Google "Less Secure Apps" özelliğini 2022'de kapattı.
    /// 
    /// YAPILMASI GEREKENLER:
    /// 1. Google Hesabınızda 2 Adımlı Doğrulama AÇIK olmalı
    /// 2. https://myaccount.google.com/apppasswords adresine gidin
    /// 3. "Uygulama parolası oluştur" seçin
    /// 4. Uygulama: "Posta", Cihaz: "Windows Bilgisayar" seçin
    /// 5. OLuşturulan 16 karakterlik şifreyi SenderPassword'a yapıştırın
    ///    (Örnek format: "abcd efgh ijkl mnop" → "abcdefghijklmnop")
    /// 
    /// DİĞER SMTP SUNUCULARI:
    /// - Outlook: smtp.office365.com, Port 587
    /// - Yahoo: smtp.mail.yahoo.com, Port 587
    /// - Yandex: smtp.yandex.com, Port 587
    /// ═══════════════════════════════════════════════════════════════════════
    /// </summary>
    public class EmailService
    {
        // SMTP Sunucu Ayarları
        private const string SmtpHost = "smtp.gmail.com";
        private const int SmtpPort = 587; // TLS portu, DEĞİŞTİRMEYİN
        private const bool EnableSsl = true; // Güvenli bağlantı, DEĞİŞTİRMEYİN

        // ═══════════════════════════════════════════════════════════════════
        // ⚠️ PRODUCTION: Aşağıdaki değerleri gerçek bilgilerle değiştirin
        // ═══════════════════════════════════════════════════════════════════
        private const string SenderEmail = "your-email@gmail.com"; // Gönderici e-posta
        private const string SenderPassword = "xxxx xxxx xxxx xxxx"; // 16 karakterlik App Password (boşluksuz)
        private const string SenderDisplayName = "Kamatek Teknik Servis";

        /// <summary>
        /// PDF teklif dosyasını e-posta ile gönderir
        /// </summary>
        /// <param name="toEmail">Alıcı e-posta adresi</param>
        /// <param name="subject">E-posta konusu</param>
        /// <param name="body">E-posta içeriği (HTML destekler)</param>
        /// <param name="attachmentPath">Ek dosya yolu (opsiyonel)</param>
        public async Task SendQuoteEmailAsync(string toEmail, string subject, string body, string? attachmentPath = null)
        {
            // Placeholder kontrolü
            if (SenderEmail.Contains("your-email") || SenderPassword == "xxxx xxxx xxxx xxxx")
            {
                throw new Exception(
                    "E-posta ayarları yapılandırılmamış!\n\n" +
                    "EmailService.cs dosyasındaki:\n" +
                    "• SenderEmail: Gerçek Gmail adresiniz\n" +
                    "• SenderPassword: Google App Password (16 karakter)\n\n" +
                    "değerlerini güncelleyin. Detaylar için dosyadaki yorumları okuyun.");
            }

            try
            {
                using var mail = new MailMessage();
                mail.From = new MailAddress(SenderEmail, SenderDisplayName);
                mail.To.Add(toEmail);
                mail.Subject = subject;
                mail.Body = body;
                mail.IsBodyHtml = true;

                // Ek dosya varsa ekle
                Attachment? attachment = null;
                if (!string.IsNullOrEmpty(attachmentPath) && System.IO.File.Exists(attachmentPath))
                {
                    attachment = new Attachment(attachmentPath);
                    mail.Attachments.Add(attachment);
                }

                try
                {
                    using var smtp = new SmtpClient(SmtpHost, SmtpPort);
                    smtp.Credentials = new NetworkCredential(SenderEmail, SenderPassword.Replace(" ", "")); // Boşlukları temizle
                    smtp.EnableSsl = EnableSsl;
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.Timeout = 30000; // 30 saniye timeout

                    await smtp.SendMailAsync(mail);
                }
                finally
                {
                    // Attachment'ı dispose et (dosya kilidini serbest bırak)
                    attachment?.Dispose();
                }
            }
            catch (SmtpException ex) when (ex.Message.Contains("authentication") || ex.Message.Contains("535"))
            {
                throw new Exception(
                    "Gmail kimlik doğrulama hatası!\n\n" +
                    "Olası nedenler:\n" +
                    "1. Google App Password kullanmıyorsunuz\n" +
                    "2. 2 Adımlı Doğrulama kapalı\n" +
                    "3. App Password yanlış\n\n" +
                    "Çözüm: https://myaccount.google.com/apppasswords", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"E-posta gönderilemedi: {ex.Message}", ex);
            }
        }
    }
}
