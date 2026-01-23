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
            // ConvertBack için List<string> instance'ına ihtiyacımız var
            // Bu nedenle bu converter MultiBinding ile kullanılmalı veya
            // ViewModel'de ayrı boolean property'ler kullanılmalı
            throw new NotImplementedException("ListContainsConverter.ConvertBack is not implemented. Use separate boolean properties in ViewModel instead.");
        }
    }
}
