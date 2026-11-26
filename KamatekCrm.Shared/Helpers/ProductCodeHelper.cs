using System;
using System.Globalization;
using System.Linq;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Helpers
{
    /// <summary>
    /// Ürün Stok Kodu (SKU) ve Barkod standardizasyonu, normalizasyonu,
    /// kısa okunabilir SKU ve geçerli dahili EAN-13 barkod üretimi yardımcısı.
    /// </summary>
    public static class ProductCodeHelper
    {
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return value.Trim().ToUpperInvariant();
        }

        public static string GetCategoryPrefix(ProductCategoryType? category = null)
        {
            return category switch
            {
                ProductCategoryType.Camera => "KAM",
                ProductCategoryType.Intercom => "DIA",
                ProductCategoryType.FireAlarm => "YAN",
                ProductCategoryType.BurglarAlarm => "ALR",
                ProductCategoryType.SmartHome => "AKL",
                ProductCategoryType.AccessControl => "GEC",
                ProductCategoryType.Satellite => "UYD",
                ProductCategoryType.FiberOptic => "FIB",
                ProductCategoryType.Cable => "KBL",
                ProductCategoryType.Network => "NET",
                ProductCategoryType.Security => "GUV",
                _ => "STK"
            };
        }

        /// <summary>
        /// Kısa, okunabilir stok kodu üretir: {3-harf Önek}-{0001} (örn. KAM-0001, STK-0042).
        /// </summary>
        public static string GenerateShortSku(ProductCategoryType? category = null, int sequenceNumber = 0)
        {
            var prefix = GetCategoryPrefix(category);
            var number = sequenceNumber > 0 ? sequenceNumber : Random.Shared.Next(1, 9999);
            return $"{prefix}-{number:D4}";
        }

        public static string GenerateSku(ProductCategoryType? category = null) =>
            GenerateShortSku(category);

        /// <summary>
        /// Geçerli dahili EAN-13 barkod üretir: "200" öneki + 9 rastgele rakam + kontrol basamağı.
        /// </summary>
        public static string GenerateEan13Barcode()
        {
            Span<char> digits = stackalloc char[13];
            digits[0] = '2';
            digits[1] = '0';
            digits[2] = '0';
            for (var i = 3; i < 12; i++)
            {
                digits[i] = (char)('0' + Random.Shared.Next(0, 10));
            }
            digits[12] = (char)('0' + CalculateEan13CheckDigit(digits[..12]));
            return new string(digits);
        }

        public static string GenerateBarcode() => GenerateEan13Barcode();

        public static int CalculateEan13CheckDigit(ReadOnlySpan<char> first12Digits)
        {
            if (first12Digits.Length != 12)
                throw new ArgumentException("EAN-13 kontrol basamağı için tam 12 rakam gereklidir.", nameof(first12Digits));

            var sum = 0;
            for (var i = 0; i < 12; i++)
            {
                var digit = first12Digits[i] - '0';
                if (digit is < 0 or > 9)
                    throw new ArgumentException("EAN-13 yalnızca rakamlardan oluşmalıdır.", nameof(first12Digits));
                sum += (i % 2 == 0) ? digit : digit * 3;
            }
            return (10 - (sum % 10)) % 10;
        }

        public static bool IsValidEan13(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var trimmed = value.Trim();
            if (trimmed.Length != 13 || !trimmed.All(char.IsAsciiDigit)) return false;
            var expected = CalculateEan13CheckDigit(trimmed.AsSpan(0, 12));
            return (trimmed[12] - '0') == expected;
        }

        public static int ExtractNextSequence(string prefix, System.Collections.Generic.IEnumerable<string?> existingSkus)
        {
            var normalizedPrefix = Normalize(prefix) + "-";
            var max = 0;
            foreach (var raw in existingSkus)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var sku = Normalize(raw);
                if (!sku.StartsWith(normalizedPrefix, StringComparison.Ordinal)) continue;
                var tail = sku[normalizedPrefix.Length..];
                if (int.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > max)
                {
                    max = parsed;
                }
            }
            return max + 1;
        }
    }
}
