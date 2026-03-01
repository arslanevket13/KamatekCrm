using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KamatekCrm.Components
{
    /// <summary>
    /// Premium color-coded status badge with dot indicator.
    /// Usage: <km:KmStatusBadge Status="Active" /> or <km:KmStatusBadge Status="Warning" Text="Beklemede" />
    /// </summary>
    public class KmStatusBadge : Control
    {
        static KmStatusBadge()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(KmStatusBadge),
                new FrameworkPropertyMetadata(typeof(KmStatusBadge)));
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(KmStatusBadge),
                new PropertyMetadata("", OnStatusChanged));

        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(BadgeStatus), typeof(KmStatusBadge),
                new PropertyMetadata(BadgeStatus.Neutral, OnStatusChanged));

        public static readonly DependencyProperty BadgeBackgroundProperty =
            DependencyProperty.Register(nameof(BadgeBackground), typeof(Brush), typeof(KmStatusBadge));

        public static readonly DependencyProperty BadgeForegroundProperty =
            DependencyProperty.Register(nameof(BadgeForeground), typeof(Brush), typeof(KmStatusBadge));

        public static readonly DependencyProperty DotColorProperty =
            DependencyProperty.Register(nameof(DotColor), typeof(Brush), typeof(KmStatusBadge));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public BadgeStatus Status
        {
            get => (BadgeStatus)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public Brush BadgeBackground
        {
            get => (Brush)GetValue(BadgeBackgroundProperty);
            set => SetValue(BadgeBackgroundProperty, value);
        }

        public Brush BadgeForeground
        {
            get => (Brush)GetValue(BadgeForegroundProperty);
            set => SetValue(BadgeForegroundProperty, value);
        }

        public Brush DotColor
        {
            get => (Brush)GetValue(DotColorProperty);
            set => SetValue(DotColorProperty, value);
        }

        private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KmStatusBadge badge)
                badge.UpdateColors();
        }

        private void UpdateColors()
        {
            switch (Status)
            {
                case BadgeStatus.Success:
                    BadgeBackground = new SolidColorBrush(Color.FromArgb(30, 16, 124, 16));
                    BadgeForeground = new SolidColorBrush(Color.FromRgb(92, 214, 92));
                    DotColor = new SolidColorBrush(Color.FromRgb(16, 124, 16));
                    if (string.IsNullOrEmpty(Text)) Text = "Aktif";
                    break;
                case BadgeStatus.Warning:
                    BadgeBackground = new SolidColorBrush(Color.FromArgb(30, 255, 140, 0));
                    BadgeForeground = new SolidColorBrush(Color.FromRgb(255, 183, 77));
                    DotColor = new SolidColorBrush(Color.FromRgb(255, 140, 0));
                    if (string.IsNullOrEmpty(Text)) Text = "Beklemede";
                    break;
                case BadgeStatus.Error:
                    BadgeBackground = new SolidColorBrush(Color.FromArgb(30, 232, 17, 35));
                    BadgeForeground = new SolidColorBrush(Color.FromRgb(255, 107, 107));
                    DotColor = new SolidColorBrush(Color.FromRgb(232, 17, 35));
                    if (string.IsNullOrEmpty(Text)) Text = "Hata";
                    break;
                case BadgeStatus.Info:
                    BadgeBackground = new SolidColorBrush(Color.FromArgb(30, 0, 120, 212));
                    BadgeForeground = new SolidColorBrush(Color.FromRgb(100, 181, 246));
                    DotColor = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                    if (string.IsNullOrEmpty(Text)) Text = "Bilgi";
                    break;
                default:
                    BadgeBackground = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255));
                    BadgeForeground = new SolidColorBrush(Color.FromRgb(160, 160, 176));
                    DotColor = new SolidColorBrush(Color.FromRgb(128, 128, 148));
                    if (string.IsNullOrEmpty(Text)) Text = "—";
                    break;
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            UpdateColors();
        }
    }

    public enum BadgeStatus
    {
        Neutral,
        Success,
        Warning,
        Error,
        Info
    }
}
