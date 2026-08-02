using FluentAssertions;
using KamatekCrm.ApplicationCore.DTOs.Inventory;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace KamatekCrm.Tests.Services;

public sealed class StockCountServiceTests
{
    [Fact]
    public async Task ApplyAsync_UpdatesWarehouseAndReconcilesProductTotalInOneSession()
    {
        await using var fixture = await StockCountFixture.CreateAsync();
        var key = Guid.NewGuid();

        var result = await fixture.Command.ApplyAsync(new ApplyStockCountCommand(
            key, 1, DateTime.UtcNow, StockCountMode.FullWarehouse,
            [new StockCountLineCommand(fixture.ProductId, 5, 2)], "counter"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.WasAlreadyApplied.Should().BeFalse();
        await using var verify = fixture.CreateContext();
        (await verify.Inventories.SingleAsync(item => item.ProductId == fixture.ProductId && item.WarehouseId == 1))
            .Quantity.Should().Be(2);
        (await verify.Products.FindAsync(fixture.ProductId))!.TotalStockQuantity.Should().Be(5,
            "ürün toplamı iki depodaki 2 + 3 miktarından yeniden hesaplanmalıdır");
        (await verify.StockCountSessions.CountAsync()).Should().Be(1);
        (await verify.StockCountSessionItems.SingleAsync()).Difference.Should().Be(-3);
        var transaction = await verify.StockTransactions.SingleAsync(item => item.ReferenceId == result.Value.ReferenceNumber);
        transaction.TransactionType.Should().Be(StockTransactionType.AdjustmentMinus);
        transaction.UserId.Should().Be("counter");
    }

    [Fact]
    public async Task ApplyAsync_WithSameIdempotencyKey_DoesNotApplyCountTwice()
    {
        await using var fixture = await StockCountFixture.CreateAsync();
        var key = Guid.NewGuid();
        var command = new ApplyStockCountCommand(
            key, 1, DateTime.UtcNow, StockCountMode.FullWarehouse,
            [new StockCountLineCommand(fixture.ProductId, 5, 7)], "counter");

        var first = await fixture.Command.ApplyAsync(command);
        var second = await fixture.Command.ApplyAsync(command);

        first.IsSuccess.Should().BeTrue(first.Error);
        second.IsSuccess.Should().BeTrue(second.Error);
        second.Value!.WasAlreadyApplied.Should().BeTrue();
        second.Value.ReferenceNumber.Should().Be(first.Value!.ReferenceNumber);
        await using var verify = fixture.CreateContext();
        (await verify.Inventories.SingleAsync(item => item.ProductId == fixture.ProductId && item.WarehouseId == 1))
            .Quantity.Should().Be(7);
        (await verify.StockTransactions.CountAsync(item => item.ReferenceId == first.Value.ReferenceNumber)).Should().Be(1);
        (await verify.StockCountSessions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ApplyAsync_WithStaleSnapshot_WritesNothing()
    {
        await using var fixture = await StockCountFixture.CreateAsync();

        var result = await fixture.Command.ApplyAsync(new ApplyStockCountCommand(
            Guid.NewGuid(), 1, DateTime.UtcNow, StockCountMode.FullWarehouse,
            [new StockCountLineCommand(fixture.ProductId, 4, 2)], "counter"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("sayım sırasında değişti");
        await using var verify = fixture.CreateContext();
        (await verify.Inventories.SingleAsync(item => item.ProductId == fixture.ProductId && item.WarehouseId == 1))
            .Quantity.Should().Be(5);
        (await verify.StockCountSessions.CountAsync()).Should().Be(0);
        (await verify.StockTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ApplyAsync_WhenUnauthorized_DoesNotExposeOrMutateInventory()
    {
        await using var fixture = await StockCountFixture.CreateAsync(isAuthorized: false);

        var result = await fixture.Command.ApplyAsync(new ApplyStockCountCommand(
            Guid.NewGuid(), 1, DateTime.UtcNow, StockCountMode.FullWarehouse,
            [new StockCountLineCommand(fixture.ProductId, 5, 1)], "spoofed"));

        result.IsFailure.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        (await verify.Inventories.SingleAsync(item => item.ProductId == fixture.ProductId && item.WarehouseId == 1))
            .Quantity.Should().Be(5);
        (await verify.StockCountSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ReadService_ProjectsSnapshotSearchHistoryAndDetailsWithoutTracking()
    {
        await using var fixture = await StockCountFixture.CreateAsync();
        var applied = await fixture.Command.ApplyAsync(new ApplyStockCountCommand(
            Guid.NewGuid(), 1, DateTime.UtcNow, StockCountMode.Manual,
            [new StockCountLineCommand(fixture.ProductId, 5, 6)], "counter"));

        var warehouses = await fixture.Read.GetWarehousesAsync();
        var snapshot = await fixture.Read.GetWarehouseSnapshotAsync(1);
        var search = await fixture.Read.SearchProductsAsync(1, "kamera");
        var history = await fixture.Read.GetHistoryAsync();
        var appliedResult = applied.Value!;
        var detail = await fixture.Read.GetHistoryDetailAsync(
            history.Value!.Single(item => item.ReferenceNumber == appliedResult.ReferenceNumber).SessionId,
            appliedResult.ReferenceNumber);

        warehouses.Value.Should().Contain(item => item.Id == 1);
        snapshot.Value.Should().ContainSingle(item => item.ProductId == fixture.ProductId && item.SystemQuantity == 6);
        search.Value.Should().ContainSingle(item => item.ProductName == "Sayım Kamerası" && item.SystemQuantity == 6);
        history.Value.Should().ContainSingle(item => item.ReferenceNumber == appliedResult.ReferenceNumber && item.CountedBy == "counter");
        detail.Value.Should().ContainSingle(item => item.ProductName == "Sayım Kamerası" &&
                                                   item.SystemQuantity == 5 &&
                                                   item.CountedQuantity == 6 &&
                                                   item.Difference == 1);

        await using var verify = fixture.CreateContext();
        verify.ChangeTracker.Entries().Should().BeEmpty();
    }

    private sealed class StockCountFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        private StockCountFixture(
            SqliteConnection connection,
            DbContextOptions<AppDbContext> options,
            StockCountCommandService command,
            StockCountReadService read,
            int productId)
        {
            _connection = connection;
            _options = options;
            Command = command;
            Read = read;
            ProductId = productId;
        }

        public StockCountCommandService Command { get; }
        public StockCountReadService Read { get; }
        public int ProductId { get; }

        public static async Task<StockCountFixture> CreateAsync(bool isAuthorized = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            int productId;
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                var product = new Product
                {
                    ProductName = "Sayım Kamerası",
                    SKU = "COUNT-CAM",
                    Barcode = "869000000001",
                    Unit = "Adet",
                    PurchasePrice = 100m,
                    TotalStockQuantity = 999
                };
                seed.Products.Add(product);
                await seed.SaveChangesAsync();
                productId = product.Id;
                seed.Inventories.AddRange(
                    new Inventory { ProductId = productId, WarehouseId = 1, Quantity = 5 },
                    new Inventory { ProductId = productId, WarehouseId = 2, Quantity = 3 });
                await seed.SaveChangesAsync();
            }

            var factory = new Mock<IDbContextFactory<AppDbContext>>();
            factory.Setup(item => item.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new AppDbContext(options));
            var authorization = new TestAuthorizationService(isAuthorized);
            return new StockCountFixture(
                connection,
                options,
                new StockCountCommandService(factory.Object, authorization),
                new StockCountReadService(factory.Object, authorization),
                productId);
        }

        public AppDbContext CreateContext() => new(_options);

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }
}
