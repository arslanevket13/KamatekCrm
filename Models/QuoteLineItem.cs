using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Teklif satırı - Ürün + birim eşleşmesi (in-memory)
    /// </summary>
    [NotMapped]
    public class QuoteLineItem
    {
        /// <summary>
        /// Benzersiz ID
        /// </summary>
        public string Id { get; set; } = System.Guid.NewGuid().ToString();

        /// <summary>
        /// Ürün ID
        /// </summary>
        public int ProductId { get; set; }

        /// <summary>
        /// Ürün adı (görüntüleme için)
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// SKU/Barkod
        /// </summary>
        public string? ProductSKU { get; set; }

        /// <summary>
        /// Birim başına adet
        /// </summary>
        public int QuantityPerUnit { get; set; } = 1;

        /// <summary>
        /// Atanan birim ID'leri
        /// </summary>
        public List<string> AssignedUnitIds { get; set; } = new();

        /// <summary>
        /// Toplam adet (QuantityPerUnit × AssignedUnitIds.Count)
        /// </summary>
        public int TotalQuantity => QuantityPerUnit * AssignedUnitIds.Count;

        /// <summary>
        /// Birim fiyat
        /// </summary>
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Toplam tutar
        /// </summary>
        public decimal TotalAmount => UnitPrice * TotalQuantity;

        /// <summary>
        /// Tüm birimlere uygula mı?
        /// </summary>
        public bool ApplyToAllUnits { get; set; } = false;

        /// <summary>
        /// Sadece girişlere uygula mı?
        /// </summary>
        public bool ApplyToEntrancesOnly { get; set; } = false;

        /// <summary>
        /// Not
        /// </summary>
        public string? Note { get; set; }
    }
}
