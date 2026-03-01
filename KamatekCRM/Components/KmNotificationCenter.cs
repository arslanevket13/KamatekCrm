using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace KamatekCrm.Components
{
    /// <summary>
    /// KmNotificationCenter — Bildirim çanı + popup panel.
    /// Okunmamış bildirim sayısı badge'i, bildirim listesi popup, 
    /// tümünü okundu olarak işaretle, tümünü temizle.
    /// </summary>
    public class KmNotificationCenter : Control
    {
        static KmNotificationCenter()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(KmNotificationCenter),
                new FrameworkPropertyMetadata(typeof(KmNotificationCenter)));
        }

        #region Dependency Properties

        public static readonly DependencyProperty NotificationsProperty = DependencyProperty.Register(
            nameof(Notifications), typeof(ObservableCollection<NotificationItem>), typeof(KmNotificationCenter),
            new PropertyMetadata(null));

        public static readonly DependencyProperty UnreadCountProperty = DependencyProperty.Register(
            nameof(UnreadCount), typeof(int), typeof(KmNotificationCenter),
            new PropertyMetadata(0));

        public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
            nameof(IsOpen), typeof(bool), typeof(KmNotificationCenter),
            new PropertyMetadata(false));

        public static readonly DependencyProperty MarkAllReadCommandProperty = DependencyProperty.Register(
            nameof(MarkAllReadCommand), typeof(ICommand), typeof(KmNotificationCenter));

        public static readonly DependencyProperty ClearAllCommandProperty = DependencyProperty.Register(
            nameof(ClearAllCommand), typeof(ICommand), typeof(KmNotificationCenter));

        public static readonly DependencyProperty NotificationClickCommandProperty = DependencyProperty.Register(
            nameof(NotificationClickCommand), typeof(ICommand), typeof(KmNotificationCenter));

        #endregion

        #region Properties

        public ObservableCollection<NotificationItem> Notifications
        {
            get => (ObservableCollection<NotificationItem>)GetValue(NotificationsProperty);
            set => SetValue(NotificationsProperty, value);
        }

        public int UnreadCount
        {
            get => (int)GetValue(UnreadCountProperty);
            set => SetValue(UnreadCountProperty, value);
        }

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public ICommand? MarkAllReadCommand
        {
            get => (ICommand?)GetValue(MarkAllReadCommandProperty);
            set => SetValue(MarkAllReadCommandProperty, value);
        }

        public ICommand? ClearAllCommand
        {
            get => (ICommand?)GetValue(ClearAllCommandProperty);
            set => SetValue(ClearAllCommandProperty, value);
        }

        public ICommand? NotificationClickCommand
        {
            get => (ICommand?)GetValue(NotificationClickCommandProperty);
            set => SetValue(NotificationClickCommandProperty, value);
        }

        #endregion

        public KmNotificationCenter()
        {
            Notifications = new ObservableCollection<NotificationItem>();
        }

        /// <summary>API'dan bildirim ekle, unread count güncelle</summary>
        public void AddNotification(string title, string message, NotificationType type = NotificationType.Info)
        {
            var notification = new NotificationItem
            {
                Title = title,
                Message = message,
                Type = type,
                Timestamp = DateTime.Now,
                IsRead = false
            };

            // UI thread'de ekle
            Dispatcher.BeginInvoke(() =>
            {
                Notifications.Insert(0, notification); // En yeni en üste
                UnreadCount = Notifications.Count(n => !n.IsRead);

                // Max 50 bildirim tut
                while (Notifications.Count > 50)
                    Notifications.RemoveAt(Notifications.Count - 1);
            });
        }

        /// <summary>Tümünü okundu yap</summary>
        public void MarkAllAsRead()
        {
            foreach (var n in Notifications)
                n.IsRead = true;
            UnreadCount = 0;
        }
    }

    /// <summary>
    /// Bildirim öğesi
    /// </summary>
    public class NotificationItem
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public NotificationType Type { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - Timestamp;
                if (diff.TotalMinutes < 1) return "Az önce";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} dk";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} saat";
                return $"{(int)diff.TotalDays} gün";
            }
        }

        public string TypeIcon => Type switch
        {
            NotificationType.Success => "✅",
            NotificationType.Warning => "⚠️",
            NotificationType.Error => "❌",
            NotificationType.JobUpdate => "🔧",
            NotificationType.StockAlert => "📦",
            NotificationType.Assignment => "👤",
            _ => "ℹ️"
        };
    }

    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error,
        JobUpdate,
        StockAlert,
        Assignment
    }
}
