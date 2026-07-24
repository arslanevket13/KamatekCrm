using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.Data;
using KamatekCrm.Services;
using KamatekCrm.Services.Domain;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.DomainServices
{
    public class InventoryDomainServiceTests
    {
        private readonly Mock<IAuthService> _authServiceMock;

        public InventoryDomainServiceTests()
        {
            _authServiceMock = new Mock<IAuthService>();
        }

        private static IDbContextFactory<AppDbContext> CreateInMemoryDbContextFactory(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var factoryMock = new Mock<IDbContextFactory<AppDbContext>>();
            factoryMock.Setup(f => f.CreateDbContext()).Returns(() => new AppDbContext(options));
            factoryMock.Setup(f => f.CreateDbContextAsync(default)).ReturnsAsync(() => new AppDbContext(options));
            return factoryMock.Object;
        }

        [Fact]
        public void TransferStock_ShouldFail_WhenQuantityIsZeroOrNegative()
        {
            // Arrange
            var factory = CreateInMemoryDbContextFactory(Guid.NewGuid().ToString());
            var service = new InventoryDomainService(_authServiceMock.Object, factory);

            var request = new TransferRequest
            {
                ProductId = 1,
                SourceWarehouseId = 1,
                TargetWarehouseId = 2,
                Quantity = 0
            };

            // Act
            var result = service.TransferStock(request);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("sıfırdan büyük olmalıdır");
        }

        [Fact]
        public void TransferStock_ShouldFail_WhenSourceAndTargetWarehousesAreSame()
        {
            // Arrange
            var factory = CreateInMemoryDbContextFactory(Guid.NewGuid().ToString());
            var service = new InventoryDomainService(_authServiceMock.Object, factory);

            var request = new TransferRequest
            {
                ProductId = 1,
                SourceWarehouseId = 1,
                TargetWarehouseId = 1,
                Quantity = 10
            };

            // Act
            var result = service.TransferStock(request);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Kaynak ve hedef depo aynı olamaz");
        }

        [Fact]
        public void TransferStock_ShouldFail_WhenSourceStockIsInsufficient()
        {
            // Arrange
            string dbName = Guid.NewGuid().ToString();
            var factory = CreateInMemoryDbContextFactory(dbName);
            var service = new InventoryDomainService(_authServiceMock.Object, factory);

            // Seed initial stock of 5
            using (var context = factory.CreateDbContext())
            {
                context.Inventories.Add(new Inventory
                {
                    ProductId = 100,
                    WarehouseId = 1,
                    Quantity = 5
                });
                context.SaveChanges();
            }

            var request = new TransferRequest
            {
                ProductId = 100,
                SourceWarehouseId = 1,
                TargetWarehouseId = 2,
                Quantity = 10
            };

            // Act
            var result = service.TransferStock(request);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("yeterli stok yok");
        }

        [Fact]
        public void TransferStock_ShouldSucceed_WhenSufficientStockExists()
        {
            // Arrange
            string dbName = Guid.NewGuid().ToString();
            var factory = CreateInMemoryDbContextFactory(dbName);
            var service = new InventoryDomainService(_authServiceMock.Object, factory);

            // Seed initial stock of 20 in Warehouse 1
            using (var context = factory.CreateDbContext())
            {
                context.Inventories.Add(new Inventory
                {
                    ProductId = 10,
                    WarehouseId = 1,
                    Quantity = 20
                });
                context.SaveChanges();
            }

            var request = new TransferRequest
            {
                ProductId = 10,
                SourceWarehouseId = 1,
                TargetWarehouseId = 2,
                Quantity = 5,
                Description = "Test Transfer"
            };

            // Act
            var result = service.TransferStock(request);

            // Assert
            result.Success.Should().BeTrue();

            using var verifyContext = factory.CreateDbContext();
            var sourceInventory = verifyContext.Inventories.FirstOrDefault(i => i.ProductId == 10 && i.WarehouseId == 1);
            var targetInventory = verifyContext.Inventories.FirstOrDefault(i => i.ProductId == 10 && i.WarehouseId == 2);

            sourceInventory.Should().NotBeNull();
            sourceInventory!.Quantity.Should().Be(15);

            targetInventory.Should().NotBeNull();
            targetInventory!.Quantity.Should().Be(5);
        }
    }
}
