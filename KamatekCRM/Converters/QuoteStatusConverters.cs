using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Converters
{
    /// <summary>
    /// QuoteStatus → Türkçe metin dönüştürücü
    /// </summary>
    public class QuoteStatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is QuoteStatus status)
            {
                return status switch
                {
                    QuoteStatus.Draft => "📝 Taslak",
                    QuoteStatus.Sent => "📨 Gönderildi",
                    QuoteStatus.Approved => "✅ Onaylandı",
                    QuoteStatus.Rejected => "❌ Reddedildi",
                    QuoteStatus.Expired => "⏰ Süresi Doldu",
                    QuoteStatus.Revised => "🔄 Revize",
                    _ => "Bilinmiyor"
                };
            }
            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>
    /// QuoteStatus → Badge arka plan rengi dönüştürücü
    /// </summary>
    public class QuoteStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var resourceKey = value is QuoteStatus status
                ? status switch
                {
                    QuoteStatus.Draft => "StatusNeutralBg",
                    QuoteStatus.Sent => "StatusInfoBgLight",
                    QuoteStatus.Approved => "StatusSuccessBgLight",
                    QuoteStatus.Rejected => "StatusErrorBgLight",
                    QuoteStatus.Expired => "StatusWarningBgLight",
                    QuoteStatus.Revised => "AccentOverlayBrush",
                    _ => "StatusNeutralBg"
                }
                : "StatusNeutralBg";

            return Application.Current?.TryFindResource(resourceKey) as Brush ?? DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>
    /// DateTime → "dd.MM.yyyy" string dönüştürücü
    /// </summary>
    public class DateDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
                return dt.ToLocalTime().ToString("dd.MM.yyyy");
            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>
    /// bool → Visibility dönüştürücü (true = Visible)
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                bool invert = parameter is string s &&
                    (s.Equals("Inverse", StringComparison.OrdinalIgnoreCase) ||
                     s.Equals("Invert", StringComparison.OrdinalIgnoreCase));
                if (invert) b = !b;
                return b ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
