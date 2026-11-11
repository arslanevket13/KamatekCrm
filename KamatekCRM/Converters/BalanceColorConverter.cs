using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace KamatekCrm.Converters
{
    public class BalanceColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                // Pozitif bakiye (Borç) => Kırmızı
                // Sıfır veya negatif (Alacak/Yok) => Yeşil
                var resourceKey = decimalValue > 0 ? "StatusErrorBrush" : "StatusSuccessBrush";
                return Application.Current?.TryFindResource(resourceKey) as Brush ?? DependencyProperty.UnsetValue;
            }
            return Application.Current?.TryFindResource("PrimaryTextBrush") as Brush ?? DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
