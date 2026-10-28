using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace KamatekCrm.Shared.Helpers
{
    /// <summary>
    /// Müşteri iletişim ve kimlik verileri için standart normalizasyon ve resmi doğrulama algoritmaları.
    /// </summary>
    public static class CustomerValidationHelper
    {
        private static readonly Regex NonDigitsRegex = new Regex(@"\D", RegexOptions.Compiled);

        /// <summary>
        /// Telefon numarasını kanonik Türkiye formatına (05XXXXXXXXX veya 0XXXXXXXXXX - 11 hane) normalize eder.
        /// +90 (532) 111-2233, 05321112233, 532 111 22 33 -> 05321112233
        /// </summary>
        public static string NormalizePhoneNumber(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            var digits = NonDigitsRegex.Replace(phone, string.Empty);
            if (string.IsNullOrEmpty(digits))
                return string.Empty;

            // 90 ile başlıyorsa ve 12 hane ise (905XXXXXXXXX)
            if (digits.Length == 12 && digits.StartsWith("90"))
            {
                return "0" + digits.Substring(2);
            }

            // 10 hane ise ve 0 ile başlamıyorsa (5XXXXXXXXX veya 2XXXXXXXXX vb.)
            if (digits.Length == 10 && !digits.StartsWith("0"))
            {
                return "0" + digits;
            }

            // Zaten 11 hane ve 0 ile başlıyorsa (05XXXXXXXXX)
            if (digits.Length == 11 && digits.StartsWith("0"))
            {
                return digits;
            }

            // 0090 ile başlıyorsa (00905XXXXXXXXX - 14 hane)
            if (digits.Length == 14 && digits.StartsWith("0090"))
            {
                return "0" + digits.Substring(4);
            }

            return digits;
        }

        /// <summary>
        /// Telefon numarasının geçerli bir Türkiye GSM / sabit hat formatında olup olmadığını kontrol eder.
        /// </summary>
        public static bool IsValidPhoneNumber(string? phone)
        {
            var normalized = NormalizePhoneNumber(phone);
            if (string.IsNullOrEmpty(normalized))
                return false;

            // Standart Türkiye formatı: 11 hane, 0 ile başlar, ikinci hane 2-8 arası
            return normalized.Length == 11 &&
                   normalized[0] == '0' &&
                   normalized[1] >= '2' &&
                   normalized[1] <= '8';
        }

        /// <summary>
        /// Türkiye Cumhuriyeti Kimlik Numarası (11 Hane) Resmi Mod-10 Checksum Algoritması.
        /// - 11 haneli ve tamamen rakamlardan oluşmalı.
        /// - İlk hane 0 olamaz.
        /// - 1, 3, 5, 7 ve 9. hanelerin toplamının 7 katından 2, 4, 6 ve 8. hanelerin toplamı çıkarıldığında
        ///   elde edilen sonucun 10'a bölümünden kalan (Mod 10), 10. haneyi vermelidir.
        /// - İlk 10 hanenin toplamının 10'a bölümünden kalan (Mod 10), 11. haneyi vermelidir.
        /// </summary>
        public static bool IsValidTcKimlikNo(string? tcKimlikNo)
        {
            if (string.IsNullOrWhiteSpace(tcKimlikNo))
                return false;

            var trimmed = tcKimlikNo.Trim();
            if (trimmed.Length != 11 || !trimmed.All(char.IsDigit))
                return false;

            if (trimmed[0] == '0')
                return false;

            int[] d = new int[11];
            for (int i = 0; i < 11; i++)
            {
                d[i] = trimmed[i] - '0';
            }

            int oddSum = d[0] + d[2] + d[4] + d[6] + d[8];
            int evenSum = d[1] + d[3] + d[5] + d[7];

            int tenthDigit = ((oddSum * 7 - evenSum) % 10 + 10) % 10;
            if (tenthDigit != d[9])
                return false;

            int totalFirstTen = 0;
            for (int i = 0; i < 10; i++)
            {
                totalFirstTen += d[i];
            }

            int eleventhDigit = totalFirstTen % 10;
            return eleventhDigit == d[10];
        }

        /// <summary>
        /// Türkiye Vergi Kimlik Numarası (VKN - 10 Hane) Resmi Mod-9 / Mod-10 Algoritması.
        /// - 10 haneli ve tamamen rakamlardan oluşmalı.
        /// - İlk 9 hanenin her biri için formül uygulanıp toplanır, son hane ile kontrol edilir.
        /// </summary>
        public static bool IsValidTaxNumber(string? taxNumber)
        {
            if (string.IsNullOrWhiteSpace(taxNumber))
                return false;

            var trimmed = taxNumber.Trim();
            if (trimmed.Length != 10 || !trimmed.All(char.IsDigit))
                return false;

            int sum = 0;
            for (int i = 0; i < 9; i++)
            {
                int digit = trimmed[i] - '0';
                int c = (digit + 10 - (i + 1)) % 10;
                int term = (int)((c * Math.Pow(2, 9 - i)) % 9);
                if (c != 0 && term == 0)
                {
                    term = 9;
                }
                sum += term;
            }

            int checkDigit = (10 - (sum % 10)) % 10;
            return checkDigit == (trimmed[9] - '0');
        }
    }
}
