using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Ürün/Stok entity'si
    /// </summary>
    public class Product
    {
        public int Id { get; set; }

        /// <summary>
        /// Ürün Kodu / Barkod (Unique)
        /// </summary>
        [MaxLength(100)]
        public string? SKU { get; set; }

        /// <summary>
        /// Barkod Numarası
        /// </summary>
        [MaxLength(50)]
        public string? Barcode { get; set; }

        [Required(ErrorMessage = "Ürün adı zorunludur")]
        [MaxLength(200)]
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Model Adı
        /// </summary>
        [MaxLength(100)]
        public string? ModelName { get; set; }

        /// <summary>
        /// Ürün Açıklaması
        /// </summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Birim (Adet, Metre, Kg, Paket vb.)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Unit { get; set; } = "Adet";

        /// <summary>
        /// Ürün Kategorisi (Enum)
        /// </summary>
        public ProductCategory ProductCategoryType { get; set; } = ProductCategory.Other;

        /// <summary>
        /// Tedarikçi Adı
        /// </summary>
        [MaxLength(200)]
        public string? SupplierName { get; set; }

        /// <summary>
        /// Para Birimi
        /// </summary>
        [MaxLength(10)]
        public string Currency { get; set; } = "TRY";

        /// <summary>
        /// Toplam Stok Miktarı (Tüm depolardaki toplam)
        /// </summary>
        public int TotalStockQuantity { get; set; }

        /// <summary>
        /// Kritik Stok Seviyesi (Uyarı için)
        /// </summary>
        public int MinStockLevel { get; set; } = 0;
        
        /// <summary>
        /// Maksimum Stok Seviyesi
        /// </summary>
        public int MaxStockLevel { get; set; } = 0;

        /// <summary>
        /// Alış Fiyatı (Son alış veya Liste fiyatı)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        /// <summary>
        /// Satış Fiyatı
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal SalePrice { get; set; }

        /// <summary>
        /// KDV Oranı (%)
        /// </summary>
        public int VatRate { get; set; } = 20;

        /// <summary>
        /// Hareketli Ağırlıklı Ortalama Maliyet
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentAverageCost { get; set; }

        /// <summary>
        /// Seri Numaralı Takip Yapılıyor mu?
        /// </summary>
        public bool IsSerialized { get; set; } = false;

        /// <summary>
        /// Kategori bazlı teknik özellikler (JSON olarak saklanır)
        /// </summary>
        public string? TechSpecsJson { get; set; }

        public int? BrandId { get; set; }
        public virtual Brand? Brand { get; set; }

        public int? CategoryId { get; set; }
        public virtual Category? Category { get; set; }

        /// <summary>
        /// Depolardaki Stoklar
        /// </summary>
        public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();

        /// <summary>
        /// Seri Numaraları
        /// </summary>
        public virtual ICollection<ProductSerial> ProductSerials { get; set; } = new List<ProductSerial>();

        /// <summary>
        /// Stok Hareketleri
        /// </summary>
        public virtual ICollection<StockTransaction> Transactions { get; set; } = new List<StockTransaction>();

        public virtual ICollection<ServiceJobItem> ServiceJobItems { get; set; } = new List<ServiceJobItem>();
    }
}
