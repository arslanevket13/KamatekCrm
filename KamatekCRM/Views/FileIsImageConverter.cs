using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace KamatekCrm.Views
{
    /// <summary>
    /// Dosya yolu bir resim dosyası mı (jpg/png/bmp/webp/gif) döndürür.
    /// PDF gibi görüntülenemeyen belgelerde simge gösterimi için kullanılır.
    /// </summary>
    public sealed class FileIsImageConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var path = value as string;
            if (string.IsNullOrWhiteSpace(path)) return false;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp" or ".gif";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
