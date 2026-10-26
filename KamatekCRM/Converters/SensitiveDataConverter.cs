using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using Microsoft.Extensions.DependencyInjection;

namespace KamatekCrm.Converters;

public sealed class SensitiveDataConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;
        var rawText = value.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

        var paramStr = parameter?.ToString()?.Trim() ?? string.Empty;

        // Özel parametre: Iban / IBAN kontrolü
        if (string.Equals(paramStr, "Iban", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(paramStr, "IBAN", StringComparison.OrdinalIgnoreCase))
        {
            return MaskIban(rawText);
        }

        if (Enum.TryParse<PersonalDataKind>(paramStr, true, out var kind))
        {
            try
            {
                var protection = App.ServiceProvider?.GetService<IPersonalDataProtectionService>();
                if (protection != null)
                {
                    return protection.Protect(rawText, kind);
                }
            }
            catch
            {
                // DI veya servis hatasında güvenli fallback
            }

            // Güvenli yerel fallback (ArgumentOutOfRangeException korumalı)
            return SafeFallbackMask(rawText, kind);
        }

        return "••••";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;

    private static string MaskIban(string rawText)
    {
        var clean = new string(rawText.Where(char.IsLetterOrDigit).ToArray());
        if (clean.Length <= 6) return new string('•', clean.Length);
        var prefix = clean.Length >= 2 ? clean[..2] : "";
        var suffix = clean[^4..];
        return $"{prefix}** **** **** **** {suffix}";
    }

    private static string SafeFallbackMask(string text, PersonalDataKind kind)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return string.Empty;

        return kind switch
        {
            PersonalDataKind.Phone => MaskPhoneSafe(trimmed),
            PersonalDataKind.Email => MaskEmailSafe(trimmed),
            PersonalDataKind.NationalIdentity or PersonalDataKind.TaxNumber => MaskIdentifierSafe(trimmed, 2),
            PersonalDataKind.Address => "Adres bilgisi kısıtlı",
            _ => "••••"
        };
    }

    private static string MaskPhoneSafe(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length < 4) return "••••";
        return $"*** *** ** {digits[^2..]}";
    }

    private static string MaskEmailSafe(string value)
    {
        var parts = value.Split('@', 2);
        if (parts.Length != 2 || string.IsNullOrEmpty(parts[0])) return "••••";
        var prefix = parts[0].Length <= 1 ? "*" : parts[0][0] + "***";
        return $"{prefix}@{parts[1]}";
    }

    private static string MaskIdentifierSafe(string value, int visibleSuffix)
    {
        if (value.Length <= visibleSuffix) return new string('•', value.Length);
        return new string('•', value.Length - visibleSuffix) + value[^visibleSuffix..];
    }
}
