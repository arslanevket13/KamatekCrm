using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KamatekCrm.Helpers
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return DependencyProperty.UnsetValue;

            string? checkValue = value.ToString();
            string? targetValue = parameter.ToString();

            if (checkValue == null || targetValue == null)
                 return DependencyProperty.UnsetValue;

            return checkValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return DependencyProperty.UnsetValue;

            if ((bool)value)
            {
                string? targetValue = parameter.ToString();
                if (targetValue != null)
                {
                    return Enum.Parse(targetType, targetValue);
                }
            }

            return DependencyProperty.UnsetValue;
        }
    }
}
