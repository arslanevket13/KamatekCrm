using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace KamatekCrm.Diagnostics.Core
{
    /// <summary>
    /// Log ve tanılama kayıtlarındaki hassas verileri maskeler.
    /// <para>
    /// Amaç: parola, token, API anahtarı, DPAPI ile şifrelenmiş bağlantı dizesi,
    /// e-posta, telefon, TCKN, IBAN ve kart numarası gibi değerlerin diske veya
    /// arayüze düz metin olarak çıkmaması.
    /// </para>
    /// </summary>
    public static class LogRedactor
    {
        public const string Mask = "***";

        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

        private static readonly RegexOptions Options =
            RegexOptions.Compiled | RegexOptions.CultureInvariant;

        private static readonly Regex BearerPattern = new(
            @"(?i)\bBearer\s+[A-Za-z0-9\-\._~\+/]{8,}=*",
            Options, RegexTimeout);

        private static readonly Regex JwtPattern = new(
            @"\beyJ[A-Za-z0-9_\-]{8,}\.[A-Za-z0-9_\-]{8,}\.[A-Za-z0-9_\-]{8,}\b",
            Options, RegexTimeout);

        private static readonly Regex EncPattern = new(
            @"\bENC:[A-Za-z0-9+/=]{8,}",
            Options, RegexTimeout);

        private static readonly Regex PasswordPattern = new(
            @"(?i)\b(password|pwd|passwd|parola|şifre|sifre)\b\s*[=:]\s*(""[^""\r\n]*""|[^;,\r\n""]*)",
            Options, RegexTimeout);

        private static readonly Regex TokenPattern = new(
            @"(?i)\b(token|access[_-]?token|refresh[_-]?token|api[_-]?key|apikey|client[_-]?secret|secret|authorization)\b\s*[=:]\s*(""[^""\r\n]*""|[^;,\r\n""]*)",
            Options, RegexTimeout);

        private static readonly Regex UserNamePattern = new(
            @"(?i)\b(user\s*id|uid|username|kullanıcı\s*adı)\b\s*=\s*([^;,\r\n]*)",
            Options, RegexTimeout);

        private static readonly Regex EmailPattern = new(
            @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}",
            Options, RegexTimeout);

        private static readonly Regex PhonePattern = new(
            @"(?<!\d)(?:\+90|0)?\s?5\d{2}[\s\-]?\d{3}[\s\-]?\d{2}[\s\-]?\d{2}(?!\d)",
            Options, RegexTimeout);

        private static readonly Regex TcknPattern = new(
            @"(?i)\b(tckn|tc\s*kimlik(\s*no)?|tcno)\b\s*[=:]?\s*\d{11}",
            Options, RegexTimeout);

        private static readonly Regex IbanPattern = new(
            @"\bTR\d{2}\s?(?:\d{4}\s?){5}\d{2}\b",
            Options, RegexTimeout);

        private static readonly Regex CardPattern = new(
            @"(?<!\d)(?:\d[ \-]?){15}\d(?!\d)",
            Options, RegexTimeout);

        private static readonly HashSet<string> SensitiveKeyFragments = new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "pwd", "passwd", "parola", "şifre", "sifre",
            "token", "secret", "apikey", "api_key", "authorization",
            "connectionstring", "connection_string", "credential"
        };

        /// <summary>
        /// Metindeki bilinen hassas desenleri maskeler. Null/boş girdi boş string döner.
        /// </summary>
        public static string Redact(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var result = value;

            result = ReplaceSafe(BearerPattern, result, _ => $"Bearer {Mask}");
            result = ReplaceSafe(JwtPattern, result, _ => Mask);
            result = ReplaceSafe(EncPattern, result, _ => $"ENC:{Mask}");
            result = ReplaceSafe(PasswordPattern, result, m => $"{m.Groups[1].Value}={Mask}");
            result = ReplaceSafe(TokenPattern, result, m => $"{m.Groups[1].Value}={Mask}");
            result = ReplaceSafe(UserNamePattern, result, m => $"{m.Groups[1].Value}={Mask}");
            result = ReplaceSafe(EmailPattern, result, _ => $"***@***");
            result = ReplaceSafe(PhonePattern, result, _ => Mask);
            result = ReplaceSafe(TcknPattern, result, m => $"{m.Groups[1].Value}={Mask}");
            result = ReplaceSafe(IbanPattern, result, _ => "TR************************");
            result = ReplaceSafe(CardPattern, result, _ => "**** **** **** ****");

            return result;
        }

        /// <summary>
        /// Redakte eder ve uzun metinleri kısaltır (stack trace / büyük payload koruması).
        /// </summary>
        public static string RedactAndTruncate(string? value, int maxLength = 4000)
        {
            var redacted = Redact(value);
            if (maxLength <= 0 || redacted.Length <= maxLength)
            {
                return redacted;
            }

            return redacted[..maxLength] + "…(kısaltıldı)";
        }

        /// <summary>Anahtar adı hassas alanı işaret ediyorsa değer tamamen maskelenir.</summary>
        public static bool IsSensitiveKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            foreach (var fragment in SensitiveKeyFragments)
            {
                if (key.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Yapısal log alanlarını redakte eder; anahtar adı hassassa değeri maskeler.</summary>
        public static IReadOnlyDictionary<string, string?>? RedactProperties(
            IReadOnlyDictionary<string, string?>? properties)
        {
            if (properties == null || properties.Count == 0)
            {
                return null;
            }

            var result = new Dictionary<string, string?>(properties.Count, StringComparer.Ordinal);
            foreach (var pair in properties)
            {
                result[pair.Key] = IsSensitiveKey(pair.Key)
                    ? Mask
                    : RedactAndTruncate(pair.Value, 512);
            }

            return result;
        }

        /// <summary>Metinde maskelenmesi gereken bir iz olup olmadığını hızlıca bildirir (test/telemetri için).</summary>
        public static bool ContainsSensitiveMarker(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.Contains("password", StringComparison.OrdinalIgnoreCase)
                || value.Contains("pwd=", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
                || value.Contains("ENC:", StringComparison.OrdinalIgnoreCase)
                || value.Contains("@", StringComparison.Ordinal);
        }

        private static string ReplaceSafe(Regex regex, string input, MatchEvaluator evaluator)
        {
            try
            {
                return regex.Replace(input, evaluator);
            }
            catch (RegexMatchTimeoutException)
            {
                // Zaman aşımında orijinal metni sızdırmamak için tamamını maskele.
                return Mask;
            }
        }

        /// <summary>Test ve tanılama paketi için: metnin tamamını tek satırlık güvenli forma çevirir.</summary>
        public static string ToSingleLine(string? value, int maxLength = 4000)
        {
            var redacted = RedactAndTruncate(value, maxLength);
            if (redacted.Length == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(redacted.Length);
            foreach (var ch in redacted)
            {
                builder.Append(ch is '\r' or '\n' ? ' ' : ch);
            }

            return builder.ToString();
        }
    }
}
