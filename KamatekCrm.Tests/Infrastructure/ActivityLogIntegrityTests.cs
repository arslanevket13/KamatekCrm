using FluentAssertions;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Tests.Infrastructure;

public class ActivityLogIntegrityTests
{
    [Fact]
    public async Task SaveChangesAsync_SealsNewLog_AndNormalizesLegacyAliases()
    {
        await using var context = CreateContext();
        var log = new ActivityLog
        {
            ActionType = "Update",
            RecordId = "42",
            Description = "Müşteri güncellendi",
            Timestamp = DateTime.Now
        };

        context.ActivityLogs.Add(log);
        await context.SaveChangesAsync();

        log.Action.Should().Be("Update");
        log.ReferenceId.Should().Be("42");
        log.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        log.IntegrityVersion.Should().Be(ActivityLogIntegrity.CurrentVersion);
        log.IntegrityHash.Should().MatchRegex("^[0-9A-F]{64}$");
        ActivityLogIntegrity.Verify(log).Should().BeTrue();
    }

    [Fact]
    public async Task Verify_ReturnsFalse_WhenSealedContentIsChanged()
    {
        await using var context = CreateContext();
        var log = new ActivityLog { Action = "Login", Timestamp = DateTime.UtcNow };
        context.ActivityLogs.Add(log);
        await context.SaveChangesAsync();

        log.Description = "sonradan değiştirildi";

        ActivityLogIntegrity.Verify(log).Should().BeFalse();
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task SaveChangesAsync_RejectsMutationOfExistingLog(EntityState state)
    {
        var databaseName = $"audit-integrity-{Guid.NewGuid():N}";
        await using (var setup = CreateContext(databaseName))
        {
            setup.ActivityLogs.Add(new ActivityLog { Action = "Create", Timestamp = DateTime.UtcNow });
            await setup.SaveChangesAsync();
        }

        await using var context = CreateContext(databaseName);
        var log = await context.ActivityLogs.SingleAsync();
        context.Entry(log).State = state;

        var action = async () => await context.SaveChangesAsync();

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Denetim kayıtları değiştirilemez*");
    }

    private static AppDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"audit-integrity-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
