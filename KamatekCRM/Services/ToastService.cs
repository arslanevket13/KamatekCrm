using KamatekCrm.Models;
using KamatekCrm.Shared.Enums;
using System;

namespace KamatekCrm.Services
{
    public interface IToastService
    {
        event Action<ToastMessage> OnShow;
        void Show(string title, string message, ToastType type = ToastType.Info);
        void ShowSuccess(string message, string title = "Başarılı");
        void ShowError(string message, string title = "Hata");
        void ShowWarning(string message, string title = "Uyarı");
        void ShowInfo(string message, string title = "Bilgi");
    }

    public class ToastService : IToastService
    {
        public event Action<ToastMessage>? OnShow;

        public void Show(string title, string message, ToastType type = ToastType.Info)
        {
            if (ContainsRawDatabaseError(title) || ContainsRawDatabaseError(message))
            {
                var correlationId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                Serilog.Log.Error(
                    "Ham veritabanı/SQL hatası kullanıcı bildiriminden gizlendi [CorrelationId: {CorrelationId}] Başlık: {RawTitle} | Mesaj: {RawMessage}",
                    correlationId, title, message);
                title = "Hata";
                message = $"İşlem sırasında veritabanı hatası oluştu. Lütfen tekrar deneyin. (Ref: {correlationId})";
                type = ToastType.Error;
            }

            OnShow?.Invoke(new ToastMessage(title, message, type));
        }

        public static bool ContainsRawDatabaseError(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return text.Contains("POSITION:", StringComparison.OrdinalIgnoreCase)
                || text.Contains("SQLSTATE", StringComparison.OrdinalIgnoreCase)
                || text.Contains("PostgresException", StringComparison.OrdinalIgnoreCase)
                || text.Contains("DbUpdateException", StringComparison.OrdinalIgnoreCase)
                || text.Contains("NpgsqlException", StringComparison.OrdinalIgnoreCase)
                || text.Contains("42703:", StringComparison.OrdinalIgnoreCase)
                || text.Contains("42701:", StringComparison.OrdinalIgnoreCase)
                || text.Contains("42P01:", StringComparison.OrdinalIgnoreCase)
                || text.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                || text.Contains("of relation \"", StringComparison.OrdinalIgnoreCase);
        }

        public void ShowSuccess(string message, string title = "Başarılı")
        {
            Show(title, message, ToastType.Success);
        }

        public void ShowError(string message, string title = "Hata")
        {
            Show(title, message, ToastType.Error);
        }

        public void ShowWarning(string message, string title = "Uyarı")
        {
            Show(title, message, ToastType.Warning);
        }

        public void ShowInfo(string message, string title = "Bilgi")
        {
            Show(title, message, ToastType.Info);
        }
    }
}
