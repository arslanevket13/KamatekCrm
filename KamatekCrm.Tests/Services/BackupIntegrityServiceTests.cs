using System.IO;
using FluentAssertions;
using KamatekCrm.Services;

namespace KamatekCrm.Tests.Services;

public class BackupIntegrityServiceTests
{
    [Fact]
    public void CreateManifest_ThenValidate_ReturnsVerifiedMetadata()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var backupPath = Path.Combine(directory, "verified.backup");
            File.WriteAllBytes(backupPath, Enumerable.Range(0, 2048).Select(value => (byte)(value % 251)).ToArray());
            var service = new BackupIntegrityService();

            var manifest = service.CreateManifest(backupPath, "kamatek_test");
            var result = service.Validate(backupPath);

            result.IsValid.Should().BeTrue();
            result.Manifest.Should().BeEquivalentTo(manifest);
            manifest.Sha256.Should().HaveLength(64);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Validate_WhenArchiveWasModified_RejectsBackup()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var backupPath = Path.Combine(directory, "tampered.backup");
            File.WriteAllBytes(backupPath, [1, 2, 3, 4, 5]);
            var service = new BackupIntegrityService();
            service.CreateManifest(backupPath, "kamatek_test");

            File.AppendAllText(backupPath, "modified");
            var result = service.Validate(backupPath);

            result.IsValid.Should().BeFalse();
            result.Message.Should().MatchRegex("boyutu|SHA-256");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Validate_WithoutManifest_RejectsUntrustedBackup()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var backupPath = Path.Combine(directory, "unknown.backup");
            File.WriteAllBytes(backupPath, [1, 2, 3]);

            var result = new BackupIntegrityService().Validate(backupPath);

            result.IsValid.Should().BeFalse();
            result.Message.Should().Contain("manifest");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "KamatekCrmTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
