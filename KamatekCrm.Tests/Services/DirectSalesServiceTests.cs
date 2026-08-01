using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Services;

public class DirectSalesServiceTests
{
    [Fact]
    public async Task ProcessSale_WithSameIdempotencyKey_ChangesStockOnlyOnce()
    {
        var (service, factory) = CreateServiceWithStock(10);
        var key = Guid.NewGuid().ToString();
        var items = new[] { CartItem(1, 2, 100m) };
        var payments = new[] { new PosPaymentEntry { PaymentMethod = PaymentMethod.Cash, Amount = 200m } };

        var first = await service.ProcessSaleAsync(null, "Perakende", 1, items, payments, null, "test", key);
        var second = await service.ProcessSaleAsync(null, "Perakende", 1, items, payments, null, "test", key);

        second.Id.Should().Be(first.Id);
        await using var verify = await factory.CreateDbContextAsync();
        (await verify.SalesOrders.CountAsync()).Should().Be(1);
        (await verify.Inventories.SingleAsync()).Quantity.Should().Be(8);
    }

    [Fact]
    public async Task ProcessSale_AggregatesDuplicateProductLines_WhenCheckingStock()
    {
        var (service, _) = CreateServiceWithStock(5);
        var items = new[] { CartItem(1, 3, 10m), CartItem(1, 3, 10m) };
        var payments = new[] { new PosPaymentEntry { PaymentMethod = PaymentMethod.Cash, Amount = 60m } };

        var action = () => service.ProcessSaleAsync(null, "Perakende", 1, items, payments, null, "test", Guid.NewGuid().ToString());

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*yetersiz stok*");
    }

    [Fact]
    public async Task ProcessSale_WhenUserIsUnauthorized_DoesNotWriteAnything()
    {
        var (service, factory) = CreateServiceWithStock(10, isAuthorized: false);
        var items = new[] { CartItem(1, 1, 100m) };
        var payments = new[] { new PosPaymentEntry { PaymentMethod = PaymentMethod.Cash, Amount = 100m } };

        var action = () => service.ProcessSaleAsync(
            null, "Perakende", 1, items, payments, null, "test", Guid.NewGuid().ToString());

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        await using var verify = await factory.CreateDbContextAsync();
        (await verify.SalesOrders.CountAsync()).Should().Be(0);
        (await verify.Inventories.SingleAsync()).Quantity.Should().Be(10);
    }

    private static PosCartItem CartItem(int productId, int quantity, decimal unitPrice) => new()
    {
        ProductId = productId,
        ProductName = "Test Ürün",
        Quantity = quantity,
        UnitPrice = unitPrice,
        TaxRate = 0
    };

    private static (DirectSalesService Service, IDbContextFactory<AppDbContext> Factory) CreateServiceWithStock(
        int quantity,
        bool isAuthorized = true)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(default)).ReturnsAsync(() => new AppDbContext(options));

        using (var seed = new AppDbContext(options))
        {
            seed.Inventories.Add(new Inventory { ProductId = 1, WarehouseId = 1, Quantity = quantity });
            seed.SaveChanges();
        }

        return (new DirectSalesService(factory.Object, new TestAuthorizationService(isAuthorized)), factory.Object);
    }
}
