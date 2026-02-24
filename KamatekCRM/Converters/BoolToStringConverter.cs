using System;
using System.Globalization;
using System.Windows.Data;

namespace KamatekCrm.Converters
{
    /// <summary>
    /// Boolean değeri string'e dönüştürür.
    /// ConverterParameter formatı: "TrueValue|FalseValue"
    /// Örnek: ConverterParameter='Evet|Hayır'
    /// </summary>
    public class BoolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                var parts = paramString.Split('|');
                if (parts.Length >= 2)
                {
                    return boolValue ? parts[0] : parts[1];
                }
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
