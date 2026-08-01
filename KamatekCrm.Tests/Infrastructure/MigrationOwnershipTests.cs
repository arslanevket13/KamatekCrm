using FluentAssertions;
using KamatekCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Tests.Infrastructure;

public class MigrationOwnershipTests
{
    [Fact]
    public void InfrastructureAssembly_OwnsTheCompleteOrderedMigrationChain()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=migration_contract_test;Username=test;Password=test",
                postgres => postgres.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;
        using var context = new AppDbContext(options);

        context.Database.GetMigrations().Should().Equal(
            "20260409193923_InitialCreate",
            "20260419195646_AddActivityLogsTable",
            "20260717111304_RefactorSpecsToJsonb",
            "20260723205112_MakeServiceJobDatesNullable",
            "20260726222204_AddDiscoveryFieldsToServiceJob",
            "20260801185833_AddSalesIdempotency",
            "20260801205000_ReconcileQuoteAndUserSchema",
            "20260801210000_EnforceTemporaryPasswordChange",
            "20260801220000_SealAuditLogEntries");

        context.Database.HasPendingModelChanges().Should().BeFalse(
            "çalışma modeli ile Infrastructure model snapshot'ı aynı olmalıdır");
    }
}
