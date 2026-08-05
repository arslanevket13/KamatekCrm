using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;

namespace KamatekCrm.Views
{
    /// <summary>Görev tamamlanma durumunu ✓ / ⬜ işaretine çevirir.</summary>
    public sealed class CompleteMarkConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is true ? "✅" : "⬜";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Görev tamamlandıysa üstü çizili metin stili uygular.</summary>
    public sealed class StrikeConverter : IValueConverter
    {
        private static readonly TextDecorationCollection Strikethrough = new()
        {
            TextDecorations.Strikethrough
        };

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is true ? Strikethrough : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
