using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FluentAssertions;
using KamatekCrm.Diagnostics.Core;
using Xunit;

namespace KamatekCrm.Tests.Diagnostics;

/// <summary>
/// Tanılama paketi: redaksiyon, yol kısıtı, boyut sınırı ve saklama politikası testleri.
/// Testler gerçek %APPDATA% dizinini kullanmaz; geçici dizinler enjekte edilir.
/// </summary>
public sealed class DiagnosticPackageServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _logs;
    private readonly string _crashes;
    private readonly string _exports;

    public DiagnosticPackageServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "kamatek-paket-test-" + Guid.NewGuid().ToString("N")[..8]);
        _logs = Path.Combine(_root, "Logs");
        _crashes = Path.Combine(_root, "Crashes");
        _exports = Path.Combine(_root, "Exports");

        Directory.CreateDirectory(_logs);
        Directory.CreateDirectory(_crashes);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Test temizliği başarısız olsa da test sonucu etkilenmesin.
        }
    }

    private DiagnosticPackageService CreateService(
        long? maxPackageBytes = null,
        long? maxBytesPerFile = null,
        int? retentionCount = null)
        => new(_logs, _crashes, _exports, maxPackageBytes, maxBytesPerFile, retentionCount: retentionCount);

    [Fact]
    public void CreatePackage_RedactsSensitiveValues()
    {
        File.WriteAllLines(Path.Combine(_logs, "gunluk-20261029.log"), new[]
        {
            "2026-10-29 10:00:00 [INF] Giris denemesi password=Sup3rGizli!",
            "2026-10-29 10:00:01 [INF] Kullanici admin@ornek.com ile giris yapti",
            "2026-10-29 10:00:02 [INF] Authorization: Bearer abcdefgh12345678xyz",
            "2026-10-29 10:00:03 [INF] Telefon 0555 123 45 67 kaydedildi"
        });

        File.WriteAllText(
            Path.Combine(_crashes, "crash-20261029-100000-deadbeef.json"),
            "{\"exceptionType\":\"System.InvalidOperationException\",\"message\":\"token=abc123secret\"}");

        var result = CreateService().CreatePackage();

        result.Succeeded.Should().BeTrue(result.ErrorMessage);
        result.EntryCount.Should().Be(3); // 1 log + 1 çökme + manifest

        using var archive = ZipFile.OpenRead(result.FilePath!);
        archive.Entries.Select(entry => entry.FullName).Should().Contain(new[]
        {
            "logs/gunluk-20261029.log",
            "crashes/crash-20261029-100000-deadbeef.json",
            "manifest.json"
        });

        var logText = ReadEntry(archive, "logs/gunluk-20261029.log");
        logText.Should().NotContain("Sup3rGizli");
        logText.Should().NotContain("admin@ornek.com");
        logText.Should().NotContain("abcdefgh12345678xyz");
        logText.Should().NotContain("0555 123 45 67");
        logText.Should().Contain("***");

        var crashText = ReadEntry(archive, "crashes/crash-20261029-100000-deadbeef.json");
        crashText.Should().NotContain("abc123secret");

        var manifest = ReadEntry(archive, "manifest.json");
        manifest.Should().Contain("redaksiyon");
    }

    [Fact]
    public void CreatePackage_WhenSourceDirectoriesMissing_StillCreatesManifestOnlyPackage()
    {
        var service = new DiagnosticPackageService(
            Path.Combine(_root, "yok-logs"),
            Path.Combine(_root, "yok-crashes"),
            _exports);

        var result = service.CreatePackage();

        result.Succeeded.Should().BeTrue(result.ErrorMessage);
        result.EntryCount.Should().Be(1);
        result.Warnings.Should().NotBeEmpty();

        using var archive = ZipFile.OpenRead(result.FilePath!);
        archive.Entries.Select(entry => entry.FullName).Should().ContainSingle().Which.Should().Be("manifest.json");
    }

    [Fact]
    public void CreatePackage_WhenFileExceedsPerFileLimit_TruncatesAndWarns()
    {
        var path = Path.Combine(_logs, "buyuk.log");
        var line = "2026-10-29 10:00:00 [INF] " + new string('x', 60);
        File.WriteAllLines(path, Enumerable.Repeat(line, 200));

        var result = CreateService(maxBytesPerFile: 2048).CreatePackage();

        result.Succeeded.Should().BeTrue(result.ErrorMessage);
        result.Warnings.Should().Contain(warning => warning.Contains("sınırında kesildi"));

        using var archive = ZipFile.OpenRead(result.FilePath!);
        var text = ReadEntry(archive, "logs/buyuk.log");
        text.Should().Contain("sınırında kesildi");
        Encoding.UTF8.GetByteCount(text).Should().BeLessThan(4096);
    }

    [Fact]
    public void CreatePackage_WhenBudgetExhausted_StopsAddingFiles()
    {
        for (var i = 0; i < 3; i++)
        {
            var line = "2026-10-29 10:00:00 [INF] " + new string('y', 60);
            File.WriteAllLines(Path.Combine(_logs, $"parca-{i}.log"), Enumerable.Repeat(line, 200));
        }

        var result = CreateService(maxPackageBytes: 4096, maxBytesPerFile: 2048).CreatePackage();

        result.Succeeded.Should().BeTrue(result.ErrorMessage);
        result.Warnings.Should().Contain(warning => warning.Contains("boyut sınırı"));

        using var archive = ZipFile.OpenRead(result.FilePath!);
        archive.Entries.Count(entry => entry.FullName.StartsWith("logs/", StringComparison.Ordinal))
            .Should().BeLessThan(3);
    }

    [Fact]
    public void CreatePackage_WhenOnlyLogsSelected_ExcludesCrashes()
    {
        File.WriteAllText(Path.Combine(_logs, "a.log"), "satir");
        File.WriteAllText(Path.Combine(_crashes, "crash-1.json"), "{}");

        var result = CreateService().CreatePackage(new DiagnosticPackageRequest(IncludeLogs: true, IncludeCrashes: false));

        result.Succeeded.Should().BeTrue(result.ErrorMessage);
        using var archive = ZipFile.OpenRead(result.FilePath!);
        archive.Entries.Select(entry => entry.FullName).Should().NotContain(name => name.StartsWith("crashes/"));
    }

    [Fact]
    public void DeletePackage_RejectsPathsOutsideExportDirectory()
    {
        var service = CreateService();
        Directory.CreateDirectory(_exports);

        var victim = Path.Combine(_root, "disarida.zip");
        File.WriteAllText(victim, "silinmemeli");

        service.DeletePackage("../disarida.zip").Should().BeFalse();
        service.DeletePackage("..\\disarida.zip").Should().BeFalse();
        service.DeletePackage(victim).Should().BeFalse();
        service.DeletePackage("notlar.txt").Should().BeFalse();
        service.DeletePackage("alt/disarida.zip").Should().BeFalse();
        service.DeletePackage(null).Should().BeFalse();
        service.DeletePackage(string.Empty).Should().BeFalse();

        File.Exists(victim).Should().BeTrue();
    }

    [Fact]
    public void DeletePackage_RemovesPackageInsideExportDirectory()
    {
        var service = CreateService();
        var result = service.CreatePackage(new DiagnosticPackageRequest(false, false));

        service.DeletePackage(result.FileName).Should().BeTrue();
        File.Exists(result.FilePath).Should().BeFalse();
        service.GetPackages().Should().BeEmpty();
    }

    [Fact]
    public void CreatePackage_PrunesBeyondRetentionCount()
    {
        File.WriteAllText(Path.Combine(_logs, "a.log"), "satir");
        var service = CreateService(retentionCount: 2);

        for (var i = 0; i < 3; i++)
        {
            var res = service.CreatePackage();
            res.Succeeded.Should().BeTrue(res.ErrorMessage);
        }

        service.GetPackages(50).Should().HaveCount(2);
        Directory.GetFiles(_exports, "*.zip").Should().HaveCount(2);
    }

    [Fact]
    public void GetPackages_IgnoresUnrelatedFiles()
    {
        Directory.CreateDirectory(_exports);
        File.WriteAllText(Path.Combine(_exports, "notlar.txt"), "x");
        File.WriteAllText(Path.Combine(_exports, "baska.zip"), "x");

        var service = CreateService();

        service.GetPackages().Should().BeEmpty();
    }

    private static string ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name);
        entry.Should().NotBeNull($"'{name}' pakette bulunmalı");

        using var stream = entry!.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
