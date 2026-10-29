using System.Collections.Generic;
using FluentAssertions;
using KamatekCrm.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace KamatekCrm.Tests.Diagnostics;

public sealed class DiagnosticOptionsTests
{
    [Fact]
    public void FromConfiguration_ReadsValuesAndClampsCapacity()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Diagnostics:Enabled"] = "true",
                ["Diagnostics:MinimumLevel"] = "Debug",
                ["Diagnostics:BufferCapacity"] = "999999",
                ["Diagnostics:RetainedFileCountLimit"] = "7",
                ["Diagnostics:DeveloperAccounts:0"] = "dev1",
                ["Diagnostics:DeveloperAccounts:1"] = "  "
            })
            .Build();

        var options = DiagnosticOptions.FromConfiguration(configuration);

        options.MinimumLevel.Should().Be("Debug");
        options.BufferCapacity.Should().Be(100_000);
        options.RetainedFileCountLimit.Should().Be(7);
        options.DeveloperAccounts.Should().ContainSingle().Which.Should().Be("dev1");
    }

    [Fact]
    public void FromConfiguration_WithoutSection_ReturnsSafeDefaults()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = DiagnosticOptions.FromConfiguration(configuration);

        options.Enabled.Should().BeTrue();
        options.MinimumLevel.Should().Be("Debug");
        options.BufferCapacity.Should().Be(5_000);
        options.DeveloperAccounts.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_ClampsInvalidValues()
    {
        var options = new DiagnosticOptions
        {
            MinimumLevel = "gecersiz-seviye",
            BufferCapacity = -5,
            FileSizeLimitBytes = 1,
            RetainedFileCountLimit = 0,
            UiFreezeThresholdMs = 99_999
        }.Normalize();

        options.MinimumLevel.Should().Be("Information");
        options.BufferCapacity.Should().Be(100);
        options.FileSizeLimitBytes.Should().Be(1024 * 1024);
        options.RetainedFileCountLimit.Should().Be(1);
        options.UiFreezeThresholdMs.Should().Be(10_000);
    }

    [Fact]
    public void Normalize_DoesNotMutateOriginal()
    {
        var original = new DiagnosticOptions { BufferCapacity = 1 };

        var normalized = original.Normalize();

        original.BufferCapacity.Should().Be(1);
        normalized.BufferCapacity.Should().Be(100);
    }
}
