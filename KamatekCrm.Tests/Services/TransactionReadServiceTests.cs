using FluentAssertions;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace KamatekCrm.Tests.Services;

public sealed class TransactionReadServiceTests
{
    [Fact]
    public async Task ReadModels_ProjectWarehousePurchaseHistoryAndReturnReceiptWithoutTrackingEntities()
    {
        var (service, options) = await CreateServiceAsync();

        var warehouses = await service.GetActiveWarehousesAsync(includeQuarantine: false);
        var purchases = await service.GetPurchaseHistoryAsync();
        var receipt = await service.GetSalesReturnReceiptAsync(900);
        var workspace = await service.GetPurchasingWorkspaceAsync();
        var search = await service.SearchPurchaseProductsAsync("okuma");

        warehouses.IsSuccess.Should().BeTrue(warehouses.Error);
        warehouses.Value.Should().NotContain(item => item.IsQuarantine);
        purchases.Value.Should().ContainSingle(item => item.SupplierName == "Test Tedarikçi" && item.InvoiceNumber == "INV-READ");
        receipt.IsSuccess.Should().BeTrue(receipt.Error);
        receipt.Value!.SalesOrderNumber.Should().Be("ORD-READ");
        receipt.Value.Lines.Should().ContainSingle(item => item.ProductName == "Okuma Ürünü" && item.Quantity == 1);
        workspace.Value!.Products.Should().ContainSingle(item => item.Id == 100 && item.Sku == "READ-100");
        workspace.Value.Suppliers.Should().ContainSingle(item => item.Id == 300);
        search.Value.Should().ContainSingle(item => item.ProductName == "Okuma Ürünü");

        await using var verify = new AppDbContext(options);
        verify.ChangeTracker.Entries().Should().BeEmpty("query service returns DTO projections and never exposes tracked entities");
    }

    [Fact]
    public async Task ReadModels_WhenUnauthorized_ReturnFailureWithoutExposingData()
    {
        var (_, options) = await CreateServiceAsync();
        var factory = CreateFactory(options);
        var service = new TransactionReadService(factory, new TestAuthorizationService(isAuthorized: false));

        var result = await service.GetPurchaseHistoryAsync();

        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    private static async Task<(TransactionReadService Service, DbContextOptions<AppDbContext> Options)> CreateServiceAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using (var context = new AppDbContext(options))
        {
            var supplier = new Supplier { Id = 300, Name = "Test", CompanyName = "Test Tedarikçi" };
            var product = new Product { Id = 100, ProductName = "Okuma Ürünü", SKU = "READ-100" };
            var purchase = new PurchaseOrder
            {
                Id = 500,
                SupplierId = supplier.Id,
                Supplier = supplier,
                InvoiceNumber = "INV-READ",
                OrderDate = DateTime.UtcNow,
                Status = PurchaseStatus.Received,
                TotalAmount = 100m
            };
            var sale = new SalesOrder
            {
                Id = 700,
                OrderNumber = "ORD-READ",
                CustomerName = "Test",
                Status = SalesOrderStatus.Refunded,
                TotalAmount = 100m
            };
            var saleLine = new SalesOrderItem
            {
                Id = 800,
                SalesOrderId = sale.Id,
                SalesOrder = sale,
                ProductId = product.Id,
                ProductName = product.ProductName,
                Quantity = 1,
                UnitPrice = 100m,
                LineTotal = 100m
            };
            sale.Items.Add(saleLine);
            var salesReturn = new SalesReturn
            {
                Id = 900,
                SalesOrderId = sale.Id,
                SalesOrder = sale,
                ReturnNumber = "SRET-READ",
                IdempotencyKey = Guid.NewGuid().ToString(),
                Reason = "Test",
                TotalAmount = 100m,
                CreatedBy = "test"
            };
            salesReturn.Items.Add(new SalesReturnItem
            {
                Id = 901,
                SalesOrderItemId = saleLine.Id,
                SalesOrderItem = saleLine,
                ProductId = product.Id,
                DestinationWarehouseId = 1,
                Quantity = 1,
                LineTotal = 100m
            });
            salesReturn.Payments.Add(new SalesReturnPayment { Id = 902, PaymentMethod = PaymentMethod.Cash, Amount = 100m });
            context.Products.Add(product);
            context.PurchaseOrders.Add(purchase);
            context.SalesReturns.Add(salesReturn);
            context.Warehouses.Add(new Warehouse { Id = 3, Name = "İade / Karantina", IsActive = true, IsQuarantine = true });
            context.Warehouses.Add(new Warehouse { Id = 4, Name = "Satılabilir Depo", IsActive = true, IsQuarantine = false });
            await context.SaveChangesAsync();
        }

        return (new TransactionReadService(CreateFactory(options), new TestAuthorizationService()), options);
    }

    private static IDbContextFactory<AppDbContext> CreateFactory(DbContextOptions<AppDbContext> options)
    {
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(item => item.CreateDbContextAsync(default)).ReturnsAsync(() => new AppDbContext(options));
        return factory.Object;
    }
}
