using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using KamatekCrm.Diagnostics.Core;
using Serilog;
using Xunit;

namespace KamatekCrm.Tests.Diagnostics;

/// <summary>
/// Gerçek dosya sink'i + redaksiyon + korelasyon enricher birlikte çalışıyor mu?
/// Geçici dizinde çalışır; kullanıcının gerçek log klasörüne dokunmaz.
/// </summary>
public sealed class FileLoggingIntegrationTests
{
    [Fact]
    public void FileSink_WritesRedactedLineWithCorrelationId()
    {
        var directory = Path.Combine(Path.GetTempPath(), "kamatek-diag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            using (CorrelationContext.BeginScope("CRL-FILE-1"))
            {
                var logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.With(new CorrelationIdEnricher())
                    .WriteTo.File(
                        formatter: new RedactingTextFormatter(
                            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {CorrelationId} {Message:lj}"),
                        path: Path.Combine(directory, "test-.log"))
                    .CreateLogger();

                logger.Information("giris denemesi Password=123456");
                logger.Dispose();
            }

            var file = Directory.GetFiles(directory, "test-*.log").Single();
            var content = File.ReadAllText(file);

            content.Should().Contain("CRL-FILE-1");
            content.Should().Contain("Password=***");
            content.Should().NotContain("123456");
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // Geçici dizin silinemezse test sonucu etkilenmesin.
            }
        }
    }

    [Fact]
    public void InMemorySink_CapturesWhatSerilogEmits()
    {
        var store = new InMemoryDiagnosticSink(100);
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(new SerilogSinkAdapter(store))
            .CreateLogger();

        using (CorrelationContext.BeginScope("CRL-MEM-1"))
        {
            logger.Warning("stok azaldi: {Adet}", 2);
        }

        var events = store.GetSnapshot();
        events.Should().ContainSingle();
        events[0].Message.Should().Contain("stok azaldi: 2");
        events[0].Level.Should().Be(DiagnosticLevel.Warning);
        events[0].CorrelationId.Should().Be("CRL-MEM-1");
    }
}
