using System;
using FluentAssertions;
using KamatekCrm.ViewModels;
using Xunit;

namespace KamatekCrm.Tests.Algorithms
{
    public class InventoryAlgorithmTests
    {
        /// <summary>
        /// Ağırlıklı Ortalama Maliyet (WAC) Algoritması
        /// </summary>
        public static decimal CalculateWac(int currentQuantity, decimal currentCost, int incomingQuantity, decimal incomingPrice)
        {
            if (incomingQuantity <= 0) return currentCost;
            if (currentQuantity <= 0) return incomingPrice;

            var totalCost = (currentQuantity * currentCost) + (incomingQuantity * incomingPrice);
            var totalQuantity = currentQuantity + incomingQuantity;

            return Math.Round(totalCost / totalQuantity, 2, MidpointRounding.AwayFromZero);
        }

        [Fact]
        public void Wac_ShouldCalculateCorrectly_WhenExistingStockIsPositive()
        {
            // Arrange: 10 adet 100 TL'den var. 20 adet 130 TL'den girdi.
            // Beklenen: (10 * 100 + 20 * 130) / 30 = (1000 + 2600) / 30 = 3600 / 30 = 120 TL
            int currentQty = 10;
            decimal currentCost = 100m;
            int incomingQty = 20;
            decimal incomingPrice = 130m;

            // Act
            var newWac = CalculateWac(currentQty, currentCost, incomingQty, incomingPrice);

            // Assert
            newWac.Should().Be(120m);
        }

        [Fact]
        public void Wac_ShouldTakeIncomingPrice_WhenExistingStockIsZero()
        {
            // Arrange: 0 stok, 15 adet 250 TL'den girdi.
            int currentQty = 0;
            decimal currentCost = 0m;
            int incomingQty = 15;
            decimal incomingPrice = 250m;

            // Act
            var newWac = CalculateWac(currentQty, currentCost, incomingQty, incomingPrice);

            // Assert
            newWac.Should().Be(250m);
        }

        [Fact]
        public void Wac_ShouldTakeIncomingPrice_WhenExistingStockIsNegative()
        {
            // Arrange: -5 adet stok açığı var, 10 adet 300 TL'den girdi.
            int currentQty = -5;
            decimal currentCost = 100m;
            int incomingQty = 10;
            decimal incomingPrice = 300m;

            // Act
            var newWac = CalculateWac(currentQty, currentCost, incomingQty, incomingPrice);

            // Assert
            newWac.Should().Be(300m);
        }

        [Fact]
        public void StockCountItem_ShouldCalculateDelta_WhenSurplus()
        {
            // Arrange: Sistemde 10 adet görünüyor, fiili sayım 14 adet çıktı (Fazla)
            var item = new StockCountItem
            {
                ProductId = 1,
                SystemQuantity = 10,
                PurchasePrice = 50m,
                CountedQuantity = 14
            };

            // Assert
            item.Difference.Should().Be(4); // +4
            item.FinancialDifference.Should().Be(200m); // 4 * 50 = +200 TL
            item.IsCounted.Should().BeTrue();
        }

        [Fact]
        public void StockCountItem_ShouldCalculateDelta_WhenShortage()
        {
            // Arrange: Sistemde 20 adet görünüyor, fiili sayım 17 adet çıktı (Eksik)
            var item = new StockCountItem
            {
                ProductId = 2,
                SystemQuantity = 20,
                PurchasePrice = 80m,
                CountedQuantity = 17
            };

            // Assert
            item.Difference.Should().Be(-3); // -3
            item.FinancialDifference.Should().Be(-240m); // -3 * 80 = -240 TL
        }

        [Fact]
        public void StockCountItem_ShouldCalculateZeroDelta_WhenEqual()
        {
            // Arrange: Sistemde 15 adet, fiili sayım 15 adet
            var item = new StockCountItem
            {
                ProductId = 3,
                SystemQuantity = 15,
                PurchasePrice = 120m,
                CountedQuantity = 15
            };

            // Assert
            item.Difference.Should().Be(0);
            item.FinancialDifference.Should().Be(0m);
        }
    }
}
