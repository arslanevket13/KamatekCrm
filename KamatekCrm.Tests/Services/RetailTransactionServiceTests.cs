using FluentAssertions;
using KamatekCrm.ApplicationCore.DTOs.Transactions;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Tests.Services;

public sealed class RetailTransactionServiceTests
{
    [Fact]
    public async Task CompleteSale_WithSameIdempotencyKey_ChangesEveryLedgerOnlyOnce()
    {
        await using var fixture = await RetailFixture.CreateAsync(stock: 10);
        var service = fixture.CreateService();
        var key = Guid.NewGuid().ToString();
        var command = SaleCommand(key, quantity: 2, PaymentMethod.Cash);

        var first = await service.CompleteSaleAsync(command);
        var second = await service.CompleteSaleAsync(command);

        first.IsSuccess.Should().BeTrue(first.Error);
        second.IsSuccess.Should().BeTrue(second.Error);
        second.Value!.WasAlreadyProcessed.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        (await verify.SalesOrders.CountAsync()).Should().Be(1);
        (await verify.Inventories.SingleAsync(item => item.ProductId == 100)).Quantity.Should().Be(8);
        (await verify.Products.SingleAsync(item => item.Id == 100)).TotalStockQuantity.Should().Be(8);
        (await verify.CashTransactions.CountAsync()).Should().Be(1);
        (await verify.StockTransactions.CountAsync(item => item.TransactionType == StockTransactionType.Sale)).Should().Be(1);
    }

    [Fact]
    public async Task CompleteSale_OnAccount_CreatesDebtWithoutCashOrCollection()
    {
        await using var fixture = await RetailFixture.CreateAsync(stock: 10, withCustomer: true);
        var service = fixture.CreateService();

        var result = await service.CompleteSaleAsync(SaleCommand(Guid.NewGuid().ToString(), 1, PaymentMethod.OnAccount, customerId: 200));

        result.IsSuccess.Should().BeTrue(result.Error);
        await using var verify = fixture.CreateContext();
        (await verify.CashTransactions.CountAsync()).Should().Be(0);
        var ledger = await verify.Transactions.Where(item => item.CustomerId == 200).ToListAsync();
        ledger.Should().ContainSingle(item => item.Type == TransactionType.Debt && item.Amount == 100m);
        ledger.Should().NotContain(item => item.Type == TransactionType.Payment);
    }

    [Fact]
    public async Task PartialThenFullReturn_ReconcilesStockCashCustomerLoyaltyAndStatus()
    {
        await using var fixture = await RetailFixture.CreateAsync(stock: 10, withCustomer: true);
        var service = fixture.CreateService();
        var sale = await service.CompleteSaleAsync(SaleCommand(Guid.NewGuid().ToString(), 2, PaymentMethod.Cash, customerId: 200));
        sale.IsSuccess.Should().BeTrue(sale.Error);
        var saleId = sale.Value!.SalesOrderId;
        await using (var lookup = fixture.CreateContext())
        {
            var itemId = await lookup.SalesOrderItems.Where(item => item.SalesOrderId == saleId).Select(item => item.Id).SingleAsync();
            var first = await service.ReturnSaleAsync(new ReturnSaleCommand(
                saleId,
                [new SalesReturnLineInput(itemId, 1, ReturnDisposition.Restock, 1)],
                [new PaymentAllocationInput(PaymentMethod.Cash, 50m), new PaymentAllocationInput(PaymentMethod.OnAccount, 50m)],
                "Müşteri talebi", null, "test", Guid.NewGuid().ToString()));
            first.IsSuccess.Should().BeTrue(first.Error);
        }

        await using (var partial = fixture.CreateContext())
        {
            (await partial.SalesOrders.SingleAsync()).Status.Should().Be(SalesOrderStatus.PartiallyRefunded);
            (await partial.Inventories.SingleAsync(item => item.ProductId == 100)).Quantity.Should().Be(9);
        }

        await using (var lookup = fixture.CreateContext())
        {
            var order = await lookup.SalesOrders.Include(item => item.Items).SingleAsync();
            var second = await service.ReturnSaleAsync(new ReturnSaleCommand(
                order.Id,
                [new SalesReturnLineInput(order.Items.Single().Id, 1, ReturnDisposition.Restock, 1)],
                [new PaymentAllocationInput(PaymentMethod.Cash, 100m)],
                "Kalan ürün iadesi", null, "test", Guid.NewGuid().ToString()));
            second.IsSuccess.Should().BeTrue(second.Error);
        }

        await using var verify = fixture.CreateContext();
        (await verify.SalesOrders.SingleAsync()).Status.Should().Be(SalesOrderStatus.Refunded);
        (await verify.Inventories.SingleAsync(item => item.ProductId == 100)).Quantity.Should().Be(10);
        (await verify.Products.SingleAsync(item => item.Id == 100)).TotalStockQuantity.Should().Be(10);
        var customer = await verify.Customers.SingleAsync(item => item.Id == 200);
        customer.TotalSpent.Should().Be(0);
        customer.TotalPurchaseCount.Should().Be(0);
        customer.LoyaltyPoints.Should().Be(0);
        var balance = verify.Transactions.Where(item => item.CustomerId == 200).AsEnumerable().Sum(item => item.Type switch
        {
            TransactionType.Debt or TransactionType.Refund => item.Amount,
            TransactionType.Payment or TransactionType.CreditNote => -item.Amount,
            _ => 0m
        });
        balance.Should().Be(-50m);
        (await verify.CashTransactions.Where(item => item.TransactionType == CashTransactionType.CashExpense).SumAsync(item => item.Amount)).Should().Be(150m);
    }

