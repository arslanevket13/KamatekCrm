using System;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Helpers
{
    /// <summary>
    /// Ürün Stok Kodu (SKU) ve Barkod standardizasyonu, normalizasyonu ve benzersiz kod üretimi yardımcısı.
    /// </summary>
    public static class ProductCodeHelper
    {
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return value.Trim().ToUpperInvariant();
        }

        public static string GenerateSku(ProductCategoryType? category = null)
        {
            var prefix = category switch
            {
                ProductCategoryType.Camera => "CAM",
                ProductCategoryType.Intercom => "INT",
                ProductCategoryType.FireAlarm => "FIR",
                ProductCategoryType.BurglarAlarm => "BUR",
                ProductCategoryType.SmartHome => "SMA",
                ProductCategoryType.AccessControl => "ACC",
                ProductCategoryType.Satellite => "SAT",
                ProductCategoryType.FiberOptic => "FIB",
                ProductCategoryType.Cable => "CBL",
                ProductCategoryType.Network => "NET",
                ProductCategoryType.Security => "SEC",
                _ => "GEN"
            };

            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var randPart = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            return $"PRD-{prefix}-{datePart}-{randPart}";
        }

        public static string GenerateBarcode()
        {
            var random = Random.Shared.Next(1000, 9999);
            return $"869{DateTime.UtcNow:yyMMdd}{random}";
        }
    }
}

