using System;
using System.IO;
using FluentAssertions;
using KamatekCrm.Diagnostics.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace KamatekCrm.Tests.Diagnostics;

public sealed class RedactingTextFormatterTests
{
    private static LogEvent CreateLogEvent(string template, Exception? exception = null)
    {
        return new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Information,
            exception,
            new MessageTemplateParser().Parse(template),
            Array.Empty<LogEventProperty>());
    }

    [Fact]
    public void Format_MasksPasswordBeforeWriting()
    {
        var formatter = new RedactingTextFormatter("{Message:lj}");
        using var writer = new StringWriter();

        formatter.Format(CreateLogEvent("giris denemesi Password=123456"), writer);

        var text = writer.ToString();
        text.Should().NotContain("123456");
        text.Should().Contain("Password=***");
    }

    [Fact]
    public void Format_MasksSensitiveExceptionText()
    {
        var formatter = new RedactingTextFormatter("{Message:lj}{NewLine}{Exception}");
        var exception = new InvalidOperationException("baglanti Password=gizli123 kurulamadi");
        using var writer = new StringWriter();

        formatter.Format(CreateLogEvent("hata", exception), writer);

        var text = writer.ToString();
        text.Should().NotContain("gizli123");
        text.Should().Contain("Password=***");
    }

    [Fact]
    public void Format_PreservesNormalContent()
    {
        var formatter = new RedactingTextFormatter("[{Level:u3}] {Message:lj}");
        using var writer = new StringWriter();

        formatter.Format(CreateLogEvent("Satis tamamlandi: 3 kalem"), writer);

        writer.ToString().Should().Be("[INF] Satis tamamlandi: 3 kalem");
    }

    [Fact]
    public void Constructor_RejectsEmptyTemplate()
    {
        var action = () => new RedactingTextFormatter("   ");

        action.Should().Throw<ArgumentException>();
    }
}