    [Fact]
    public async Task DamagedReturn_CreatesSingleQuarantineWarehouse_AndRestoresPhysicalTotalStock()
    {
        await using var fixture = await RetailFixture.CreateAsync(stock: 5);
        var service = fixture.CreateService();
        var sale = await service.CompleteSaleAsync(SaleCommand(Guid.NewGuid().ToString(), 1, PaymentMethod.Cash));
        await using var lookup = fixture.CreateContext();
        var itemId = await lookup.SalesOrderItems.Select(item => item.Id).SingleAsync();

        var result = await service.ReturnSaleAsync(new ReturnSaleCommand(
            sale.Value!.SalesOrderId,
            [new SalesReturnLineInput(itemId, 1, ReturnDisposition.Quarantine, 1)],
            [new PaymentAllocationInput(PaymentMethod.Cash, 100m)],
            "Hasarlı ürün", null, "test", Guid.NewGuid().ToString()));

        result.IsSuccess.Should().BeTrue(result.Error);
        await using var verify = fixture.CreateContext();
        var quarantine = await verify.Warehouses.SingleAsync(item => item.IsQuarantine);
        (await verify.Inventories.SingleAsync(item => item.ProductId == 100 && item.WarehouseId == quarantine.Id)).Quantity.Should().Be(1);
        (await verify.Products.SingleAsync(item => item.Id == 100)).TotalStockQuantity.Should().Be(5);
    }

    [Fact]
    public async Task RefundAboveExternalCollection_RollsBackStockAndAllReturnLedgers()
    {
        await using var fixture = await RetailFixture.CreateAsync(stock: 5, withCustomer: true);
        var service = fixture.CreateService();
        var sale = await service.CompleteSaleAsync(new CompleteSaleCommand(
            200, "Test Müşteri", 1,
            [new TransactionLineInput(100, "Test Ürün", 1, 100m, 0m, 0, 100m)],
            [new PaymentAllocationInput(PaymentMethod.Cash, 50m), new PaymentAllocationInput(PaymentMethod.OnAccount, 50m)],
            null, "test", Guid.NewGuid().ToString()));
        await using var lookup = fixture.CreateContext();
        var itemId = await lookup.SalesOrderItems.Select(item => item.Id).SingleAsync();

        var result = await service.ReturnSaleAsync(new ReturnSaleCommand(
            sale.Value!.SalesOrderId,
            [new SalesReturnLineInput(itemId, 1, ReturnDisposition.Restock, 1)],
            [new PaymentAllocationInput(PaymentMethod.Cash, 100m)],
            "Hatalı dış iade", null, "test", Guid.NewGuid().ToString()));

        result.IsFailure.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        (await verify.Inventories.SingleAsync(item => item.ProductId == 100)).Quantity.Should().Be(4);
        (await verify.SalesReturns.CountAsync()).Should().Be(0);
        (await verify.CashTransactions.CountAsync()).Should().Be(1);
        (await verify.StockTransactions.CountAsync(item => item.TransactionType == StockTransactionType.ReturnFromCustomer)).Should().Be(0);
    }

