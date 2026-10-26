using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace KamatekCrm.Helpers
{
    /// <summary>
    /// List<string> içinde belirli bir değerin olup olmadığını kontrol eden converter
    /// Smart Home kategorisinde ControlMethods için kullanılır
    /// </summary>
    public class ListContainsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is List<string> list && parameter is string item)
            {
                return list.Contains(item);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
