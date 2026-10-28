using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// true → false, false → true dönüşümü.
    /// XAML'de IsAutoDiscoveryEnabled'ın tersini kontrol etmek için kullanılır.
    /// </summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // ConverterParameter "Inverse" ise Visibility döner
                if (parameter is string param && param == "Inverse")
                {
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                }
                return !boolValue;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue) return !boolValue;
            return value;
        }
    }

    /// <summary>
    /// Bağlantı sağlığına göre durum badge arka plan rengini belirler.
    /// true (bağlı) → Yeşil arka plan, false (kopuk) → Kırmızı arka plan.
    /// </summary>
    public class ConnectionStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isHealthy = value is true;
            // Semi-transparent arka plan renkleri
            return isHealthy
                ? new SolidColorBrush(Color.FromArgb(230, 22, 163, 74))   // Green-600
                : new SolidColorBrush(Color.FromArgb(230, 220, 38, 38));  // Red-600
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// true → Collapsed, false → Visible dönüşümü.
    /// </summary>
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility vis)
            {
                return vis != Visibility.Visible;
            }
            return false;
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
