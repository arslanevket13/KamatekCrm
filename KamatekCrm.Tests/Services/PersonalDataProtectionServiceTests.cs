using FluentAssertions;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.ApplicationCore.Services;

namespace KamatekCrm.Tests.Services;

public class PersonalDataProtectionServiceTests
{
    [Theory]
    [InlineData("0532 123 45 67", PersonalDataKind.Phone)]
    [InlineData("musteri@example.com", PersonalDataKind.Email)]
    [InlineData("12345678901", PersonalDataKind.NationalIdentity)]
    public void Protect_ReturnsOriginalValue_WhenUserIsAuthorized(
        string value,
        PersonalDataKind kind)
    {
        var service = new PersonalDataProtectionService(new TestAuthorizationService());

        service.Protect(value, kind).Should().Be(value);
    }

    [Theory]
    [InlineData("0532 123 45 67", PersonalDataKind.Phone, "*** *** ** 67")]
    [InlineData("musteri@example.com", PersonalDataKind.Email, "m***@example.com")]
    [InlineData("Ankara Çankaya tam adres", PersonalDataKind.Address, "Adres bilgisi kısıtlı")]
    [InlineData("12345678901", PersonalDataKind.NationalIdentity, "•••••••••01")]
    [InlineData("1234567890", PersonalDataKind.TaxNumber, "••••••••90")]
    public void Protect_MasksValue_WhenUserIsUnauthorized(
        string value,
        PersonalDataKind kind,
        string expected)
    {
        var service = new PersonalDataProtectionService(new TestAuthorizationService(isAuthorized: false));

        service.Protect(value, kind).Should().Be(expected);
    }
}
