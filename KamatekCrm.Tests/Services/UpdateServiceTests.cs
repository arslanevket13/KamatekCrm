using System;
using System.IO;
using System.Text.Json;
using KamatekCrm.Services.Update;
using Velopack;
using Xunit;

namespace KamatekCrm.Tests.Services
{
    public class UpdateServiceTests
    {
        [Theory]
        [InlineData("v2.1.0", "v2.1.1", true)]
        [InlineData("v2.1.1", "v2.1.0", false)]
        [InlineData("v2.1.0", "v2.1.0", false)]
        [InlineData("v2.1.0", "v2.2.0-beta.1", true)]
        public void SemVerComparison_ShouldCorrectlyIdentifyNewerVersion(string currentVerStr, string targetVerStr, bool isTargetNewer)
        {
            // Arrange
            var currentVer = SemanticVersion.Parse(currentVerStr.TrimStart('v'));
            var targetVer = SemanticVersion.Parse(targetVerStr.TrimStart('v'));

            // Act & Assert
            bool actualNewer = targetVer > currentVer;
            Assert.Equal(isTargetNewer, actualNewer);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void UpdateSettings_ShouldSerializeAndDeserializeCorrectly()
        {
            // Arrange
            var settings = new UpdateSettings
            {
                CheckForUpdatesOnStartup = false,
                AutoDownloadUpdates = true,
                UpdateChannel = "Beta",
                InstallOnClose = true,
                LastCheckTime = new DateTime(2026, 8, 3, 20, 0, 0, DateTimeKind.Utc),
                LastDownloadedVersion = "v2.2.0-beta.1"
            };

            // Act
            string json = JsonSerializer.Serialize(settings);
            var deserialized = JsonSerializer.Deserialize<UpdateSettings>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.False(deserialized.CheckForUpdatesOnStartup);
            Assert.True(deserialized.AutoDownloadUpdates);
            Assert.Equal("Beta", deserialized.UpdateChannel);
            Assert.True(deserialized.InstallOnClose);
            Assert.Equal("v2.2.0-beta.1", deserialized.LastDownloadedVersion);
        }

        [Theory]
        [InlineData("Network connection failed", "İnternet bağlantısı kurulamadı. Lütfen ağ bağlantınızı kontrol edin.")]
        [InlineData("HTTP 404 Not Found", "GitHub üzerinde yayınlanmış bir sürüm paketi bulunamadı.")]
        [InlineData("Rate limit exceeded 403", "GitHub erişim sınırı aşıldı. Lütfen daha sonra tekrar deneyin.")]
        [InlineData("Checksum hash mismatch corrupt", "İndirilen paket doğrulanamadı veya dosya bozulmuş.")]
        [InlineData("Disk space full", "Disk alanı yetersiz. Güncelleme indirilemedi.")]
        [InlineData("Access denied permission", "Güncelleme dosyaları için yazma yetkisi bulunmuyor.")]
        public void MapExceptionToUserMessage_ShouldReturnLocalizedUserFriendlyMessage(string exceptionMsg, string expectedUserMsg)
        {
            // Arrange
            var ex = new Exception(exceptionMsg);

            // Act
            string actualMsg = VelopackUpdateService.MapExceptionToUserMessage(ex);

            // Assert
            Assert.Equal(expectedUserMsg, actualMsg);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void UpdateChannel_DefaultValue_ShouldBeStable()
        {
            // Arrange & Act
            var settings = new UpdateSettings();

            // Assert
            Assert.Equal("Stable", settings.UpdateChannel);
            Assert.True(settings.CheckForUpdatesOnStartup);
            Assert.False(settings.AutoDownloadUpdates);
            Assert.False(settings.InstallOnClose);
        }
    }
}
