using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Converters
{
    public class SegmentToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var resourceKey = value is CustomerSegment segment
                ? segment switch
                {
                    CustomerSegment.VIP => "WarningBrush",
                    CustomerSegment.Potential => "SuccessBrush",
                    CustomerSegment.AtRisk => "DangerBrush",
                    CustomerSegment.Passive => "StatusNeutralBg",
                    CustomerSegment.New => "InfoBrush",
                    _ => "StatusNeutralBg"
                }
                : "StatusNeutralBg";

            return Application.Current?.TryFindResource(resourceKey) as Brush ?? DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
