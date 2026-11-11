using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace KamatekCrm.Converters
{
    /// <summary>
    /// Bağlantı sağlığına göre durum badge arka plan rengini belirler.
    /// true (bağlı) → ThemeSuccessDark, false (kopuk) → ThemeErrorDark.
    /// </summary>
    public class ConnectionStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var resourceKey = value is true ? "ThemeSuccessDark" : "ThemeErrorDark";
            return Application.Current?.TryFindResource(resourceKey) as Brush ?? DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// RadioButton'ların IsMainServer boolean özelliğine bağlanması için iki yönlü dönüştürücü.
    /// ConverterParameter="Server" veya "Client"
    /// </summary>
    public class ServerModeToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isMainServer = value is true;
            string param = parameter?.ToString() ?? "Server";

            return param.Equals("Server", StringComparison.OrdinalIgnoreCase)
                ? isMainServer
                : !isMainServer;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isChecked = value is true;
            if (!isChecked) return Binding.DoNothing;

            string param = parameter?.ToString() ?? "Server";
            return param.Equals("Server", StringComparison.OrdinalIgnoreCase);
        }
    }
}

