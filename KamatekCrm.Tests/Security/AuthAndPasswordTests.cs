using System;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Services;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Security
{
    public class AuthAndPasswordTests
    {
        private static IDbContextFactory<AppDbContext> CreateInMemoryDbContextFactory(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var factoryMock = new Mock<IDbContextFactory<AppDbContext>>();
            factoryMock.Setup(f => f.CreateDbContext()).Returns(() => new AppDbContext(options));
            factoryMock.Setup(f => f.CreateDbContextAsync(default)).ReturnsAsync(() => new AppDbContext(options));
            return factoryMock.Object;
        }

        [Theory]
        [InlineData(null, "en az 10 karakter")]
        [InlineData("", "en az 10 karakter")]
        [InlineData("Short1!", "en az 10 karakter")]
        [InlineData("alllowercase1!", "en az bir büyük harf")]
        [InlineData("ALLUPPERCASE1!", "en az bir küçük harf")]
        [InlineData("NoDigitsHere!", "en az bir rakam")]
        [InlineData("NoSpecialChars123", "en az bir özel karakter")]
        public void PasswordPolicy_Validate_ShouldReturnError_WhenPasswordDoesNotMeetRequirements(string? password, string expectedErrorSubstring)
        {
            // Act
            var error = PasswordPolicy.Validate(password);

            // Assert
            error.Should().NotBeNull();
            error.Should().Contain(expectedErrorSubstring);
        }

        [Fact]
        public void PasswordPolicy_Validate_ShouldReturnNull_WhenPasswordIsStrong()
        {
            // Arrange
            var validPassword = "SecurePassword123!";

            // Act
            var error = PasswordPolicy.Validate(validPassword);

            // Assert
            error.Should().BeNull();
        }

        [Fact]
        public void PasswordPolicy_GenerateTemporaryPassword_ShouldProduceValidStrongPassword()
        {
            // Act
            var tempPassword = PasswordPolicy.GenerateTemporaryPassword(16);

            // Assert
            tempPassword.Should().HaveLength(16);
            PasswordPolicy.Validate(tempPassword).Should().BeNull();
        }

        [Fact]
        public void AuthService_HashPassword_ShouldProduceValidBcryptHash()
        {
            // Arrange
            var password = "MyTestPassword123*";

            // Act
            var hash = AuthService.HashPassword(password);

            // Assert
            hash.Should().StartWith("$2");
            BCrypt.Net.BCrypt.Verify(password, hash).Should().BeTrue();
            BCrypt.Net.BCrypt.Verify("WrongPassword123*", hash).Should().BeFalse();
        }

        [Fact]
        public async Task AuthService_LoginAsync_ShouldSucceed_WithValidCredentials()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var factory = CreateInMemoryDbContextFactory(dbName);
            var authService = new AuthService(factory);

            var password = "AdminPassword123!";
            using (var db = factory.CreateDbContext())
            {
                db.Users.Add(new User
                {
                    Username = "adminuser",
                    PasswordHash = AuthService.HashPassword(password),
                    Role = "Admin",
                    IsActive = true
                });
                await db.SaveChangesAsync();
            }

            // Act
            var result = await authService.LoginAsync("adminuser", password);

            // Assert
            result.Should().BeTrue();
            authService.IsLoggedIn.Should().BeTrue();
            authService.CurrentUser?.Username.Should().Be("adminuser");
        }

        [Fact]
        public async Task AuthService_LoginAsync_ShouldThrowUnauthorized_WithWrongPassword()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var factory = CreateInMemoryDbContextFactory(dbName);
            var authService = new AuthService(factory);

            using (var db = factory.CreateDbContext())
            {
                db.Users.Add(new User
                {
                    Username = "testuser",
                    PasswordHash = AuthService.HashPassword("CorrectPassword123!"),
                    Role = "Technician",
                    IsActive = true
                });
                await db.SaveChangesAsync();
            }

            // Act
            Func<Task> act = async () => await authService.LoginAsync("testuser", "WrongPassword999!");

            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("*Kullanıcı adı veya parola hatalı*");
        }

        [Fact]
        public async Task AuthService_LoginAsync_ShouldLockAccount_AfterMaxFailedAttempts()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var factory = CreateInMemoryDbContextFactory(dbName);
            var authService = new AuthService(factory);
            var username = $"bruteforce_{Guid.NewGuid():N}";

            using (var db = factory.CreateDbContext())
            {
                db.Users.Add(new User
                {
                    Username = username,
                    PasswordHash = AuthService.HashPassword("ValidPassword123!"),
                    Role = "Technician",
                    IsActive = true
                });
                await db.SaveChangesAsync();
            }

            // Act: 4 kez başarısız giriş yap
            for (int i = 0; i < 4; i++)
            {
                Func<Task> actFail = async () => await authService.LoginAsync(username, "BadPassword!");
                await actFail.Should().ThrowAsync<UnauthorizedAccessException>();
            }

            // 5. denemede hesap kilitlenmeli (InvalidOperationException)
            Func<Task> actFifth = async () => await authService.LoginAsync(username, "BadPassword!");
            await actFifth.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*kilitlendi*");

            // Kilitliyken doğru şifre girilse dahi kilit devam etmeli
            Func<Task> actWhileLocked = async () => await authService.LoginAsync(username, "ValidPassword123!");
            await actWhileLocked.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*kilitlendi*");
        }
    }
}
