using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace KamatekCrm.Converters
{
    /// <summary>
    /// Ürün ImagePath'ini (relative veya absolute) BitmapImage'a dönüştürür.
    /// Relative path'ler uygulama kök dizinine göre çözülür.
    /// </summary>
    public class ImagePathConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string imagePath || string.IsNullOrEmpty(imagePath))
                return null;

            try
            {
                string absolutePath;

                if (Path.IsPathRooted(imagePath))
                {
                    // Zaten absolute path
                    absolutePath = imagePath;
                }
                else
                {
                    // Relative path → absolute (uygulama kök dizinine göre)
                    absolutePath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        imagePath.Replace('/', Path.DirectorySeparatorChar));
                }

                if (!File.Exists(absolutePath))
                    return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(absolutePath);
                bitmap.DecodePixelWidth = 200;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
