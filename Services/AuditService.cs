using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Audit log servisi - Asenkron kayıt tutma
    /// </summary>
    public static class AuditService
    {
        // Helper to resolve IAuthService from global ServiceProvider
        private static IAuthService? GetAuthService()
        {
            try
            {
                return App.ServiceProvider?.GetService<IAuthService>();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Aktivite logu kaydet (asenkron - UI'ı bloklamaz)
        /// </summary>
        public static async Task LogAsync(
            AuditActionType actionType,
            string? entityName = null,
            string? recordId = null,
            string? description = null,
            string? additionalData = null)
        {
            try
            {
                var user = GetAuthService()?.CurrentUser;
                var userId = user?.Id;
                var username = user?.Username ?? "System/Anonymous";

                await Task.Run(() =>
                {
                    using var context = new AppDbContext();

                    var log = new ActivityLog
                    {
                        UserId = userId,
                        Username = username,
                        ActionType = actionType.ToString(),
                        EntityName = entityName,
                        RecordId = recordId,
                        Description = description,
                        AdditionalData = additionalData,
                        Timestamp = DateTime.Now
                    };

                    context.ActivityLogs.Add(log);
                    context.SaveChanges();
                });
            }
            catch
            {
                // Logging hatası uygulamayı etkilememeli
            }
        }

        /// <summary>
        /// Aktivite logu kaydet (senkron - hızlı işlemler için)
        /// </summary>
        public static void Log(
            AuditActionType actionType,
            string? entityName = null,
            string? recordId = null,
            string? description = null)
        {
            try
            {
                var user = GetAuthService()?.CurrentUser;
                
                using var context = new AppDbContext();

                var log = new ActivityLog
                {
                    UserId = user?.Id,
                    Username = user?.Username ?? "System/Anonymous",
                    Action = actionType.ToString(),
                    EntityName = entityName,
                    ReferenceId = recordId,
                    Description = description,
                    Timestamp = DateTime.Now,
                    DurationMs = 0, 
                    IpAddress = "127.0.0.1", 
                    UserAgent = "WPF Client" 
                };

                context.ActivityLogs.Add(log);
                context.SaveChanges();
            }
            catch
            {
                // Logging hatası uygulamayı etkilememeli
            }
        }

        /// <summary>
        /// Login logu kaydet
        /// </summary>
        public static void LogLogin(string username)
        {
            Log(AuditActionType.Login, "User", null, $"{username} sisteme giriş yaptı");
        }

        /// <summary>
        /// Logout logu kaydet
        /// </summary>
        public static void LogLogout()
        {
            var username = GetAuthService()?.CurrentUser?.Username ?? "Unknown";
            Log(AuditActionType.Logout, "User", null, $"{username} sistemden çıkış yaptı");
        }

        /// <summary>
        /// Kullanıcı oluşturma logu
        /// </summary>
        public static async Task LogUserCreatedAsync(User newUser)
        {
            await LogAsync(
                AuditActionType.Create,
                "User",
                newUser.Id.ToString(),
                $"Yeni kullanıcı oluşturuldu: {newUser.AdSoyad} ({newUser.Username}), Rol: {newUser.Role}");
        }

        /// <summary>
        /// Kullanıcı güncelleme logu
        /// </summary>
        public static async Task LogUserUpdatedAsync(User user, string changes)
        {
            await LogAsync(
                AuditActionType.Update,
                "User",
                user.Id.ToString(),
                $"{user.AdSoyad} ({user.Username}) güncellendi: {changes}");
        }

        /// <summary>
        /// Kullanıcı silme logu
        /// </summary>
        public static async Task LogUserDeletedAsync(string deletedUsername, string deletedUserFullName)
        {
            await LogAsync(
                AuditActionType.Delete,
                "User",
                null,
                $"Kullanıcı silindi: {deletedUserFullName} ({deletedUsername})");
        }

        /// <summary>
        /// Şifre değiştirme logu
        /// </summary>
        public static async Task LogPasswordChangedAsync(User targetUser)
        {
            await LogAsync(
                AuditActionType.PasswordChange,
                "User",
                targetUser.Id.ToString(),
                $"{targetUser.AdSoyad} ({targetUser.Username}) için şifre değiştirildi");
        }

        /// <summary>
        /// Şifre sıfırlama logu
        /// </summary>
        public static async Task LogPasswordResetAsync(User targetUser)
        {
            await LogAsync(
                AuditActionType.PasswordReset,
                "User",
                targetUser.Id.ToString(),
                $"{targetUser.AdSoyad} ({targetUser.Username}) için şifre '1234' olarak sıfırlandı");
        }

        /// <summary>
        /// Müşteri işlem logu
        /// </summary>
        public static async Task LogCustomerActionAsync(AuditActionType action, int customerId, string customerName, string? details = null)
        {
            await LogAsync(
                action,
                "Customer",
                customerId.ToString(),
                $"Müşteri: {customerName}. {details}");
        }

        /// <summary>
        /// Ürün işlem logu
        /// </summary>
        public static async Task LogProductActionAsync(AuditActionType action, int productId, string productName, string? details = null)
        {
            await LogAsync(
                action,
                "Product",
                productId.ToString(),
                $"Ürün: {productName}. {details}");
        }

        /// <summary>
        /// İş emri işlem logu
        /// </summary>
        public static async Task LogServiceJobActionAsync(AuditActionType action, int jobId, string? details = null)
        {
            await LogAsync(
                action,
                "ServiceJob",
                jobId.ToString(),
                $"İş Emri #{jobId}. {details}");
        }
    }
}
