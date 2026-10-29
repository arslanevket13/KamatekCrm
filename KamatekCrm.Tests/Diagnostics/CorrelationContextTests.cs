using System;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.Diagnostics.Core;
using Xunit;

namespace KamatekCrm.Tests.Diagnostics;

public sealed class CorrelationContextTests
{
    [Fact]
    public void BeginScope_SetsAndRestoresCorrelationId()
    {
        CorrelationContext.Clear();

        using (CorrelationContext.BeginScope("CRL-TEST-1"))
        {
            CorrelationContext.Current.Should().Be("CRL-TEST-1");
        }

        CorrelationContext.Current.Should().BeNull();
    }

    [Fact]
    public void NestedScopes_RestorePreviousValue()
    {
        CorrelationContext.Clear();

        using (CorrelationContext.BeginScope("outer"))
        {
            using (CorrelationContext.BeginScope("inner"))
            {
                CorrelationContext.Current.Should().Be("inner");
            }

            CorrelationContext.Current.Should().Be("outer");
        }
    }

    [Fact]
    public async Task Scope_FlowsAcrossAwaitWithoutLeaking()
    {
        CorrelationContext.Clear();

        using (CorrelationContext.BeginScope("async-scope"))
        {
            var observed = await Task.Run(() => CorrelationContext.Current);
            observed.Should().Be("async-scope");
        }

        CorrelationContext.Current.Should().BeNull();
    }

    [Fact]
    public void NewId_HasExpectedShape()
    {
        var id = CorrelationContext.NewId("TST");

        id.Should().StartWith("TST-");
        id.Should().Contain(DateTime.UtcNow.ToString("yyyyMMdd"));
        id.Length.Should().BeGreaterThan(15);
    }

    [Fact]
    public void SetAndClear_WorkWithoutScope()
    {
        CorrelationContext.Set("manual-id");
        CorrelationContext.Current.Should().Be("manual-id");

        CorrelationContext.Clear();
        CorrelationContext.Current.Should().BeNull();
    }

    [Fact]
    public void BeginScope_WithoutId_GeneratesOne()
    {
        using var scope = CorrelationContext.BeginScope();

        CorrelationContext.Current.Should().NotBeNullOrWhiteSpace();
        CorrelationContext.Current.Should().StartWith("CRL-");
    }
}
