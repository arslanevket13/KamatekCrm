using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace KamatekCrm.Converters
{
    /// <summary>
    /// Sayının pozitif olup olmadığını kontrol eden converter
    /// </summary>
    public class IsPositiveConverter : IValueConverter
    {
        public static readonly IsPositiveConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue) return intValue > 0;
            if (value is decimal decimalValue) return decimalValue > 0;
            if (value is double doubleValue) return doubleValue > 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Sayının negatif olup olmadığını kontrol eden converter
    /// </summary>
    public class IsNegativeConverter : IValueConverter
    {
        public static readonly IsNegativeConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue) return intValue < 0;
            if (value is decimal decimalValue) return decimalValue < 0;
            if (value is double doubleValue) return doubleValue < 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Sayının 0 olup olmadığını kontrol eden converter
    /// </summary>
    public class IsZeroConverter : IValueConverter
    {
        public static readonly IsZeroConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue) return intValue == 0;
            if (value is decimal decimalValue) return decimalValue == 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
