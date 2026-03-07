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
            => throw new NotImplementedException();
    }

    /// <summary>
    /// QuoteStatus → Badge arka plan rengi dönüştürücü
    /// </summary>
    public class QuoteStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is QuoteStatus status)
            {
                var hex = status switch
                {
                    QuoteStatus.Draft => "#9E9E9E",
                    QuoteStatus.Sent => "#2196F3",
                    QuoteStatus.Approved => "#4CAF50",
                    QuoteStatus.Rejected => "#F44336",
                    QuoteStatus.Expired => "#FF9800",
                    QuoteStatus.Revised => "#9C27B0",
                    _ => "#757575"
                };

                var converter = new BrushConverter();
                return converter.ConvertFromString(hex) ?? Brushes.Gray;
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
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
            => throw new NotImplementedException();
    }

    /// <summary>
    /// bool → Visibility dönüştürücü (true = Visible)
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
