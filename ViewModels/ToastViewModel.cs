using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Models;
using KamatekCrm.Services;
using System.Collections.ObjectModel;
using System.Timers;
using System.Windows.Threading;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ViewModels
{
    public partial class ToastViewModel : ObservableObject
    {
        private readonly IToastService _toastService;

        public ObservableCollection<ToastMessageViewModel> Toasts { get; } = new ObservableCollection<ToastMessageViewModel>();

        public ToastViewModel(IToastService toastService)
        {
            _toastService = toastService;
            _toastService.OnShow += ShowToast;
        }

        private void ShowToast(ToastMessage message)
        {
            // UI thread üzerinde çalışmasını garanti et
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var vm = new ToastMessageViewModel(message);
                Toasts.Add(vm);

                // Belirlenen süre sonra otomatik kapat
                var timer = new System.Timers.Timer(message.Duration.TotalMilliseconds);
                timer.Elapsed += (s, e) =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        RemoveToast(vm);
                        timer.Dispose();
                    });
                };
                timer.AutoReset = false;
                timer.Start();
            });
        }

        [RelayCommand]
        public void RemoveToast(ToastMessageViewModel toast)
        {
            if (Toasts.Contains(toast))
            {
                Toasts.Remove(toast);
            }
        }
    }

    public class ToastMessageViewModel
    {
        public ToastMessage Message { get; }

        public string BackgroundColor => Message.Type switch
        {
            ToastType.Success => "#D1E7DD",
            ToastType.Error => "#F8D7DA",
            ToastType.Warning => "#FFF3CD",
            ToastType.Info => "#CFF4FC",
            _ => "#FFFFFF"
        };

        public string BorderColor => Message.Type switch
        {
            ToastType.Success => "#BADBCC",
            ToastType.Error => "#F5C2C7",
            ToastType.Warning => "#FFECB5",
            ToastType.Info => "#B6EFFB",
            _ => "#DEE2E6"
        };

        public string TextColor => Message.Type switch
        {
            ToastType.Success => "#0F5132",
            ToastType.Error => "#842029",
            ToastType.Warning => "#664D03",
            ToastType.Info => "#055160",
            _ => "#212529"
        };

        public string Icon => Message.Type switch
        {
            ToastType.Success => "✓",
            ToastType.Error => "✕",
            ToastType.Warning => "⚠",
            ToastType.Info => "ℹ",
            _ => ""
        };

        public ToastMessageViewModel(ToastMessage message)
        {
            Message = message;
        }
    }
}
