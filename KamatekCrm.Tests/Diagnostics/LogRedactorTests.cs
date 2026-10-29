using System.Collections.Generic;
using FluentAssertions;
using KamatekCrm.Diagnostics.Core;
using Xunit;

namespace KamatekCrm.Tests.Diagnostics;

public sealed class LogRedactorTests
{
    [Fact]
    public void Redact_PasswordInText_IsMasked()
    {
        var result = LogRedactor.Redact("login failed Password=Sup3rGizli! user=ali");

        result.Should().Contain("Password=***");
        result.Should().NotContain("Sup3rGizli");
    }

    [Fact]
    public void Redact_ConnectionString_KeepsHostButMasksCredentials()
    {
        const string input =
            "Host=127.0.0.1;Port=5432;Database=kamatekcrm;Username=postgres;Password=123456";

        var result = LogRedactor.Redact(input);

        result.Should().Contain("Host=127.0.0.1");
        result.Should().NotContain("123456");
        result.Should().NotContain("postgres");
        result.Should().Contain("Password=***");
    }

    [Fact]
    public void Redact_EncryptedConnectionString_DoesNotLeakDpapiBlob()
    {
        const string input = "Password=\"ENC:AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAAYhJD5Hcnl0u9\"";
        const string standalone = "baglanti ENC:AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAAYhJD5Hcnl0u9";

        LogRedactor.Redact(input).Should().NotContain("AQAAANCMnd8");
        LogRedactor.Redact(standalone).Should().Contain("ENC:***");
        LogRedactor.Redact(standalone).Should().NotContain("AQAAANCMnd8");
    }

    [Fact]
    public void Redact_BearerTokenAndJwt_AreMasked()
    {
        const string bearer = "Authorization: Bearer abcdef1234567890abcdef";
        const string jwt =
            "token eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U kullanildi";

        var bearerResult = LogRedactor.Redact(bearer);
        bearerResult.Should().Contain("***");
        bearerResult.Should().NotContain("abcdef1234567890abcdef");

        var jwtResult = LogRedactor.Redact(jwt);
        jwtResult.Should().NotContain("eyJhbGciOiJIUzI1NiJ9");
    }

    [Fact]
    public void Redact_EmailPhoneTcknAndCard_AreMasked()
    {
        const string input =
            "E-posta: ali.veli@example.com, Tel: 0555 123 45 67, TCKN=12345678901, Kart: 4111 1111 1111 1111";

        var result = LogRedactor.Redact(input);

        result.Should().NotContain("ali.veli@example.com");
        result.Should().NotContain("12345678901");
        result.Should().NotContain("4111 1111");
        result.Should().Contain("***@***");
    }

    [Fact]
    public void Redact_OrdinaryMessage_IsUnchanged()
    {
        const string input = "Satış tamamlandı: 3 kalem, toplam 1250,00 TL";

        LogRedactor.Redact(input).Should().Be(input);
    }

    [Fact]
    public void RedactProperties_MasksSensitiveKeysAndKeepsOthers()
    {
        var properties = new Dictionary<string, string?>
        {
            ["Password"] = "gizli",
            ["ApiKey"] = "abc123",
            ["CustomerCode"] = "MUS-001"
        };

        var result = LogRedactor.RedactProperties(properties);

        result.Should().NotBeNull();
        result!["Password"].Should().Be(LogRedactor.Mask);
        result["ApiKey"].Should().Be(LogRedactor.Mask);
        result["CustomerCode"].Should().Be("MUS-001");
    }

    [Fact]
    public void RedactAndTruncate_LongText_IsLimited()
    {
        var input = new string('a', 500);

        var result = LogRedactor.RedactAndTruncate(input, 100);

        result.Should().HaveLength(100 + "…(kısaltıldı)".Length);
        result.Should().EndWith("…(kısaltıldı)");
    }

    [Fact]
    public void Redact_NullOrEmpty_ReturnsEmpty()
    {
        LogRedactor.Redact(null).Should().BeEmpty();
        LogRedactor.Redact(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void ToSingleLine_RemovesLineBreaks()
    {
        var result = LogRedactor.ToSingleLine("birinci satır\r\nikinci satır\nüçüncü");

        result.Should().Be("birinci satır  ikinci satır üçüncü");
    }
}
