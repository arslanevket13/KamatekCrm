using System;
using System.Globalization;
using System.Windows.Data;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Converters
{
    /// <summary>
    /// UI TextBox veya NumberBox girişlerinde boş dize (""), boşluk veya geçersiz sayı girildiğinde
    /// int.Parse kaynaklı System.FormatException hatasını önleyen güvenli dönüştürücü.
    /// </summary>
    public class SafeIntConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int intVal)
            {
                return intVal.ToString(culture);
            }
            if (value is double dVal)
            {
                return ((int)Math.Round(dVal)).ToString(culture);
            }
            if (value is null)
            {
                return parameter?.ToString() ?? "1";
            }
            return value.ToString() ?? "1";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            int defaultVal = 1;
            if (parameter is string paramStr && int.TryParse(paramStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pVal))
            {
                defaultVal = pVal;
            }

            if (value == null)
            {
                return defaultVal;
            }

            if (value is int iVal)
            {
                return iVal;
            }

            if (value is double doubleVal)
            {
                return Math.Max(defaultVal, (int)Math.Round(doubleVal));
            }

            var str = value.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(str))
            {
                return defaultVal;
            }

            if (int.TryParse(str, NumberStyles.Integer, culture, out int result) ||
                int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return Math.Max(defaultVal, result);
            }

            return defaultVal;
        }
    }

    /// <summary>
    /// Boş bırakılabilir tamsayı (int?) alanları için güvenli dönüştürücü.
    /// Boş dizede null döner, geçersiz girişte FormatException fırlatmaz.
    /// </summary>
    public class SafeNullableIntConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int intVal)
            {
                return intVal.ToString(culture);
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return null;
            if (value is int iVal) return iVal;

            var str = value.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(str)) return null;

            if (int.TryParse(str, NumberStyles.Integer, culture, out int result) ||
                int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            return null;
        }
    }

    /// <summary>
    /// Para veya miktar (decimal) alanları için güvenli dönüştürücü.
    /// Boş dizede 0m döner, FormatException fırlatmaz.
    /// Hem Türkçe virgül (125,50) hem uluslararası nokta (125.50) formatlarını güvenle ayrıştırır.
    /// </summary>
    public class SafeDecimalConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is decimal decVal)
            {
                return decVal.ToString("G", culture);
            }
            if (value is double dVal)
            {
                return dVal.ToString("G", culture);
            }
            return "0";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            decimal defaultVal = 0m;
            if (parameter is string paramStr && decimal.TryParse(paramStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pVal))
            {
                defaultVal = pVal;
            }

            if (value == null) return defaultVal;
            if (value is decimal dVal) return dVal;

            var str = value.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(str)) return defaultVal;

            // Hem virgül (Türkçe) hem nokta (uluslararası) ondalık ayracı desteği
            if (str.Contains(',') && str.Contains('.'))
            {
                if (str.LastIndexOf(',') > str.LastIndexOf('.'))
                {
                    // "1.250,50" -> "1250.50"
                    str = str.Replace(".", "").Replace(',', '.');
                }
                else
                {
                    // "1,250.50" -> "1250.50"
                    str = str.Replace(",", "");
                }
            }
            else if (str.Contains(','))
            {
                // "125,50" -> "125.50"
                str = str.Replace(',', '.');
            }

            if (decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
            {
                return result;
            }

            if (decimal.TryParse(str, NumberStyles.Any, culture, out result))
            {
                return result;
            }

            return defaultVal;
        }
    }

    /// <summary>
    /// StructureType enum değerini Türkçe anlaşılır kullanıcı metnine dönüştürür.
    /// </summary>
    public class StructureTypeDisplayConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is StructureType st)
            {
                return st switch
                {
                    StructureType.SingleUnit => "🏡 Müstakil Ev / Tekil Yapı",
                    StructureType.Apartment => "🏢 Apartman (Tek Blok)",
                    StructureType.Site => "🏘️ Site (Çoklu Blok)",
                    StructureType.Commercial => "🏬 Ticari / İş Yeri",
                    _ => st.ToString()
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                if (s.Contains("Müstakil", StringComparison.OrdinalIgnoreCase)) return StructureType.SingleUnit;
                if (s.Contains("Apartman", StringComparison.OrdinalIgnoreCase)) return StructureType.Apartment;
                if (s.Contains("Site", StringComparison.OrdinalIgnoreCase)) return StructureType.Site;
                if (s.Contains("Ticari", StringComparison.OrdinalIgnoreCase)) return StructureType.Commercial;
            }
            return Binding.DoNothing;
        }
    }
}
