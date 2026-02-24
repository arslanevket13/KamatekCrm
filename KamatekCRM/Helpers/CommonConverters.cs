using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Helpers
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            if (parameter is string paramStr && paramStr.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            {
                // Inverse: Null -> Visible, NotNull -> Collapsed
                return isNull ? Visibility.Visible : Visibility.Collapsed;
            }
            // Normal: Null -> Collapsed, NotNull -> Visible
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            if (parameter is string paramStr && paramStr.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            {
                return isNull; // Inverse: Null -> True
            }
            return !isNull; // Normal: Null -> False
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SegmentToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CustomerSegment segment)
            {
                return segment switch
                {
                    CustomerSegment.VIP => new SolidColorBrush(Color.FromRgb(255, 193, 7)),     // Altın
                    CustomerSegment.Potential => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Yeşil
                    CustomerSegment.AtRisk => new SolidColorBrush(Color.FromRgb(244, 67, 54)),   // Kırmızı
                    CustomerSegment.Passive => new SolidColorBrush(Color.FromRgb(158, 158, 158)), // Gri
                    CustomerSegment.New => new SolidColorBrush(Color.FromRgb(33, 150, 243)),     // Mavi
                    _ => new SolidColorBrush(Color.FromRgb(224, 224, 224))                     // Varsayılan Gri
                };
            }
            return new SolidColorBrush(Color.FromRgb(224, 224, 224));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
