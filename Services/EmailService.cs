using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace KamatekCrm.Services
{
    public class EmailService
    {
        private const string SmtpHost = "smtp.gmail.com";
        private const int SmtpPort = 587;
        private const string SenderEmail = "kamatek.teknik@gmail.com"; // Demo credentials
        private const string SenderPassword = "xxxx xxxx xxxx xxxx"; // App Password

        public async Task SendQuoteEmailAsync(string toEmail, string subject, string body, string? attachmentPath = null)
        {
            try
            {
                using (var mail = new MailMessage())
                {
                    mail.From = new MailAddress(SenderEmail, "Kamatek Teknik Servis");
                    mail.To.Add(toEmail);
                    mail.Subject = subject;
                    mail.Body = body;
                    mail.IsBodyHtml = true;

                    if (!string.IsNullOrEmpty(attachmentPath))
                    {
                        mail.Attachments.Add(new Attachment(attachmentPath));
                    }

                    using (var smtp = new SmtpClient(SmtpHost, SmtpPort))
                    {
                        smtp.Credentials = new NetworkCredential(SenderEmail, SenderPassword);
                        smtp.EnableSsl = true;
                        
                        await smtp.SendMailAsync(mail);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"E-posta gönderilemedi: {ex.Message}", ex);
            }
        }
    }
}
