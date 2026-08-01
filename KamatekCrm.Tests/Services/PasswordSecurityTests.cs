using System;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Services;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Services;

public class PasswordSecurityTests
{
    [Theory]
    [InlineData("short")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoNumberHere!")]
    [InlineData("NoSpecial123")]
    public void PasswordPolicy_RejectsWeakPasswords(string password)
    {
        PasswordPolicy.Validate(password).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PasswordReset_HashesPasswordBeforePersisting()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var user = new User
        {
            Id = 42,
            Username = "test",
            Ad = "Test",
            Soyad = "User",
            PasswordHash = "old",
            MustChangePassword = true
        };

        await using (var seed = new AppDbContext(options))
        {
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
        }

        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(default)).ReturnsAsync(() => new AppDbContext(options));
        var authService = new Mock<IAuthService>();
        authService.SetupGet(service => service.CurrentUser).Returns(user);
        var viewModel = new PasswordResetViewModel(user, authService.Object, factory.Object)
        {
            NewPassword = "StrongPass1!",
            ConfirmPassword = "StrongPass1!"
        };

        await viewModel.SaveCommand.ExecuteAsync(null);

        await using var verify = new AppDbContext(options);
        var stored = await verify.Users.SingleAsync(u => u.Id == user.Id);
        stored.PasswordHash.Should().NotBe("StrongPass1!");
        BCrypt.Net.BCrypt.Verify("StrongPass1!", stored.PasswordHash).Should().BeTrue();
        stored.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public void GenerateTemporaryPassword_AlwaysMeetsStrongPasswordPolicy()
    {
        var passwords = Enumerable.Range(0, 25)
            .Select(_ => PasswordPolicy.GenerateTemporaryPassword())
            .ToList();

        passwords.Should().OnlyHaveUniqueItems();
        passwords.Should().OnlyContain(password => PasswordPolicy.Validate(password) == null);
    }
}
