using System;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
    public class Product : KamatekCrm.Shared.Models.Common.BaseEntity
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string Currency { get; set; } = "TL";
        public int VatRate { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public int? BrandId { get; set; }
        public ProductCategoryType ProductCategoryType { get; set; }
        public int? CategoryId { get; set; }
        [ForeignKey(nameof(CategoryId))]
        public virtual Category? Category { get; set; }
        public int TotalStockQuantity { get; set; }
        public int MinStockLevel { get; set; }
        public string TechSpecsJson { get; set; } = "{}";
        /// <summary>
        /// Relative path to compressed product image (e.g., "uploads/products/xxx.webp")
        /// </summary>
        public string? ImagePath { get; set; }
        public decimal AverageCost { get; set; }
        [ForeignKey(nameof(BrandId))]
        public virtual Brand? Brand { get; set; } = new Brand();
        public virtual System.Collections.Generic.ICollection<Inventory> Inventories { get; set; } = new System.Collections.Generic.List<Inventory>();
        public virtual System.Collections.Generic.ICollection<StockTransaction> Transactions { get; set; } = new System.Collections.Generic.List<StockTransaction>();
    }

    public class Brand
    {
        public int Id { get; set; }
        public string BrandName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int? ParentId { get; set; }
        public virtual Category? ParentCategory { get; set; }
        public virtual System.Collections.Generic.ICollection<Category> SubCategories { get; set; } = new System.Collections.Generic.List<Category>();
    }

    public class Inventory
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public int Quantity { get; set; }
        public decimal AverageCost { get; set; }
        [ForeignKey(nameof(ProductId))]
        public virtual Product Product { get; set; } = null!;
        [ForeignKey(nameof(WarehouseId))]
        public virtual Warehouse Warehouse { get; set; } = null!;
    }

    public class Warehouse : KamatekCrm.Shared.Models.Common.BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public WarehouseType Type { get; set; }
        public bool IsActive { get; set; }
        public virtual System.Collections.Generic.ICollection<Inventory> Inventories { get; set; } = new System.Collections.Generic.List<Inventory>();
    }

    public class StockTransaction
    {
        public int Id { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public int ProductId { get; set; }
        public int? SourceWarehouseId { get; set; }
        public int? TargetWarehouseId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public StockTransactionType TransactionType { get; set; }
        public string Description { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int? InventoryId { get; set; }
        public int? PurchaseOrderId { get; set; }
        public int? SalesOrderId { get; set; }
        [ForeignKey(nameof(SourceWarehouseId))]
        public virtual Warehouse? SourceWarehouse { get; set; }
        [ForeignKey(nameof(TargetWarehouseId))]
        public virtual Warehouse? TargetWarehouse { get; set; }
        [ForeignKey(nameof(ProductId))]
        public virtual Product Product { get; set; } = null!;
    }

    public class ServiceJobItem
    {
        public int Id { get; set; }
        public int ServiceJobId { get; set; }
        public int ProductId { get; set; }
        public int QuantityUsed { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitCost { get; set; }
        [ForeignKey(nameof(ProductId))]
        public virtual Product Product { get; set; } = null!;
        [ForeignKey(nameof(ServiceJobId))]
        public virtual ServiceJob ServiceJob { get; set; } = null!;
    }

    public class ProductSerial
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = "";
        public int ProductId { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime? ManufactureDate { get; set; }
        public string? Location { get; set; }
    }

    public class InventoryImage
    {
        public int Id { get; set; }
        public int InventoryId { get; set; }
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public string UploadedBy { get; set; } = string.Empty;
        [ForeignKey(nameof(ProductId))]
        public virtual Product Product { get; set; } = null!;
        [ForeignKey(nameof(WarehouseId))]
        public virtual Warehouse Warehouse { get; set; } = null!;
    }

    public class StockReservation
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public int Quantity { get; set; }
        public string ReferenceType { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
        public DateTime ReservedAt { get; set; } = DateTime.Now;
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        public string ReservedBy { get; set; } = string.Empty;
    }
}
