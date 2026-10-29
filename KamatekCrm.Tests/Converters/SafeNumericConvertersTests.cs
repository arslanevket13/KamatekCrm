using System;
using System.Globalization;
using FluentAssertions;
using KamatekCrm.Converters;
using KamatekCrm.Shared.Enums;
using Xunit;

namespace KamatekCrm.Tests.Converters
{
    public class SafeNumericConvertersTests
    {
        [Theory]
        [InlineData("", 1)]
        [InlineData("   ", 1)]
        [InlineData(null, 1)]
        [InlineData("0", 1)]
        [InlineData("-5", 1)]
        [InlineData("10", 10)]
        [InlineData("abc", 1)]
        public void SafeIntConverter_ConvertBack_NeverThrowsFormatException_ReturnsValidInt(object? input, int expected)
        {
            var converter = new SafeIntConverter();
            var result = converter.ConvertBack(input, typeof(int), parameter: "1", CultureInfo.InvariantCulture);

            result.Should().BeOfType<int>();
            ((int)result).Should().Be(expected);
        }

        [Fact]
        public void SafeIntConverter_Convert_FormatsIntCorrectly()
        {
            var converter = new SafeIntConverter();
            var result = converter.Convert(42, typeof(string), null, CultureInfo.InvariantCulture);

            result.Should().Be("42");
        }

        [Theory]
        [InlineData("", null)]
        [InlineData("   ", null)]
        [InlineData(null, null)]
        [InlineData("60", 60)]
        [InlineData("xyz", null)]
        public void SafeNullableIntConverter_ConvertBack_HandlesEmptyAndInvalidSafely(object? input, int? expected)
        {
            var converter = new SafeNullableIntConverter();
            var result = converter.ConvertBack(input, typeof(int?), null, CultureInfo.InvariantCulture);

            if (expected == null)
            {
                result.Should().BeNull();
            }
            else
            {
                result.Should().Be(expected);
            }
        }

        [Theory]
        [InlineData("", 0.0)]
        [InlineData("   ", 0.0)]
        [InlineData(null, 0.0)]
        [InlineData("125.50", 125.50)]
        [InlineData("125,50", 125.50)]
        [InlineData("invalid", 0.0)]
        public void SafeDecimalConverter_ConvertBack_HandlesBothDotAndCommaSafely(object? input, double expectedDouble)
        {
            var converter = new SafeDecimalConverter();
            var result = converter.ConvertBack(input, typeof(decimal), null, new CultureInfo("tr-TR"));

            result.Should().BeOfType<decimal>();
            ((decimal)result).Should().Be((decimal)expectedDouble);
        }

        [Theory]
        [InlineData(StructureType.SingleUnit, "Müstakil")]
        [InlineData(StructureType.Apartment, "Apartman")]
        [InlineData(StructureType.Site, "Site")]
        [InlineData(StructureType.Commercial, "Ticari")]
        public void StructureTypeDisplayConverter_Convert_ReturnsClearTurkishText(StructureType type, string expectedSubstring)
        {
            var converter = new StructureTypeDisplayConverter();
            var result = converter.Convert(type, typeof(string), null, CultureInfo.InvariantCulture);

            result.Should().NotBeNull();
            result.ToString().Should().Contain(expectedSubstring);
        }
    }
}

