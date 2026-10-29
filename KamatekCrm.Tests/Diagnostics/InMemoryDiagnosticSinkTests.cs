using System.Linq;
using FluentAssertions;
using KamatekCrm.Diagnostics.Core;
using Xunit;

namespace KamatekCrm.Tests.Diagnostics;

public sealed class InMemoryDiagnosticSinkTests
{
    private static DiagnosticEvent CreateEvent(string message, DiagnosticLevel level = DiagnosticLevel.Information, string module = "Test")
        => new()
        {
            Timestamp = System.DateTimeOffset.Now,
            Level = level,
            Module = module,
            Message = message
        };

    [Fact]
    public void Constructor_ClampsCapacityToSafeRange()
    {
        new InMemoryDiagnosticSink(1).Capacity.Should().Be(InMemoryDiagnosticSink.MinimumCapacity);
        new InMemoryDiagnosticSink(int.MaxValue).Capacity.Should().Be(InMemoryDiagnosticSink.MaximumCapacity);
    }

    [Fact]
    public void Publish_AssignsIncreasingSequenceNumbers()
    {
        var sink = new InMemoryDiagnosticSink(100);

        sink.Publish(CreateEvent("birinci"));
        sink.Publish(CreateEvent("ikinci"));

        var snapshot = sink.GetSnapshot();
        snapshot.Should().HaveCount(2);
        snapshot[0].Message.Should().Be("ikinci");
        snapshot[0].Sequence.Should().BeGreaterThan(snapshot[1].Sequence);
    }

    [Fact]
    public void Publish_WhenFull_DropsOldestAndCounts()
    {
        var capacity = InMemoryDiagnosticSink.MinimumCapacity;
        var sink = new InMemoryDiagnosticSink(capacity);

        for (var i = 0; i < capacity + 10; i++)
        {
            sink.Publish(CreateEvent($"olay-{i:D3}"));
        }

        sink.Count.Should().Be(capacity);
        sink.DroppedCount.Should().Be(10);
        sink.GetSnapshot(1)[0].Message.Should().Be($"olay-{capacity + 9:D3}");
    }

    [Fact]
    public void Query_FiltersByLevelModuleAndSearch()
    {
        var sink = new InMemoryDiagnosticSink(100);
        sink.Publish(CreateEvent("db baglantisi kuruldu", DiagnosticLevel.Information, "ConnectionHeartbeatService"));
        sink.Publish(CreateEvent("db baglantisi koptu", DiagnosticLevel.Error, "ConnectionHeartbeatService"));
        sink.Publish(CreateEvent("satis tamamlandi", DiagnosticLevel.Information, "SalesService"));

        sink.Query(new DiagnosticQuery { MinimumLevel = DiagnosticLevel.Error })
            .Should().ContainSingle().Which.Message.Should().Contain("koptu");

        sink.Query(new DiagnosticQuery { Module = "SalesService" })
            .Should().ContainSingle().Which.Message.Should().Contain("satis");

        sink.Query(new DiagnosticQuery { SearchText = "baglanti" })
            .Should().HaveCount(2);
    }

    [Fact]
    public void Query_AppliesSkipAndTake()
    {
        var sink = new InMemoryDiagnosticSink(100);
        for (var i = 0; i < 10; i++)
        {
            sink.Publish(CreateEvent($"olay-{i}"));
        }

        var page = sink.Query(new DiagnosticQuery { Skip = 2, Take = 3 });

        page.Should().HaveCount(3);
        page.Select(item => item.Message).Should().ContainInOrder("olay-7", "olay-6", "olay-5");
    }

    [Fact]
    public void GetSnapshot_RespectsLimitAndReturnsNewestFirst()
    {
        var sink = new InMemoryDiagnosticSink(100);
        sink.Publish(CreateEvent("eski"));
        sink.Publish(CreateEvent("yeni"));

        var snapshot = sink.GetSnapshot(1);

        snapshot.Should().ContainSingle().Which.Message.Should().Be("yeni");
    }

    [Fact]
    public void Clear_ResetsBuffer()
    {
        var sink = new InMemoryDiagnosticSink(100);
        sink.Publish(CreateEvent("olay"));

        sink.Clear();

        sink.Count.Should().Be(0);
        sink.GetSnapshot().Should().BeEmpty();
    }

    [Fact]
    public void NullSink_ReturnsEmptyWithoutThrowing()
    {
        var sink = NullDiagnosticSink.Instance;

        sink.Publish(CreateEvent("yok sayilir"));

        sink.Count.Should().Be(0);
        sink.Query(new DiagnosticQuery()).Should().BeEmpty();
        sink.Capacity.Should().Be(0);
    }
}
