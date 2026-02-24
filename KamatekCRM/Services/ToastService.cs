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
            OnShow?.Invoke(new ToastMessage(title, message, type));
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
