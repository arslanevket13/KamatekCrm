using FluentAssertions;
using KamatekCrm.ApplicationCore.DTOs.Quotes;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace KamatekCrm.Tests.Services;

public sealed class StandardQuoteServiceTests
{
    [Fact]
    public void PricingPolicy_CalculatesRoundedDiscountTaxCostAndProfitFromLines()
    {
        var result = StandardQuotePricingPolicy.Calculate([
            (1, 2, 100.005m, 40m, 10m, 20m),
            (2, 1, 50m, 10m, 0m, 10m)
        ]);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().BeEquivalentTo(new
        {
            SubTotal = 250.01m,
            TotalDiscount = 20m,
            NetTotal = 230.01m,
            TotalTax = 41m,
            GrandTotal = 271.01m,
            TotalCost = 90m,
            TotalProfit = 140.01m,
            ProfitMarginPercent = 155.57m
        });
    }

    [Fact]
    public async Task SaveAsync_UsesCatalogSnapshotsAndIsIdempotent()
    {
        await using var fixture = await StandardQuoteFixture.CreateAsync();
        var key = Guid.NewGuid();
        var command = fixture.CommandFor(key, fixture.ProductId, 2, 120m, 10m, QuoteStatus.Sent);

        var first = await fixture.Command.SaveAsync(command);
        var replay = await fixture.Command.SaveAsync(command);

        first.IsSuccess.Should().BeTrue(first.Error);
        replay.IsSuccess.Should().BeTrue(replay.Error);
        replay.Value!.WasAlreadyApplied.Should().BeTrue();
        replay.Value.QuoteId.Should().Be(first.Value!.QuoteId);
        first.Value.Pricing.Should().BeEquivalentTo(new
        {
            SubTotal = 240m,
            TotalDiscount = 24m,
            NetTotal = 216m,
            TotalTax = 43.20m,
            GrandTotal = 259.20m,
            TotalCost = 80m,
            TotalProfit = 136m
        });

        await using var verify = fixture.CreateContext();
        var quote = await verify.Quotes.Include(item => item.Lines).SingleAsync();
        quote.Status.Should().Be(QuoteStatus.Sent);
        quote.Currency.Should().Be("TRY");
        quote.Lines.Should().ContainSingle();
        var line = quote.Lines.Single();
        line.ProductName.Should().Be("Standart Kamera");
        line.ProductCode.Should().Be("STD-CAM");
        line.PurchasePrice.Should().Be(40m);
        line.TaxPercent.Should().Be(20m);
        line.LineTotal.Should().Be(259.20m);
        (await verify.ActivityLogs.CountAsync(log => log.ReferenceId == $"STANDARD-QUOTE-SAVE:{key:N}"))
            .Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_RejectsUnsupportedCurrencyAndDuplicateProductsWithoutWriting()
    {
        await using var fixture = await StandardQuoteFixture.CreateAsync();
        var unsupported = fixture.CommandFor(
            Guid.NewGuid(), fixture.ProductId, 1, 100m, 0m, QuoteStatus.Draft) with { Currency = "USD" };
        var line = unsupported.Lines[0] with { };
        var duplicate = unsupported with
        {
            IdempotencyKey = Guid.NewGuid(),
            Currency = "TRY",
            Lines = [line, line]
        };

        (await fixture.Command.SaveAsync(unsupported)).IsFailure.Should().BeTrue();
        (await fixture.Command.SaveAsync(duplicate)).IsFailure.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        (await verify.Quotes.CountAsync()).Should().Be(0);
        (await verify.QuoteLines.CountAsync()).Should().Be(0);
        (await verify.ActivityLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SaveAsync_WhenProductDisappeared_RollsBackQuoteAndAudit()
    {
        await using var fixture = await StandardQuoteFixture.CreateAsync();
        var operation = Guid.NewGuid();

        var result = await fixture.Command.SaveAsync(fixture.CommandFor(
            operation, 999_999, 1, 100m, 0m, QuoteStatus.Draft));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("ürün kataloğunda");
        await using var verify = fixture.CreateContext();
        (await verify.Quotes.CountAsync()).Should().Be(0);
        (await verify.ActivityLogs.AnyAsync(log => log.ReferenceId == $"STANDARD-QUOTE-SAVE:{operation:N}"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task ReadService_ProjectsWorkspaceSearchAndSavedDocument()
    {
        await using var fixture = await StandardQuoteFixture.CreateAsync();
        var saved = await fixture.Command.SaveAsync(fixture.CommandFor(
            Guid.NewGuid(), fixture.ProductId, 1, 100m, 0m, QuoteStatus.Draft));

        var workspace = await fixture.Read.GetWorkspaceAsync();
        var search = await fixture.Read.SearchProductsAsync("kamera");
        var document = await fixture.Read.GetDocumentAsync(saved.Value!.QuoteId);

        workspace.Value!.Customers.Should().ContainSingle(customer => customer.FullName == "Standart Müşteri");
        search.Value.Should().ContainSingle(product =>
            product.Id == fixture.ProductId && product.PurchasePrice == 40m && product.TaxPercent == 20m);
        document.Value!.Quote.Customer!.FullName.Should().Be("Standart Müşteri");
        document.Value.Lines.Should().ContainSingle(line => line.ProductName == "Standart Kamera");
    }

    [Fact]
    public async Task Services_WhenUnauthorized_DoNotExposeOrWriteStandardQuotes()
    {
        await using var fixture = await StandardQuoteFixture.CreateAsync(isAuthorized: false);

        var workspace = await fixture.Read.GetWorkspaceAsync();
        var save = await fixture.Command.SaveAsync(fixture.CommandFor(
            Guid.NewGuid(), fixture.ProductId, 1, 100m, 0m, QuoteStatus.Draft));

        workspace.IsFailure.Should().BeTrue();
        save.IsFailure.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        (await verify.Quotes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SaveAsync_FromDiscoveryLinksQuoteHistoryAndPreventsSecondQuote()
    {
        await using var fixture = await StandardQuoteFixture.CreateAsync();
        int jobId;
        await using (var arrange = fixture.CreateContext())
        {
            var seededJob = new ServiceJob
            {
                CustomerId = fixture.CustomerId,
                Title = "Keşif",
                Description = "Teklife dönüşecek keşif",
                WorkOrderType = WorkOrderType.Discovery,
                Status = JobStatus.Quoting,
                IsConvertedToQuote = true,
                CreatedBy = "test"
            };
            arrange.ServiceJobs.Add(seededJob);
            await arrange.SaveChangesAsync();
            jobId = seededJob.Id;
        }
        var command = fixture.CommandFor(
            Guid.NewGuid(), fixture.ProductId, 1, 100m, 0m, QuoteStatus.Sent) with
        {
            SourceServiceJobId = jobId
        };

        var first = await fixture.Command.SaveAsync(command);
        var second = await fixture.Command.SaveAsync(command with { IdempotencyKey = Guid.NewGuid() });

        first.IsSuccess.Should().BeTrue(first.Error);
        second.IsFailure.Should().BeTrue();
        second.Error.Should().Contain("zaten kaydedilmiş");
        await using var verify = fixture.CreateContext();
        var verifiedJob = await verify.ServiceJobs.SingleAsync();
        verifiedJob.ProposalNotes.Should().Contain(first.Value!.QuoteNumber);
        verifiedJob.ProposalSentDate.Should().NotBeNull();
        (await verify.ServiceJobHistories.CountAsync(history =>
            history.ServiceJobId == jobId && history.Action == "StandardQuoteSaved")).Should().Be(1);
        (await verify.Quotes.CountAsync()).Should().Be(1);
    }

    private sealed class StandardQuoteFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        private StandardQuoteFixture(
            SqliteConnection connection,
            DbContextOptions<AppDbContext> options,
            StandardQuoteCommandService command,
            StandardQuoteReadService read,
            int customerId,
            int productId)
        {
            _connection = connection;
            _options = options;
            Command = command;
            Read = read;
            CustomerId = customerId;
            ProductId = productId;
        }

        public StandardQuoteCommandService Command { get; }
        public StandardQuoteReadService Read { get; }
        public int CustomerId { get; }
        public int ProductId { get; }

        public static async Task<StandardQuoteFixture> CreateAsync(bool isAuthorized = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            int customerId;
            int productId;
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                var customer = new Customer
                {
                    CustomerCode = "STD-001",
                    FullName = "Standart Müşteri",
                    PhoneNumber = "5551112233",
                    City = "İstanbul"
                };
                var product = new Product
                {
                    ProductName = "Standart Kamera",
                    SKU = "STD-CAM",
                    Barcode = "869000000088",
                    Unit = "Adet",
                    SalePrice = 100m,
                    PurchasePrice = 40m,
                    VatRate = 20,
                    TotalStockQuantity = 8
                };
                seed.AddRange(customer, product);
                await seed.SaveChangesAsync();
                customerId = customer.Id;
                productId = product.Id;
            }

            var factory = new Mock<IDbContextFactory<AppDbContext>>();
            factory.Setup(item => item.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new AppDbContext(options));
            var authorization = new TestAuthorizationService(isAuthorized);
            var user = new TestCurrentUserContext();
            return new StandardQuoteFixture(
                connection, options,
                new StandardQuoteCommandService(factory.Object, authorization, user),
                new StandardQuoteReadService(factory.Object, authorization),
                customerId, productId);
        }

        public SaveStandardQuoteCommand CommandFor(
            Guid key,
            int productId,
            int quantity,
            decimal unitPrice,
            decimal discount,
            QuoteStatus status) => new(
                key,
                CustomerId,
                DateTime.Today,
                DateTime.Today.AddDays(15),
                "TRY",
                "Test koşulları",
                status,
                [new StandardQuoteLineInput(productId, quantity, unitPrice, discount)]);

        public AppDbContext CreateContext() => new(_options);
        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }

    private sealed class TestAuthorizationService(bool allowed) : IApplicationAuthorizationService
    {
        public bool IsAuthorized(ApplicationPermission permission) => allowed;
        public KamatekCrm.ApplicationCore.Common.Result Authorize(ApplicationPermission permission) =>
            allowed
                ? KamatekCrm.ApplicationCore.Common.Result.Success()
                : KamatekCrm.ApplicationCore.Common.Result.Failure("Yetkisiz işlem.");
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public bool IsAuthenticated => true;
        public int? UserId => 11;
        public string Username => "standard-quote-user";
        public string Role => "Personel";
        public bool HasPermission(ApplicationPermission permission) => true;
    }
}
