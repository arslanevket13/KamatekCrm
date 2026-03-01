using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace KamatekCrm.Components
{
    /// <summary>
    /// Premium KPI Card with animated counter, trend arrow, and accent gradient strip.
    /// Usage: <km:KmKpiCard Title="Günlük Satış" Value="12450" Prefix="₺" Trend="12.5" AccentBrush="{StaticResource KpiGradientBlue}"/>
    /// </summary>
    public class KmKpiCard : Control
    {
        static KmKpiCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(KmKpiCard),
                new FrameworkPropertyMetadata(typeof(KmKpiCard)));
        }

        #region Dependency Properties

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(KmKpiCard),
                new PropertyMetadata("KPI"));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(KmKpiCard),
                new PropertyMetadata(0.0, OnValueChanged));

        public static readonly DependencyProperty DisplayValueProperty =
            DependencyProperty.Register(nameof(DisplayValue), typeof(string), typeof(KmKpiCard),
                new PropertyMetadata("0"));

        public static readonly DependencyProperty PrefixProperty =
            DependencyProperty.Register(nameof(Prefix), typeof(string), typeof(KmKpiCard),
                new PropertyMetadata(""));

        public static readonly DependencyProperty SuffixProperty =
            DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(KmKpiCard),
                new PropertyMetadata(""));

        public static readonly DependencyProperty TrendProperty =
            DependencyProperty.Register(nameof(Trend), typeof(double?), typeof(KmKpiCard),
                new PropertyMetadata(null, OnTrendChanged));

        public static readonly DependencyProperty TrendTextProperty =
            DependencyProperty.Register(nameof(TrendText), typeof(string), typeof(KmKpiCard),
                new PropertyMetadata(""));

        public static readonly DependencyProperty TrendBrushProperty =
            DependencyProperty.Register(nameof(TrendBrush), typeof(Brush), typeof(KmKpiCard));

        public static readonly DependencyProperty TrendArrowProperty =
            DependencyProperty.Register(nameof(TrendArrow), typeof(string), typeof(KmKpiCard),
                new PropertyMetadata(""));

        public static readonly DependencyProperty TrendVisibilityProperty =
            DependencyProperty.Register(nameof(TrendVisibility), typeof(Visibility), typeof(KmKpiCard),
                new PropertyMetadata(Visibility.Collapsed));

        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(KmKpiCard),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 120, 212))));

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(string), typeof(KmKpiCard),
                new PropertyMetadata(""));

        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(KmKpiCard),
                new PropertyMetadata(""));

        public static readonly DependencyProperty FormatProperty =
            DependencyProperty.Register(nameof(Format), typeof(string), typeof(KmKpiCard),
                new PropertyMetadata("N0"));

        public static readonly DependencyProperty AnimateProperty =
            DependencyProperty.Register(nameof(Animate), typeof(bool), typeof(KmKpiCard),
                new PropertyMetadata(true));

        #endregion

        #region Properties

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string DisplayValue
        {
            get => (string)GetValue(DisplayValueProperty);
            set => SetValue(DisplayValueProperty, value);
        }

        public string Prefix
        {
            get => (string)GetValue(PrefixProperty);
            set => SetValue(PrefixProperty, value);
        }

        public string Suffix
        {
            get => (string)GetValue(SuffixProperty);
            set => SetValue(SuffixProperty, value);
        }

        public double? Trend
        {
            get => (double?)GetValue(TrendProperty);
            set => SetValue(TrendProperty, value);
        }

        public string TrendText
        {
            get => (string)GetValue(TrendTextProperty);
            set => SetValue(TrendTextProperty, value);
        }

        public Brush TrendBrush
        {
            get => (Brush)GetValue(TrendBrushProperty);
            set => SetValue(TrendBrushProperty, value);
        }

        public string TrendArrow
        {
            get => (string)GetValue(TrendArrowProperty);
            set => SetValue(TrendArrowProperty, value);
        }

        public Visibility TrendVisibility
        {
            get => (Visibility)GetValue(TrendVisibilityProperty);
            set => SetValue(TrendVisibilityProperty, value);
        }

        public Brush AccentBrush
        {
            get => (Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public string Format
        {
            get => (string)GetValue(FormatProperty);
            set => SetValue(FormatProperty, value);
        }

        public bool Animate
        {
            get => (bool)GetValue(AnimateProperty);
            set => SetValue(AnimateProperty, value);
        }

        #endregion

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KmKpiCard card)
            {
                var newValue = (double)e.NewValue;
                var oldValue = e.OldValue is double old ? old : 0.0;

                if (card.Animate && card.IsLoaded)
                {
                    card.AnimateValue(oldValue, newValue);
                }
                else
                {
                    card.DisplayValue = newValue.ToString(card.Format, CultureInfo.CurrentCulture);
                }
            }
        }

        private static void OnTrendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KmKpiCard card)
            {
                card.UpdateTrend();
            }
        }

        private void UpdateTrend()
        {
            if (Trend.HasValue)
            {
                TrendVisibility = Visibility.Visible;
                var value = Trend.Value;
                TrendText = $"{Math.Abs(value):F1}%";

                if (value > 0)
                {
                    TrendArrow = "▲";
                    TrendBrush = new SolidColorBrush(Color.FromRgb(92, 214, 92));
                }
                else if (value < 0)
                {
                    TrendArrow = "▼";
                    TrendBrush = new SolidColorBrush(Color.FromRgb(255, 107, 107));
                }
                else
                {
                    TrendArrow = "─";
                    TrendBrush = new SolidColorBrush(Color.FromRgb(160, 160, 176));
                }
            }
            else
            {
                TrendVisibility = Visibility.Collapsed;
            }
        }

        private void AnimateValue(double from, double to)
        {
            var duration = TimeSpan.FromMilliseconds(600);
            var frames = 30;
            var interval = duration.TotalMilliseconds / frames;
            var step = 0;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(interval) };
            timer.Tick += (s, e) =>
            {
                step++;
                var progress = (double)step / frames;
                // Ease-out cubic
                progress = 1 - Math.Pow(1 - progress, 3);
                var current = from + (to - from) * progress;
                DisplayValue = current.ToString(Format, CultureInfo.CurrentCulture);

                if (step >= frames)
                {
                    timer.Stop();
                    DisplayValue = to.ToString(Format, CultureInfo.CurrentCulture);
                }
            };
            timer.Start();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            DisplayValue = Value.ToString(Format, CultureInfo.CurrentCulture);
            UpdateTrend();
        }
    }
}
