using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Toast bildirim tipi
    /// </summary>
    public enum ToastType
    {
        Success,
        Error,
        Warning,
        Info
    }

    /// <summary>
    /// Toast bildirim modeli
    /// </summary>
    public class ToastMessage
    {
        public string Message { get; set; } = string.Empty;
        public ToastType Type { get; set; } = ToastType.Info;
        public string Icon => Type switch
        {
            ToastType.Success => "✅",
            ToastType.Error => "❌",
            ToastType.Warning => "⚠️",
            ToastType.Info => "ℹ️",
            _ => "📢"
        };
        public string BackgroundColor => Type switch
        {
            ToastType.Success => "#E8F5E9",
            ToastType.Error => "#FFEBEE",
            ToastType.Warning => "#FFF3E0",
            ToastType.Info => "#E3F2FD",
            _ => "#F5F5F5"
        };
        public string BorderColor => Type switch
        {
            ToastType.Success => "#4CAF50",
            ToastType.Error => "#F44336",
            ToastType.Warning => "#FF9800",
            ToastType.Info => "#2196F3",
            _ => "#9E9E9E"
        };
        public string TextColor => Type switch
        {
            ToastType.Success => "#2E7D32",
            ToastType.Error => "#C62828",
            ToastType.Warning => "#E65100",
            ToastType.Info => "#1565C0",
            _ => "#424242"
        };
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Toast bildirim yöneticisi (Singleton)
    /// </summary>
    public class ToastNotificationManager : ViewModels.ViewModelBase
    {
        private static ToastNotificationManager? _instance;
        private static readonly object _lock = new object();
        private readonly DispatcherTimer _cleanupTimer;

        public static ToastNotificationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ToastNotificationManager();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Aktif bildirimler
        /// </summary>
        public ObservableCollection<ToastMessage> Toasts { get; } = new ObservableCollection<ToastMessage>();

        /// <summary>
        /// Bildirim var mı?
        /// </summary>
        public bool HasToasts => Toasts.Count > 0;

        private ToastNotificationManager()
        {
            // Her 500ms'de süresi dolan bildirimleri temizle
            _cleanupTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _cleanupTimer.Tick += CleanupExpiredToasts;
            _cleanupTimer.Start();
        }

        /// <summary>
        /// Süresi dolan bildirimleri temizle
        /// </summary>
        private void CleanupExpiredToasts(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            var expired = new System.Collections.Generic.List<ToastMessage>();

            foreach (var toast in Toasts)
            {
                if ((now - toast.CreatedAt).TotalSeconds > 3) // 3 saniye sonra kaldır
                {
                    expired.Add(toast);
                }
            }

            foreach (var toast in expired)
            {
                Application.Current?.Dispatcher.Invoke(() => Toasts.Remove(toast));
            }

            OnPropertyChanged(nameof(HasToasts));
        }

        /// <summary>
        /// Başarı bildirimi göster
        /// </summary>
        public static void ShowSuccess(string message)
        {
            Instance.AddToast(message, ToastType.Success);
        }

        /// <summary>
        /// Hata bildirimi göster
        /// </summary>
        public static void ShowError(string message)
        {
            Instance.AddToast(message, ToastType.Error);
        }

        /// <summary>
        /// Uyarı bildirimi göster
        /// </summary>
        public static void ShowWarning(string message)
        {
            Instance.AddToast(message, ToastType.Warning);
        }

        /// <summary>
        /// Bilgi bildirimi göster
        /// </summary>
        public static void ShowInfo(string message)
        {
            Instance.AddToast(message, ToastType.Info);
        }

        /// <summary>
        /// Bildirim ekle
        /// </summary>
        private void AddToast(string message, ToastType type)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // Maksimum 3 bildirim göster
                while (Toasts.Count >= 3)
                {
                    Toasts.RemoveAt(0);
                }

                Toasts.Add(new ToastMessage
                {
                    Message = message,
                    Type = type,
                    CreatedAt = DateTime.Now
                });

                OnPropertyChanged(nameof(HasToasts));
            });
        }

        /// <summary>
        /// Bildirimi kapat
        /// </summary>
        public void DismissToast(ToastMessage toast)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Toasts.Remove(toast);
                OnPropertyChanged(nameof(HasToasts));
            });
        }
    }
}
