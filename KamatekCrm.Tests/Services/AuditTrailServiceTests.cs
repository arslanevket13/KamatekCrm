using FluentAssertions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace KamatekCrm.Tests.Services;

public sealed class AuditTrailServiceTests
{
    [Fact]
    public async Task WriteAsync_StoresAuthenticatedActorAndIntegritySeal()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(item => item.CreateDbContextAsync(default)).ReturnsAsync(() => new AppDbContext(options));
        var service = new AuditTrailService(factory.Object, new CurrentUserStub());

        var result = await service.WriteAsync(AuditActionType.View, "CustomerDocument", "42", "Belge görüntülendi");

        result.IsSuccess.Should().BeTrue(result.Error);
        await using var verify = new AppDbContext(options);
        var entry = await verify.ActivityLogs.SingleAsync();
        entry.UserId.Should().Be(7);
        entry.Username.Should().Be("audit-user");
        entry.EntityName.Should().Be("CustomerDocument");
        entry.RecordId.Should().Be("42");
        entry.IntegrityHash.Should().HaveLength(64);
        entry.IntegrityVersion.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WriteAsync_WhenPersistenceFails_ReturnsVisibleFailure()
    {
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(item => item.CreateDbContextAsync(default)).ThrowsAsync(new InvalidOperationException("db unavailable"));
        var service = new AuditTrailService(factory.Object, new CurrentUserStub());

        var result = await service.WriteAsync(AuditActionType.View, "Report");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("db unavailable");
    }

    private sealed class CurrentUserStub : ICurrentUserContext
    {
        public bool IsAuthenticated => true;
        public int? UserId => 7;
        public string Username => "audit-user";
        public string Role => "Admin";
        public bool HasPermission(ApplicationPermission permission) => true;
    }
}
