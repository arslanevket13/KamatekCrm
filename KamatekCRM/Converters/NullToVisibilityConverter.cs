using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KamatekCrm.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null || (value is string str && string.IsNullOrWhiteSpace(str));
            bool invert = parameter is string paramStr &&
                (paramStr.Equals("Inverse", StringComparison.OrdinalIgnoreCase) ||
                 paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase));

            if (invert)
            {
                return isNull ? Visibility.Visible : Visibility.Collapsed;
            }

            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
