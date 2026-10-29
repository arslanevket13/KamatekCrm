using System;
using System.Linq;
using FluentAssertions;
using KamatekCrm.Diagnostics.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace KamatekCrm.Tests.Diagnostics;

public sealed class SerilogSinkAdapterTests
{
    private static LogEvent CreateLogEvent(
        LogEventLevel level,
        string template,
        Exception? exception = null,
        params LogEventProperty[] properties)
    {
        return new LogEvent(
            DateTimeOffset.Now,
            level,
            exception,
            new MessageTemplateParser().Parse(template),
            properties);
    }

    [Fact]
    public void Map_TransfersLevelModuleAndMessage()
    {
        var logEvent = CreateLogEvent(
            LogEventLevel.Warning,
            "Stok seviyesi kritik: {Quantity}",
            properties: new[]
            {
                new LogEventProperty("SourceContext", new ScalarValue("KamatekCrm.Services.StockService")),
                new LogEventProperty("Quantity", new ScalarValue(3))
            });

        var mapped = SerilogSinkAdapter.Map(logEvent);

        mapped.Level.Should().Be(DiagnosticLevel.Warning);
        mapped.Module.Should().Be("StockService");
        mapped.Message.Should().Contain("Stok seviyesi kritik");
        mapped.Properties.Should().NotBeNull();
        mapped.Properties!["Quantity"].Should().Be("3");
    }

    [Fact]
    public void Map_UsesAmbientCorrelationIdWhenPropertyMissing()
    {
        using var scope = CorrelationContext.BeginScope("CRL-MAP-1");
        var logEvent = CreateLogEvent(LogEventLevel.Information, "islem");

        var mapped = SerilogSinkAdapter.Map(logEvent);

        mapped.CorrelationId.Should().Be("CRL-MAP-1");
    }

    [Fact]
    public void Map_RedactsSensitiveValuesInMessage()
    {
        var logEvent = CreateLogEvent(
            LogEventLevel.Error,
            "Baglanti kurulamadi: Password=123456");

        var mapped = SerilogSinkAdapter.Map(logEvent);

        mapped.Message.Should().NotContain("123456");
        mapped.Message.Should().Contain("Password=***");
    }

    [Fact]
    public void Map_TransfersExceptionDetails()
    {
        var exception = new InvalidOperationException("beklenmeyen durum");
        var logEvent = CreateLogEvent(LogEventLevel.Error, "hata olustu", exception);

        var mapped = SerilogSinkAdapter.Map(logEvent);

        mapped.ExceptionType.Should().Be(typeof(InvalidOperationException).FullName);
        mapped.ExceptionMessage.Should().Be("beklenmeyen durum");
        mapped.StackTrace.Should().Contain("InvalidOperationException");
        mapped.IsError.Should().BeTrue();
    }

    [Fact]
    public void Emit_WhenSinkThrows_DoesNotPropagate()
    {
        var adapter = new SerilogSinkAdapter(new ThrowingSink());
        var logEvent = CreateLogEvent(LogEventLevel.Information, "test");

        var action = () => adapter.Emit(logEvent);

        action.Should().NotThrow();
    }

    [Fact]
    public void Emit_WritesMappedEventToSink()
    {
        var store = new InMemoryDiagnosticSink(100);
        var adapter = new SerilogSinkAdapter(store);

        adapter.Emit(CreateLogEvent(LogEventLevel.Information, "kayit {Id}", properties: new[]
        {
            new LogEventProperty("Id", new ScalarValue(42))
        }));

        var snapshot = store.GetSnapshot();
        snapshot.Should().ContainSingle();
        snapshot[0].Message.Should().Contain("kayit 42");
    }

    private sealed class ThrowingSink : IDiagnosticSink
    {
        public void Publish(DiagnosticEvent diagnosticEvent) =>
            throw new InvalidOperationException("sink hatasi");
    }
}
