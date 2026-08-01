using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.Services;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Services
{
    public class SmsServiceTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

        public SmsServiceTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        }

        [Fact]
        public async Task SendSmsAsync_ShouldThrowException_WhenPhoneNumberIsInvalid()
        {
            // Arrange
            var service = new SmsService(_httpClientFactoryMock.Object);

            // Act
            var action = () => service.SendSmsAsync("123", "Test message");

            // Assert
            await action.Should().ThrowAsync<System.Exception>()
                .WithMessage("*geçersiz*");
        }

        [Fact]
        public async Task SendSmsAsync_ShouldComplete_WhenDemoKeysAreUsed()
        {
            // Arrange
            var service = new SmsService(_httpClientFactoryMock.Object);

            // Act
            var action = () => service.SendSmsAsync("5551234567", "Demo SMS test");

            // Assert
            await action.Should().NotThrowAsync();
        }
    }
}
