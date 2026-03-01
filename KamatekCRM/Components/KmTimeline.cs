using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KamatekCrm.Components
{
    /// <summary>
    /// Vertical timeline component for displaying event history.
    /// Usage: <km:KmTimeline Items="{Binding TimelineEvents}"/>
    /// </summary>
    public class KmTimeline : Control
    {
        static KmTimeline()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(KmTimeline),
                new FrameworkPropertyMetadata(typeof(KmTimeline)));
        }

        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items), typeof(ObservableCollection<TimelineItem>), typeof(KmTimeline),
                new PropertyMetadata(new ObservableCollection<TimelineItem>()));

        public static readonly DependencyProperty MaxItemsProperty =
            DependencyProperty.Register(nameof(MaxItems), typeof(int), typeof(KmTimeline),
                new PropertyMetadata(10));

        public ObservableCollection<TimelineItem> Items
        {
            get => (ObservableCollection<TimelineItem>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public int MaxItems
        {
            get => (int)GetValue(MaxItemsProperty);
            set => SetValue(MaxItemsProperty, value);
        }
    }

    /// <summary>
    /// Single item in a KmTimeline.
    /// </summary>
    public class TimelineItem
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Icon { get; set; } = "●";
        public BadgeStatus Status { get; set; } = BadgeStatus.Neutral;

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - Timestamp;
                if (diff.TotalMinutes < 1) return "Az önce";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} dk önce";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} saat önce";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} gün önce";
                return Timestamp.ToString("dd MMM yyyy");
            }
        }

        public Brush DotBrush
        {
            get
            {
                return Status switch
                {
                    BadgeStatus.Success => new SolidColorBrush(Color.FromRgb(16, 124, 16)),
                    BadgeStatus.Warning => new SolidColorBrush(Color.FromRgb(255, 140, 0)),
                    BadgeStatus.Error => new SolidColorBrush(Color.FromRgb(232, 17, 35)),
                    BadgeStatus.Info => new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                    _ => new SolidColorBrush(Color.FromRgb(128, 128, 148))
                };
            }
        }
    }
}
