using KamatekCrm.ApplicationCore.Security;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IPersonalDataProtectionService
{
    bool CanView(PersonalDataKind kind);
    string Protect(string? value, PersonalDataKind kind);
}
