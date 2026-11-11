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

        public KmStatusBadge()
        {
            ApplyStatusBrushes();
        }

        private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KmStatusBadge badge)
                badge.UpdateColors();
        }

        private void ApplyStatusBrushes()
        {
            switch (Status)
            {
                case BadgeStatus.Success:
                    SetResourceReference(BadgeBackgroundProperty, "StatusSuccessBg");
                    SetResourceReference(BadgeForegroundProperty, "StatusSuccessFg");
                    SetResourceReference(DotColorProperty, "SuccessBrush");
                    break;
                case BadgeStatus.Warning:
                    SetResourceReference(BadgeBackgroundProperty, "StatusWarningBg");
                    SetResourceReference(BadgeForegroundProperty, "StatusWarningFg");
                    SetResourceReference(DotColorProperty, "WarningBrush");
                    break;
                case BadgeStatus.Error:
                    SetResourceReference(BadgeBackgroundProperty, "StatusErrorBg");
                    SetResourceReference(BadgeForegroundProperty, "StatusErrorFg");
                    SetResourceReference(DotColorProperty, "DangerBrush");
                    break;
                case BadgeStatus.Info:
                    SetResourceReference(BadgeBackgroundProperty, "StatusInfoBg");
                    SetResourceReference(BadgeForegroundProperty, "StatusInfoFg");
                    SetResourceReference(DotColorProperty, "InfoBrush");
                    break;
                default:
                    SetResourceReference(BadgeBackgroundProperty, "StatusNeutralBg");
                    SetResourceReference(BadgeForegroundProperty, "SecondaryTextBrush");
                    SetResourceReference(DotColorProperty, "MutedTextBrush");
                    break;
            }
        }

        private void UpdateColors()
        {
            ApplyStatusBrushes();

            if (string.IsNullOrEmpty(Text))
            {
                Text = Status switch
                {
                    BadgeStatus.Success => "Aktif",
                    BadgeStatus.Warning => "Beklemede",
                    BadgeStatus.Error => "Hata",
                    BadgeStatus.Info => "Bilgi",
                    _ => "—"
                };
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
