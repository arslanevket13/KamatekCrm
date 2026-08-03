using System;
using System.Text.RegularExpressions;

namespace KamatekCrm.ApplicationCore.Common
{
    public static class PhoneNormalizationHelper
    {
        private static readonly Regex DigitsOnlyRegex = new Regex(@"[^\d]", RegexOptions.Compiled);

        /// <summary>
        /// Telefon numarasını Türkiye standartlarına (E.164) göre normalize eder (+905XXXXXXXXX)
        /// </summary>
        public static string NormalizePhoneNumber(string? rawPhone)
        {
            if (string.IsNullOrWhiteSpace(rawPhone))
                return string.Empty;

            var cleaned = rawPhone.Trim();

            // Uluslararası numara kontrolü (örn. +1..., +44...)
            if (cleaned.StartsWith("+") && !cleaned.StartsWith("+90"))
            {
                return "+" + DigitsOnlyRegex.Replace(cleaned, "");
            }

            var digits = DigitsOnlyRegex.Replace(cleaned, "");

            if (digits.Length == 10 && digits.StartsWith("5"))
            {
                return "+90" + digits;
            }
            if (digits.Length == 11 && digits.StartsWith("05"))
            {
                return "+90" + digits.Substring(1);
            }
            if (digits.Length == 12 && digits.StartsWith("905"))
            {
                return "+" + digits;
            }

            return digits.Length > 0 ? "+" + digits : string.Empty;
        }

        /// <summary>
        /// Telefon numarasını formatlı gösterir: 0 (5XX) XXX XX XX
        /// </summary>
        public static string FormatDisplayPhone(string? normalizedPhone)
        {
            if (string.IsNullOrWhiteSpace(normalizedPhone)) return string.Empty;

            var digits = DigitsOnlyRegex.Replace(normalizedPhone, "");
            if (digits.StartsWith("90") && digits.Length == 12)
            {
                digits = digits.Substring(2);
            }

            if (digits.Length == 10)
            {
                return string.Format("0 ({0}) {1} {2} {3}",
                    digits.Substring(0, 3),
                    digits.Substring(3, 3),
                    digits.Substring(6, 2),
                    digits.Substring(8, 2));
            }

            return normalizedPhone;
        }
    }
}
