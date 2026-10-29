using FluentAssertions;
using KamatekCrm.ApplicationCore.ErrorHandling;
using Xunit;

namespace KamatekCrm.Tests.Diagnostics;

public sealed class ApplicationErrorCorrelationTests
{
    [Fact]
    public void Constructor_GeneratesCompactCorrelationId()
    {
        var error = new ApplicationError(ErrorCategory.Unexpected, "Beklenmeyen hata");

        error.CorrelationId.Should().MatchRegex(@"^ERR-\d{8}-[0-9a-f]{8}$");
    }

    [Fact]
    public void Constructor_PreservesProvidedCorrelationId()
    {
        var error = new ApplicationError(
            ErrorCategory.Validation,
            "Gecersiz veri",
            correlationId: "CRL-FLOW-42");

        error.CorrelationId.Should().Be("CRL-FLOW-42");
    }
}
