using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.Services;
using Xunit;

namespace KamatekCrm.Tests.Services;

public sealed class DraftStorageServiceTests : IDisposable
{
    private readonly string _testDirectory;

    public DraftStorageServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "KamatekCrmTests_Drafts_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Test temizliği hatası testi engellememeli
        }
    }

    private record TestDraftPayload(int Id, string Title, decimal Amount, DateTime CreatedAt);

    [Fact]
    public async Task SaveDraftAsync_ConcurrentSavesOnSameKey_DoNotThrowFileNotFoundExceptionAndPreserveIntegrity()
    {
        // Arrange
        var service = new DraftStorageService(_testDirectory);
        const string draftKey = "project_quote_1_R1";
        const int concurrentCount = 25;

        // Act: Aynı anda 25 paralel kaydetme işlemi tetikle (yarış durumunu simüle et)
        var tasks = Enumerable.Range(1, concurrentCount).Select(i =>
        {
            var payload = new TestDraftPayload(i, $"Draft Version {i}", i * 150.5m, DateTime.UtcNow);
            return service.SaveDraftAsync(draftKey, payload);
        }).ToArray();

        // Herhangi bir FileNotFoundException veya I/O çökmesi fırlatılmamalı
        Func<Task> act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();

        // Assert
        service.HasDraft(draftKey).Should().BeTrue();
        var loaded = await service.LoadDraftAsync<TestDraftPayload>(draftKey);
        loaded.Should().NotBeNull();
        loaded!.Id.Should().BeInRange(1, concurrentCount);

        // Dizin içinde yetim kalan .tmp dosyası olmamalı
        var leftoverTemps = Directory.GetFiles(_testDirectory, "*.tmp");
        leftoverTemps.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAndLoadDraftAsync_RoundTripsCorrectly()
    {
        // Arrange
        var service = new DraftStorageService(_testDirectory);
        const string draftKey = "project_quote_2_R1";
        var payload = new TestDraftPayload(42, "Elektrik Tesisatı Projesi", 75000.50m, DateTime.UtcNow);

        // Act
        await service.SaveDraftAsync(draftKey, payload);
        var loaded = await service.LoadDraftAsync<TestDraftPayload>(draftKey);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(42);
        loaded.Title.Should().Be("Elektrik Tesisatı Projesi");
        loaded.Amount.Should().Be(75000.50m);
    }

    [Fact]
    public async Task ClearDraftAsync_RemovesDraftAndStrayTempFiles()
    {
        // Arrange
        var service = new DraftStorageService(_testDirectory);
        const string draftKey = "project_quote_3_R2";
        var payload = new TestDraftPayload(99, "Temizlenecek Taslak", 1000m, DateTime.UtcNow);

        await service.SaveDraftAsync(draftKey, payload);
        service.HasDraft(draftKey).Should().BeTrue();

        // Simüle edilmiş artık .tmp dosyası oluştur
        var strayTemp = Path.Combine(_testDirectory, "project_quote_3_R2_stray.tmp");
        await File.WriteAllTextAsync(strayTemp, "test-data");

        // Act
        await service.ClearDraftAsync(draftKey);

        // Assert
        service.HasDraft(draftKey).Should().BeFalse();
        var loaded = await service.LoadDraftAsync<TestDraftPayload>(draftKey);
        loaded.Should().BeNull();
        File.Exists(strayTemp).Should().BeFalse();
    }

    [Fact]
    public async Task Constructor_CleansUpOrphanedTempFiles()
    {
        // Arrange: Dizin içine önceden çökmüş bir süreçten kalma .tmp dosyaları bırak
        var orphan1 = Path.Combine(_testDirectory, "project_quote_1_R1.json.tmp");
        var orphan2 = Path.Combine(_testDirectory, "some_other_file_123.tmp");
        await File.WriteAllTextAsync(orphan1, "stale");
        await File.WriteAllTextAsync(orphan2, "stale");

        // Act: Servis başlatıldığında temizlemeli
        _ = new DraftStorageService(_testDirectory);

        // Assert
        File.Exists(orphan1).Should().BeFalse();
        File.Exists(orphan2).Should().BeFalse();
    }
}

