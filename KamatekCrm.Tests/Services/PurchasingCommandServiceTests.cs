using FluentAssertions;
using KamatekCrm.ApplicationCore.DTOs.Transactions;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Tests.Services;

public sealed class PurchasingCommandServiceTests
{
    [Fact]
    public async Task ImmediateReceipt_IsIdempotent_AndUpdatesStockWacSupplierAndPaymentOnce()
    {
        await using var fixture = await PurchaseFixture.CreateAsync();
        var service = fixture.CreateService();
        var key = Guid.NewGuid().ToString();
        var command = CreateCommand(key, receive: true, [new PaymentAllocationInput(PaymentMethod.OnAccount, 500m)]);

        var first = await service.CreatePurchaseAsync(command);
        var second = await service.CreatePurchaseAsync(command);

        first.IsSuccess.Should().BeTrue(first.Error);
        second.Value!.WasAlreadyProcessed.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        (await verify.PurchaseOrders.CountAsync()).Should().Be(1);
        (await verify.Inventories.SingleAsync(item => item.ProductId == 100 && item.WarehouseId == 1)).Quantity.Should().Be(5);
        (await verify.Inventories.SingleAsync(item => item.ProductId == 100 && item.WarehouseId == 1)).AverageCost.Should().Be(100m);
        (await verify.Products.SingleAsync(item => item.Id == 100)).TotalStockQuantity.Should().Be(5);
        (await verify.Products.SingleAsync(item => item.Id == 100)).AverageCost.Should().Be(100m);
        (await verify.Suppliers.SingleAsync(item => item.Id == 300)).Balance.Should().Be(500m);
        (await verify.PurchaseOrderPayments.CountAsync()).Should().Be(1);
        (await verify.StockTransactions.CountAsync(item => item.TransactionType == StockTransactionType.Purchase)).Should().Be(1);
    }

    [Fact]
    public async Task ImmediateReceipt_WhenSettlementIsInvalid_RollsBackNewProductOrderAndAllLedgers()
    {
        await using var fixture = await PurchaseFixture.CreateAsync();
        var service = fixture.CreateService();
        var command = new CreatePurchaseCommand(
            300, "INV-ROLLBACK", DateTime.UtcNow,
            [new PurchaseLineInput(null, "Yeni Ürün", 2, 100m, 0m, 0m, 200m, "NEW-1", "", "Adet")],
            null, "test", Guid.NewGuid().ToString(), true, 1,
            [new PaymentAllocationInput(PaymentMethod.Cash, 100m)]);

        var result = await service.CreatePurchaseAsync(command);

        result.IsFailure.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        (await verify.Products.CountAsync(item => item.SKU == "NEW-1")).Should().Be(0);
        (await verify.PurchaseOrders.CountAsync()).Should().Be(0);
        (await verify.StockTransactions.CountAsync()).Should().Be(0);
        (await verify.CashTransactions.CountAsync()).Should().Be(0);
        (await verify.Suppliers.SingleAsync(item => item.Id == 300)).Balance.Should().Be(0m);
    }

    [Fact]
    public async Task PurchaseReturn_WithInsufficientStock_WritesNothing()
    {
        await using var fixture = await PurchaseFixture.CreateAsync();
        var service = fixture.CreateService();
        var receipt = await service.CreatePurchaseAsync(CreateCommand(Guid.NewGuid().ToString(), true, [new PaymentAllocationInput(PaymentMethod.OnAccount, 500m)]));
        await using (var mutate = fixture.CreateContext())
        {
            var inventory = await mutate.Inventories.SingleAsync(item => item.ProductId == 100 && item.WarehouseId == 1);
            inventory.Quantity = 1;
            await mutate.SaveChangesAsync();
        }
        await using var lookup = fixture.CreateContext();
        var itemId = await lookup.PurchaseOrderItems.Select(item => item.Id).SingleAsync();

        var result = await service.ReturnPurchaseAsync(new ReturnPurchaseCommand(
            receipt.Value!.PurchaseOrderId,
            [new PurchaseReturnLineInput(itemId, 2, 1)], PaymentMethod.OnAccount, null,
            "Stok yetersizliği testi", null, "test", Guid.NewGuid().ToString()));

        result.IsFailure.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        (await verify.PurchaseReturns.CountAsync()).Should().Be(0);
        (await verify.StockTransactions.CountAsync(item => item.TransactionType == StockTransactionType.ReturnToSupplier)).Should().Be(0);
        (await verify.Suppliers.SingleAsync(item => item.Id == 300)).Balance.Should().Be(500m);
    }

