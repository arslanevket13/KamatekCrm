using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace KamatekCrm.Views
{
    /// <summary>
    /// StockCountView code-behind
    /// </summary>
    public partial class StockCountView : UserControl
    {
        public StockCountView()
        {
            InitializeComponent();
        }
    }


    /// <summary>
    /// Sayının pozitif olup olmadığını kontrol eden converter
    /// </summary>
    public class IsPositiveConverter : IValueConverter
    {
        public static readonly IsPositiveConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
                return intValue > 0;
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
            if (value is int intValue)
                return intValue < 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
