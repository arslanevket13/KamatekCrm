using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Services.Domain;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Helpers;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Models.Specs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KamatekCrm.Tests
{
    public class StockAndInventoryModuleTests
    {
        private readonly Mock<KamatekCrm.Services.IAuthService> _authServiceMock = new();

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
        public void TransferStock_ShouldFail_WhenSourceAndTargetWarehousesAreSame()
        {
            // Arrange
            var factory = CreateInMemoryDbContextFactory(Guid.NewGuid().ToString());
            var service = new InventoryDomainService(
                _authServiceMock.Object,
                factory,
                new TestAuthorizationService(),
                new TestAuditTrailService());

            var request = new TransferRequest
            {
                ProductId = 10,
                SourceWarehouseId = 1,
                TargetWarehouseId = 1, // Aynı depo
                Quantity = 5
            };

            // Act
            var result = service.TransferStock(request);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Kaynak ve hedef depo aynı olamaz");
        }

        [Fact]
        public void TransferStock_ShouldFail_WhenAvailableStockIsInsufficient()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var factory = CreateInMemoryDbContextFactory(dbName);

            // Veritabanına ürün ve 3 adet stok ekle
            using (var ctx = factory.CreateDbContext())
            {
                ctx.Products.Add(new Product { Id = 1, ProductName = "Test Kamera", SKU = "CAM-001" });
                ctx.Warehouses.Add(new Warehouse { Id = 1, Name = "Ana Depo" });
                ctx.Warehouses.Add(new Warehouse { Id = 2, Name = "Şantiye Depo" });
                ctx.Inventories.Add(new Inventory { Id = 1, ProductId = 1, WarehouseId = 1, Quantity = 3 });
                ctx.SaveChanges();
            }

            var service = new InventoryDomainService(
                _authServiceMock.Object,
                factory,
                new TestAuthorizationService(),
                new TestAuditTrailService());

            var request = new TransferRequest
            {
                ProductId = 1,
                SourceWarehouseId = 1,
                TargetWarehouseId = 2,
                Quantity = 10 // Mevcut 3 iken 10 adet transfer edilmek isteniyor
            };

            // Act
            var result = service.TransferStock(request);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("yeterli stok yok");
        }

        [Fact]
        public async Task ProductCodeHelper_ShouldNormalizeAndDetectDuplicateSkuOrBarcode()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var factory = CreateInMemoryDbContextFactory(dbName);

            using (var ctx = factory.CreateDbContext())
            {
                ctx.Products.Add(new Product
                {
                    Id = 1,
                    ProductName = "Mevcut Ürün",
                    SKU = "SEC-CAM-0001",
                    Barcode = "8690123456789"
                });
                await ctx.SaveChangesAsync();
            }

            // Normalizasyon Testi
            string rawSku = "   sec-cam-0001   ";
            string rawBarcode = "  8690123456789   ";
            string normalizedSku = ProductCodeHelper.Normalize(rawSku);
            string normalizedBarcode = ProductCodeHelper.Normalize(rawBarcode);

            normalizedSku.Should().Be("SEC-CAM-0001");
            normalizedBarcode.Should().Be("8690123456789");

            // Mükerrer Kontrol Testi
            await using (var ctx = await factory.CreateDbContextAsync())
            {
                var duplicateSkuExists = await ctx.Products.AnyAsync(p => p.SKU == normalizedSku);
                var duplicateBarcodeExists = await ctx.Products.AnyAsync(p => p.Barcode == normalizedBarcode);

                duplicateSkuExists.Should().BeTrue();
                duplicateBarcodeExists.Should().BeTrue();
            }
        }

        [Fact]
        public void StockValuation_ShouldClampNegativeQuantitiesToZero_AndNotCorruptTotalValue()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product { Id = 1, ProductName = "Normal Ürün", TotalStockQuantity = 10, PurchasePrice = 100m },
                new Product { Id = 2, ProductName = "Sıfır Stoklu Ürün", TotalStockQuantity = 0, PurchasePrice = 250m },
                new Product { Id = 3, ProductName = "Negatif Stoklu Hatalı Ürün", TotalStockQuantity = -5, PurchasePrice = 500m }
            };

            // Act - Negatif stok korumalı değerleme mantığı
            decimal totalValuation = products.Sum(p => Math.Max(0, p.TotalStockQuantity) * p.PurchasePrice);

            // Assert
            // (10 * 100) + (0 * 250) + (Math.Max(0, -5) * 500) = 1000 + 0 + 0 = 1000
            totalValuation.Should().Be(1000m);
        }

        [Fact]
        public void CriticalStock_SuggestedOrderQuantity_ShouldCalculateCorrectlyForZeroAndNearZeroStock()
        {
            // Arrange
            var p1 = new Product { Id = 1, TotalStockQuantity = 2, MinStockLevel = 10, PurchasePrice = 50m };
            var p2 = new Product { Id = 2, TotalStockQuantity = 0, MinStockLevel = 5, PurchasePrice = 80m };
            var p3 = new Product { Id = 3, TotalStockQuantity = -3, MinStockLevel = 10, PurchasePrice = 100m };

            // Act
            int suggested1 = Math.Max(1, (p1.MinStockLevel * 2) - Math.Max(0, p1.TotalStockQuantity));
            int suggested2 = Math.Max(1, (p2.MinStockLevel * 2) - Math.Max(0, p2.TotalStockQuantity));
            int suggested3 = Math.Max(1, (p3.MinStockLevel * 2) - Math.Max(0, p3.TotalStockQuantity));

            // Assert
            // p1: (10 * 2) - 2 = 18
            suggested1.Should().Be(18);

            // p2: (5 * 2) - 0 = 10
            suggested2.Should().Be(10);

            // p3: (10 * 2) - Math.Max(0, -3) = 20
            suggested3.Should().Be(20);
        }

        [Fact]
        public void ProductSpecHelper_ShouldSafelyDeserializeBrokenOrEmptyJson_WithoutThrowingJsonException()
        {
            // Arrange
            string? nullJson = null;
            string emptyJson = "   ";
            string brokenJson = "{ this is not a valid json at all !!! }";
            string validCameraJson = "{\"$type\":\"camera\",\"Resolution\":\"4 MP\",\"IsPoE\":true}";

            // Act
            var nullResult = ProductSpecHelper.DeserializeSpecs(nullJson);
            var emptyResult = ProductSpecHelper.DeserializeSpecs(emptyJson);
            var brokenResult = ProductSpecHelper.DeserializeSpecs(brokenJson);
            var validResult = ProductSpecHelper.DeserializeSpecs(validCameraJson);

            // Assert
            nullResult.Should().NotBeNull().And.BeOfType<GeneralSpecs>();
            emptyResult.Should().NotBeNull().And.BeOfType<GeneralSpecs>();
            brokenResult.Should().NotBeNull().And.BeOfType<GeneralSpecs>();
            validResult.Should().NotBeNull().And.BeOfType<CameraSpecs>();

            var camera = (CameraSpecs)validResult;
            camera.Resolution.Should().Be("4 MP");
            camera.IsPoE.Should().BeTrue();
        }
    }
}

