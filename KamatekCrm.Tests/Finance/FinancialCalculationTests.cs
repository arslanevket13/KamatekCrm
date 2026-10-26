using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using KamatekCrm.Shared.Services;
using Xunit;

namespace KamatekCrm.Tests.Finance
{
    public class FinancialCalculationTests
    {
        [Theory]
        [InlineData(10.004, 10.00)]
        [InlineData(10.005, 10.00)] // Banker's rounding to even
        [InlineData(10.015, 10.02)]
        [InlineData(123.456, 123.46)]
        public void RoundMoney_ShouldRoundToTwoDecimals_WithToEven(decimal input, decimal expected)
        {
            var result = FinancialCalculator.RoundMoney(input);
            result.Should().Be(expected);
        }

        [Fact]
        public void DistributeLargestRemainder_ShouldDistributeDiscount_WithZeroPennyDiscrepancy()
        {
            // Arrange: 100 TL genel iskonto, 3 farklı tutardaki satıra dağıtılacak
            decimal targetDiscount = 100.00m;
            var lineAmounts = new List<decimal> { 333.33m, 333.33m, 333.34m };

            // Act
            var distributedDiscounts = FinancialCalculator.DistributeLargestRemainder(targetDiscount, lineAmounts);

            // Assert: Dağıtılan parçaların toplamı KURUŞU KURUŞUNA tam 100.00 TL olmalıdır!
            distributedDiscounts.Sum().Should().Be(targetDiscount);
            distributedDiscounts.Should().HaveCount(3);
            distributedDiscounts.All(d => d > 0).Should().BeTrue();
        }

        [Fact]
        public void DistributeLargestRemainder_ShouldHandleOddFractions_WithoutLosingPenny()
        {
            // Arrange: 10 TL iskonto, eşit 3 satıra dağıtılırken 3.33, 3.33, 3.33 = 9.99 olur; 
            // Largest Remainder kalanı 1 kuruşu ağırlıklı satıra verip 10.00 TL yapmalıdır.
            decimal targetDiscount = 10.00m;
            var lineAmounts = new List<decimal> { 50m, 50m, 50m };

            // Act
            var distributed = FinancialCalculator.DistributeLargestRemainder(targetDiscount, lineAmounts);

            // Assert
            distributed.Sum().Should().Be(10.00m);
            // Parçalar 3.34, 3.33, 3.33 şeklinde olmalı
            distributed.Should().Contain(3.34m);
            distributed.Count(x => x == 3.33m).Should().Be(2);
        }

        [Fact]
        public void BalancePennyDiscrepancy_ShouldAdjustHighestLine_WhenDifferenceExists()
        {
            // Arrange
            decimal targetTotal = 100.00m;
            var lineAmounts = new decimal[] { 33.33m, 33.33m, 33.33m }; // Toplam 99.99 TL (1 kuruş eksik)
            var baseAmounts = new List<decimal> { 100m, 200m, 300m }; // 3. satır en yüksek baz tutara sahip

            // Act
            FinancialCalculator.BalancePennyDiscrepancy(targetTotal, lineAmounts, baseAmounts);

            // Assert: Eksik olan 1 kuruş en yüksek baz tutarlı satıra (3. elemana) eklenmeli
            lineAmounts.Sum().Should().Be(100.00m);
            lineAmounts[2].Should().Be(33.34m);
        }

        [Fact]
        public void VatCalculation_ShouldMatchStandardRate()
        {
            // Arrange
            decimal netAmount = 1500.00m;
            decimal vatRate = 20.00m;

            // Act
            decimal vatAmount = FinancialCalculator.RoundMoney(netAmount * vatRate / 100m);
            decimal grossAmount = netAmount + vatAmount;

            // Assert
            vatAmount.Should().Be(300.00m);
            grossAmount.Should().Be(1800.00m);
        }
    }
}