    [Fact]
    public async Task LegacyLedgerPreview_DoesNotWrite_AndApplyIsIdempotent()
    {
        await using var fixture = await RetailFixture.CreateAsync(stock: 5, withCustomer: true);
        var service = fixture.CreateService();
        var sale = await service.CompleteSaleAsync(SaleCommand(Guid.NewGuid().ToString(), 1, PaymentMethod.OnAccount, customerId: 200));
        await using (var seed = fixture.CreateContext())
        {
            seed.Transactions.Add(new Transaction
            {
                CustomerId = 200,
                Amount = 100m,
                Type = TransactionType.Payment,
                Date = DateTime.UtcNow,
                Description = $"Eski cari tahsilat {sale.Value!.OrderNumber}"
            });
            await seed.SaveChangesAsync();
        }

        var preview = await service.PreviewLegacyLedgerAsync();
        preview.IsSuccess.Should().BeTrue(preview.Error);
        preview.Value!.Issues.Should().ContainSingle();
        await using (var afterPreview = fixture.CreateContext())
            (await afterPreview.Transactions.CountAsync(item => item.ReconciliationKey != null)).Should().Be(0);

        var key = preview.Value.Issues.Single().ReconciliationKey;
        var first = await service.ApplyLegacyLedgerCorrectionsAsync([key], "test");
        var second = await service.ApplyLegacyLedgerCorrectionsAsync([key], "test");

        first.Value.Should().Be(1);
        second.Value.Should().Be(0);
        await using var verify = fixture.CreateContext();
        (await verify.Transactions.CountAsync(item => item.ReconciliationKey == key)).Should().Be(1);
    }

    [Fact]
    public async Task CompletedReturn_CannotBeEditedOrDeleted()
    {
        await using var fixture = await RetailFixture.CreateAsync(stock: 5);
        var service = fixture.CreateService();
        var sale = await service.CompleteSaleAsync(SaleCommand(Guid.NewGuid().ToString(), 1, PaymentMethod.Cash));
        await using var lookup = fixture.CreateContext();
        var itemId = await lookup.SalesOrderItems.Select(item => item.Id).SingleAsync();
        var returned = await service.ReturnSaleAsync(new ReturnSaleCommand(
            sale.Value!.SalesOrderId,
            [new SalesReturnLineInput(itemId, 1, ReturnDisposition.Restock, 1)],
            [new PaymentAllocationInput(PaymentMethod.Cash, 100m)],
            "Tam iade", null, "test", Guid.NewGuid().ToString()));

        await using var mutate = fixture.CreateContext();
        var entity = await mutate.SalesReturns.SingleAsync(item => item.Id == returned.Value!.ReturnId);
        entity.Reason = "Değiştirildi";
        var update = () => mutate.SaveChangesAsync();
        await update.Should().ThrowAsync<InvalidOperationException>().WithMessage("*telafi işlemi*");
    }

    [Fact]
    public async Task UnauthorizedReturn_DoesNotWriteAnything()
    {
        await using var fixture = await RetailFixture.CreateAsync(stock: 5);
        var sale = await fixture.CreateService().CompleteSaleAsync(SaleCommand(Guid.NewGuid().ToString(), 1, PaymentMethod.Cash));
        await using var lookup = fixture.CreateContext();
        var itemId = await lookup.SalesOrderItems.Select(item => item.Id).SingleAsync();
        var service = fixture.CreateService(isAuthorized: false);

        var result = await service.ReturnSaleAsync(new ReturnSaleCommand(
            sale.Value!.SalesOrderId,
            [new SalesReturnLineInput(itemId, 1, ReturnDisposition.Restock, 1)],
            [new PaymentAllocationInput(PaymentMethod.Cash, 100m)],
            "Yetkisiz", null, "test", Guid.NewGuid().ToString()));

        result.IsFailure.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        (await verify.SalesReturns.CountAsync()).Should().Be(0);
        (await verify.Inventories.SingleAsync(item => item.ProductId == 100)).Quantity.Should().Be(4);
    }

    private static CompleteSaleCommand SaleCommand(string key, int quantity, PaymentMethod method, int? customerId = null) =>
        new(customerId, customerId.HasValue ? "Test Müşteri" : "Perakende", 1,
            [new TransactionLineInput(100, "Test Ürün", quantity, 100m, 0m, 0, quantity * 100m)],
            [new PaymentAllocationInput(method, quantity * 100m)], null, "test", key);

    private sealed class RetailFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        private RetailFixture(SqliteConnection connection, DbContextOptions<AppDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<RetailFixture> CreateAsync(int stock, bool withCustomer = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var fixture = new RetailFixture(connection, options);
            await using var context = fixture.CreateContext();
            await context.Database.EnsureCreatedAsync();
            context.Products.Add(new Product { Id = 100, ProductName = "Test Ürün", SKU = "TEST-100", Barcode = "100", Unit = "Adet", SalePrice = 100m, TotalStockQuantity = stock });
            context.Inventories.Add(new Inventory { ProductId = 100, WarehouseId = 1, Quantity = stock });
            if (withCustomer)
                context.Customers.Add(new Customer { Id = 200, CustomerCode = "C-200", FullName = "Test Müşteri", PhoneNumber = "", City = "" });
            await context.SaveChangesAsync();
            return fixture;
        }

        public AppDbContext CreateContext() => new(_options);

        public RetailTransactionService CreateService(bool isAuthorized = true) =>
            new(new DelegateFactory(_options), new TestAuthorizationService(isAuthorized));

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }

    private sealed class DelegateFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}
