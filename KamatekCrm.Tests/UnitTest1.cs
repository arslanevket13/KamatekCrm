using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KamatekCrm.Tests;

public class UnitTest1
{
    [Fact]
    public void VerifyAllXamlSymbolsAreValid()
    {
        var validSymbols = new HashSet<string>(Enum.GetNames(typeof(Wpf.Ui.Controls.SymbolRegular)));
        var viewsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "KamatekCRM", "Views");
        if (!System.IO.Directory.Exists(viewsDir))
        {
            viewsDir = @"C:\Antigravity\KamatekCRM\Views";
        }

        var xamlFiles = System.IO.Directory.GetFiles(viewsDir, "*.xaml", System.IO.SearchOption.AllDirectories);
        var invalidSymbols = new List<string>();

        var symbolAttrRegex = new System.Text.RegularExpressions.Regex(@"Symbol=""([A-Za-z0-9]+)""");
        var iconMarkupRegex = new System.Text.RegularExpressions.Regex(@"\{ui:SymbolIcon\s+([A-Za-z0-9]+)\}");

        foreach (var file in xamlFiles)
        {
            var content = System.IO.File.ReadAllText(file);
            foreach (System.Text.RegularExpressions.Match match in symbolAttrRegex.Matches(content))
            {
                var symbol = match.Groups[1].Value;
                if (!validSymbols.Contains(symbol))
                {
                    invalidSymbols.Add($"{System.IO.Path.GetFileName(file)}: '{symbol}'");
                }
            }
        }

        Assert.Empty(invalidSymbols);
    }
    [Fact]
    public async Task ExecutionStrategy_BeginTransactionAsync_WithCreateExecutionStrategy_SucceedsWithoutExecutionStrategyException()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<KamatekCrm.Infrastructure.Data.AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=test_db;Username=test;Password=test",
                npgsql => npgsql.EnableRetryOnFailure())
            .Options;
        await using var context = new KamatekCrm.Infrastructure.Data.AppDbContext(options);

        // Helper method that uses CreateExecutionStrategy
        async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginTx(KamatekCrm.Infrastructure.Data.AppDbContext ctx)
        {
            var strategy = ctx.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () => await ctx.Database.BeginTransactionAsync());
        }

        var ex = await Record.ExceptionAsync(async () =>
        {
            await BeginTx(context);
        });

        // Ensure ex is NOT InvalidOperationException about NpgsqlRetryingExecutionStrategy!
        if (ex is InvalidOperationException invEx)
        {
            Assert.DoesNotContain("NpgsqlRetryingExecutionStrategy", invEx.Message);
        }
    }
}