using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;

namespace KamatekCrm.ApplicationCore.Services;

public sealed class PersonalDataProtectionService : IPersonalDataProtectionService
{
    private readonly IApplicationAuthorizationService _authorization;

    public PersonalDataProtectionService(IApplicationAuthorizationService authorization)
    {
        _authorization = authorization;
    }

    public bool CanView(PersonalDataKind kind) => _authorization.IsAuthorized(
        kind is PersonalDataKind.NationalIdentity or PersonalDataKind.TaxNumber
            ? ApplicationPermission.ViewCustomerIdentityData
            : ApplicationPermission.ViewCustomerContactData);

    public string Protect(string? value, PersonalDataKind kind)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (CanView(kind)) return value.Trim();

        return kind switch
        {
            PersonalDataKind.Phone => MaskPhone(value),
            PersonalDataKind.Email => MaskEmail(value),
            PersonalDataKind.Address => "Adres bilgisi kısıtlı",
            PersonalDataKind.NationalIdentity => MaskIdentifier(value, 2),
            PersonalDataKind.TaxNumber => MaskIdentifier(value, 2),
            _ => "••••"
        };
    }

    private static string MaskPhone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length < 4) return "••••";
        return $"*** *** ** {digits[^2..]}";
    }

    private static string MaskEmail(string value)
    {
        var parts = value.Trim().Split('@', 2);
        if (parts.Length != 2) return "••••";
        var prefix = parts[0].Length == 0 ? "*" : parts[0][0] + "***";
        return $"{prefix}@{parts[1]}";
    }

    private static string MaskIdentifier(string value, int visibleSuffix)
    {
        var normalized = value.Trim();
        if (normalized.Length <= visibleSuffix) return new string('•', normalized.Length);
        return new string('•', normalized.Length - visibleSuffix) + normalized[^visibleSuffix..];
    }
}
