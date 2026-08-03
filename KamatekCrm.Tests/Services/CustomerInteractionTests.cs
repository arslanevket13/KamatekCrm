using System;
using System.Threading.Tasks;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.CustomerInteractions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Services
{
    public class CustomerInteractionTests
    {
        [Theory]
        [InlineData("05321234567", "+905321234567")]
        [InlineData("5321234567", "+905321234567")]
        [InlineData("+905321234567", "+905321234567")]
        [InlineData("0 (532) 123 45 67", "+905321234567")]
        [InlineData("+14155552671", "+14155552671")]
        public void PhoneNormalization_ReturnsExpectedNormalizedFormat(string rawPhone, string expectedNormalized)
        {
            var result = PhoneNormalizationHelper.NormalizePhoneNumber(rawPhone);
            Assert.Equal(expectedNormalized, result);
        }

        [Fact]
        public async Task CreateInteraction_SucceedsAndCreatesHistory()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var factoryMock = new Mock<IDbContextFactory<AppDbContext>>();
            factoryMock.Setup(f => f.CreateDbContextAsync(default))
                .ReturnsAsync(() => new AppDbContext(options));

            var authMock = new Mock<IApplicationAuthorizationService>();
            authMock.Setup(a => a.Authorize(It.IsAny<ApplicationPermission>()))
                .Returns(Result.Success());

            var currentUserMock = new Mock<ICurrentUserContext>();
            currentUserMock.Setup(c => c.UserId).Returns(1);
            currentUserMock.Setup(c => c.Username).Returns("test_user");

            var commandService = new CustomerInteractionCommandService(factoryMock.Object, authMock.Object, currentUserMock.Object);

            var createDto = new CreateCustomerInteractionDto
            {
                CallerName = "Ahmet Yılmaz",
                CallerPhone = "05321234567",
                Subject = "Fiyat Talebi",
                Summary = "Yangın kapısı fiyat teklifi istendi.",
                RequestType = InteractionRequestType.PriceQuote,
                Priority = InteractionPriority.High,
                RequiresFollowUp = true,
                FollowUpDate = DateTime.UtcNow.AddDays(1)
            };

            var result = await commandService.CreateAsync(createDto);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.StartsWith("TALEP-", result.Value.InteractionNumber);
            Assert.Equal("+905321234567", result.Value.NormalizedPhone);
            Assert.Equal(InteractionStatus.Scheduled, result.Value.Status);

            await using var verifyContext = new AppDbContext(options);
            var count = await verifyContext.CustomerInteractions.CountAsync();
            Assert.Equal(1, count);

            var historyCount = await verifyContext.CustomerInteractionHistories.CountAsync();
            Assert.Equal(1, historyCount);
        }

        [Fact]
        public async Task FilterAsync_ReturnsMatchingInteractions()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            await using (var context = new AppDbContext(options))
            {
                context.CustomerInteractions.Add(new CustomerInteraction
                {
                    InteractionNumber = "TALEP-20260803-0001",
                    CallerName = "Mehmet Demir",
                    CallerPhone = "05429876543",
                    NormalizedPhone = "+905429876543",
                    Subject = "Keşif Randevusu",
                    Summary = "Ücretsiz keşif yapılmasını rica etti.",
                    RequestType = InteractionRequestType.Discovery,
                    Priority = InteractionPriority.Normal,
                    Status = InteractionStatus.New,
                    InteractionDate = DateTime.UtcNow,
                    CreatedByUserId = "1",
                    CreatedByUsername = "admin"
                });

                await context.SaveChangesAsync();
            }

            var factoryMock = new Mock<IDbContextFactory<AppDbContext>>();
            factoryMock.Setup(f => f.CreateDbContextAsync(default))
                .ReturnsAsync(() => new AppDbContext(options));

            var readService = new CustomerInteractionReadService(factoryMock.Object);
            var searchResult = await readService.FilterAsync(new CustomerInteractionFilterDto
            {
                SearchText = "Mehmet"
            });

            Assert.True(searchResult.IsSuccess);
            Assert.Single(searchResult.Value.Items);
            Assert.Equal("Mehmet Demir", searchResult.Value.Items[0].CallerName);
        }
    }
}
