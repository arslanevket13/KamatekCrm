using System;
using System.Globalization;
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
                return decimalValue > 0 ? Brushes.IndianRed : Brushes.MediumSeaGreen;
            }
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
