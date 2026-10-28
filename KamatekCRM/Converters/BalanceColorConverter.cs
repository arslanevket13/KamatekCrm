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
                return decimalValue > 0
                    ? (Application.Current?.TryFindResource("StatusErrorBrush") as Brush ?? Brushes.IndianRed)
                    : (Application.Current?.TryFindResource("StatusSuccessBrush") as Brush ?? Brushes.MediumSeaGreen);
            }
            return Application.Current?.TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