    [Fact]
    public async Task PartialThenFullPurchaseReturn_ReconcilesStockCashSupplierAndStatus()
    {
        await using var fixture = await PurchaseFixture.CreateAsync();
        var service = fixture.CreateService();
        var receipt = await service.CreatePurchaseAsync(CreateCommand(Guid.NewGuid().ToString(), true,
            [new PaymentAllocationInput(PaymentMethod.Cash, 300m), new PaymentAllocationInput(PaymentMethod.OnAccount, 200m)]));
        await using var lookup = fixture.CreateContext();
        var itemId = await lookup.PurchaseOrderItems.Select(item => item.Id).SingleAsync();

        var first = await service.ReturnPurchaseAsync(new ReturnPurchaseCommand(
            receipt.Value!.PurchaseOrderId,
            [new PurchaseReturnLineInput(itemId, 3, 1)], PaymentMethod.Cash, "POS-REF-1",
            "Fazla teslimat", null, "test", Guid.NewGuid().ToString()));
        var second = await service.ReturnPurchaseAsync(new ReturnPurchaseCommand(
            receipt.Value.PurchaseOrderId,
            [new PurchaseReturnLineInput(itemId, 2, 1)], PaymentMethod.OnAccount, null,
            "Kalan ürünler", null, "test", Guid.NewGuid().ToString()));

        first.IsSuccess.Should().BeTrue(first.Error);
        second.IsSuccess.Should().BeTrue(second.Error);
        await using var verify = fixture.CreateContext();
        (await verify.PurchaseOrders.SingleAsync()).Status.Should().Be(PurchaseStatus.Returned);
        (await verify.Inventories.SingleAsync(item => item.ProductId == 100 && item.WarehouseId == 1)).Quantity.Should().Be(0);
        (await verify.Products.SingleAsync(item => item.Id == 100)).TotalStockQuantity.Should().Be(0);
        (await verify.Products.SingleAsync(item => item.Id == 100)).AverageCost.Should().Be(0m);
        (await verify.Suppliers.SingleAsync(item => item.Id == 300)).Balance.Should().Be(0m);
        (await verify.CashTransactions.Where(item => item.TransactionType == CashTransactionType.CashIncome).SumAsync(item => item.Amount)).Should().Be(300m);
    }

    [Fact]
    public async Task PendingCancellation_HasNoStockCashOrSupplierSideEffect()
    {
        await using var fixture = await PurchaseFixture.CreateAsync();
        var service = fixture.CreateService();
        var created = await service.CreatePurchaseAsync(CreateCommand(Guid.NewGuid().ToString(), receive: false, null));

        var result = await service.CancelPurchaseAsync(new CancelPurchaseCommand(created.Value!.PurchaseOrderId, "Sipariş iptali", "test"));

        result.IsSuccess.Should().BeTrue(result.Error);
        await using var verify = fixture.CreateContext();
        (await verify.PurchaseOrders.SingleAsync()).Status.Should().Be(PurchaseStatus.Cancelled);
        (await verify.Inventories.CountAsync()).Should().Be(0);
        (await verify.CashTransactions.CountAsync()).Should().Be(0);
        (await verify.Suppliers.SingleAsync(item => item.Id == 300)).Balance.Should().Be(0m);
    }

    [Fact]
    public async Task UnauthorizedReceiveAndCancel_AreRejectedWithoutWriting()
    {
        await using var fixture = await PurchaseFixture.CreateAsync();
        var created = await fixture.CreateService().CreatePurchaseAsync(CreateCommand(Guid.NewGuid().ToString(), receive: false, null));
        var unauthorized = fixture.CreateService(isAuthorized: false);

        var receive = await unauthorized.ReceivePurchaseAsync(new ReceivePurchaseCommand(
            created.Value!.PurchaseOrderId, 1, [new PaymentAllocationInput(PaymentMethod.Cash, 500m)], "test", Guid.NewGuid().ToString()));
        var cancel = await unauthorized.CancelPurchaseAsync(new CancelPurchaseCommand(created.Value.PurchaseOrderId, "Yetkisiz", "test"));

        receive.IsFailure.Should().BeTrue();
        cancel.IsFailure.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        (await verify.PurchaseOrders.SingleAsync()).Status.Should().Be(PurchaseStatus.Pending);
        (await verify.Inventories.CountAsync()).Should().Be(0);
        (await verify.CashTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ReceivedPurchase_CannotBeDirectlyCancelled()
    {
        await using var fixture = await PurchaseFixture.CreateAsync();
        var service = fixture.CreateService();
        var received = await service.CreatePurchaseAsync(CreateCommand(Guid.NewGuid().ToString(), true,
            [new PaymentAllocationInput(PaymentMethod.OnAccount, 500m)]));

        var result = await service.CancelPurchaseAsync(new CancelPurchaseCommand(received.Value!.PurchaseOrderId, "Yanlış iptal", "test"));

        result.IsFailure.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        (await verify.PurchaseOrders.SingleAsync()).Status.Should().Be(PurchaseStatus.Received);
        (await verify.Inventories.SingleAsync()).Quantity.Should().Be(5);
        (await verify.Suppliers.SingleAsync()).Balance.Should().Be(500m);
    }

    private static CreatePurchaseCommand CreateCommand(string key, bool receive, IReadOnlyCollection<PaymentAllocationInput>? settlements) =>
        new(300, "INV-TEST", DateTime.UtcNow,
            [new PurchaseLineInput(100, "Test Ürün", 5, 100m, 0m, 0m, 500m)],
            null, "test", key, receive, receive ? 1 : null, settlements);

    private sealed class PurchaseFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        private PurchaseFixture(SqliteConnection connection, DbContextOptions<AppDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<PurchaseFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var fixture = new PurchaseFixture(connection, options);
            await using var context = fixture.CreateContext();
            await context.Database.EnsureCreatedAsync();
            context.Products.Add(new Product { Id = 100, ProductName = "Test Ürün", SKU = "TEST-100", Barcode = "100", Unit = "Adet" });
            context.Suppliers.Add(new Supplier { Id = 300, Name = "Test", CompanyName = "Test Tedarikçi", IsActive = true });
            await context.SaveChangesAsync();
            return fixture;
        }

        public AppDbContext CreateContext() => new(_options);

        public PurchasingCommandService CreateService(bool isAuthorized = true) =>
            new(new DelegateFactory(_options), new TestAuthorizationService(isAuthorized));

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }

    private sealed class DelegateFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}
