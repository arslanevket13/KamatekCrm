using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Models;
using KamatekCrm.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Threading;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Toast bildirimlerini yöneten ViewModel.
    /// Thread-safe: DispatcherTimer kullanır, System.Timers.Timer KULLANMAZ.
    /// </summary>
    public partial class ToastViewModel : ObservableObject
    {
        private readonly IToastService _toastService;
        private const int MaxToasts = 5;

        public ObservableCollection<ToastMessageViewModel> Toasts { get; } = new();

        /// <summary>
        /// XAML Visibility binding için — Toasts.Count > 0 kontrolü.
        /// </summary>
        public bool HasToasts => Toasts.Count > 0;

        public ToastViewModel(IToastService toastService)
        {
            _toastService = toastService;
            _toastService.OnShow += ShowToast;

            // Collection değiştiğinde HasToasts'u güncelle
            Toasts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasToasts));
        }

        private void ShowToast(ToastMessage message)
        {
            // DispatcherTimer zaten UI thread'inde ateşler — Invoke() gerekmez
            // Ancak ShowToast farklı thread'den çağrılabilir, bu yüzden BeginInvoke kullanıyoruz
            if (System.Windows.Application.Current?.Dispatcher is not { } dispatcher)
                return;

            dispatcher.BeginInvoke(() =>
            {
                // Maksimum toast sınırı — en eskisini kaldır
                while (Toasts.Count >= MaxToasts)
                {
                    Toasts.RemoveAt(0);
                }

                var vm = new ToastMessageViewModel(message);
                Toasts.Add(vm);

                // DispatcherTimer: UI thread'inde ateşler, deadlock riski YOK
                var timer = new DispatcherTimer
                {
                    Interval = message.Duration
                };
                timer.Tick += (_, _) =>
                {
                    RemoveToast(vm);
                    timer.Stop();
                };
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

    /// <summary>
    /// Tek bir toast mesajının görsel modellemesi.
    /// Dark theme uyumlu renkler kullanır.
    /// </summary>
    public class ToastMessageViewModel
    {
        public ToastMessage Message { get; }

        // ── Dark Theme Uyumlu Renkler ──

        public string BackgroundColor => Message.Type switch
        {
            ToastType.Success => "#1B3A2A",
            ToastType.Error   => "#3A1B1B",
            ToastType.Warning => "#3A351B",
            ToastType.Info    => "#1B2E3A",
            _ => "#2D2D2D"
        };

        public string BorderColor => Message.Type switch
        {
            ToastType.Success => "#2E7D32",
            ToastType.Error   => "#C62828",
            ToastType.Warning => "#F9A825",
            ToastType.Info    => "#1565C0",
            _ => "#555555"
        };

        public string TextColor => Message.Type switch
        {
            ToastType.Success => "#81C784",
            ToastType.Error   => "#EF9A9A",
            ToastType.Warning => "#FFF176",
            ToastType.Info    => "#90CAF9",
            _ => "#E0E0E0"
        };

        public string Icon => Message.Type switch
        {
            ToastType.Success => "✓",
            ToastType.Error   => "✕",
            ToastType.Warning => "⚠",
            ToastType.Info    => "ℹ",
            _ => ""
        };

        public ToastMessageViewModel(ToastMessage message)
        {
            Message = message;
        }
    }
}
