using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public TransactionType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer Customer { get; set; } = null!;
    }

    public class ServiceProject
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty; 
        public string Name { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string ProjectCode { get; set; } = string.Empty;
        public string ProjectScopeJson { get; set; } = "[]";
        public decimal TotalBudget { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal DiscountPercent { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public PipelineStage PipelineStage { get; set; }
        public ProjectStatus Status { get; set; } // Error log says ProjectStatus
        
        // Computed/Stub props
        public int TotalUnitCount { get; set; }
        public string SurveyNotes { get; set; } = "";
        public string QuoteItemsJson { get; set; } = "";

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }
    }
    
    // Stub for ProjectStatus if not in Enums
    public enum ProjectStatus { Active, Completed, Cancelled, Pending, Draft, PendingApproval }


    public class CustomerAsset
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public JobCategory Category { get; set; } = JobCategory.Other;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string? Location { get; set; }
        public AssetStatus Status { get; set; } = AssetStatus.Active;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string FullName => $"{Brand} {Model}";

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer Customer { get; set; } = null!;
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

    public class PurchaseOrder : KamatekCrm.Shared.Models.Common.BaseEntity
    {
        // Id is in BaseEntity
        public int SupplierId { get; set; }
        public PurchaseStatus Status { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public DateTime OrderDate { get; set; } = DateTime.Now; // Alias for Date?
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
        [ForeignKey(nameof(SupplierId))]
        public virtual Supplier Supplier { get; set; } = null!;  
        public virtual System.Collections.Generic.ICollection<PurchaseOrderItem> Items { get; set; } = new System.Collections.Generic.List<PurchaseOrderItem>();
    }

    public class PurchaseOrderItem
    {
        public int Id { get; set; }
        public int PurchaseOrderId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal SubTotal { get; set; }

        [ForeignKey(nameof(PurchaseOrderId))]
        public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    }
    
    public class Supplier : KamatekCrm.Shared.Models.Common.BaseEntity
    {
        // Id is in BaseEntity
        public string Name { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public bool IsActive { get; set; } = true;
        public virtual System.Collections.Generic.ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new System.Collections.Generic.List<PurchaseOrder>();
    }

    public class Product : KamatekCrm.Shared.Models.Common.BaseEntity
    {
        // Id is in BaseEntity
        public string ProductName { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
        // New properties for WPF
        public string SKU { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string Currency { get; set; } = "TL";
        public int VatRate { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public int? BrandId { get; set; }
        public ProductCategoryType ProductCategoryType { get; set; } // Enum
        public int? CategoryId { get; set; } // ID usage
        [ForeignKey(nameof(CategoryId))]
        public virtual Category? Category { get; set; } // Class navigation property
        
        public int TotalStockQuantity { get; set; }

        public int MinStockLevel { get; set; }
        public string TechSpecsJson { get; set; } = "{}";

        /// <summary>
        /// Relative path to compressed product image (e.g., "uploads/products/xxx.webp")
        /// </summary>
        public string? ImagePath { get; set; }

        [ForeignKey(nameof(BrandId))]
        public virtual Brand? Brand { get; set; } = new Brand();

        public virtual System.Collections.Generic.ICollection<Inventory> Inventories { get; set; } = new System.Collections.Generic.List<Inventory>();
        public virtual System.Collections.Generic.ICollection<StockTransaction> Transactions { get; set; } = new System.Collections.Generic.List<StockTransaction>();
    }

    public class Brand { public int Id { get; set; } public string BrandName { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; } // Alias Name

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
        // Id is in BaseEntity
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
        public int? InventoryId { get; set; } // Added
        public int? PurchaseOrderId { get; set; } // Added
        public int? SalesOrderId { get; set; } // Added

        [ForeignKey(nameof(SourceWarehouseId))]
        public virtual Warehouse? SourceWarehouse { get; set; }
        [ForeignKey(nameof(TargetWarehouseId))]
        public virtual Warehouse? TargetWarehouse { get; set; }
        [ForeignKey(nameof(ProductId))]
        public virtual Product Product { get; set; } = null!;
    }

    public class CashTransaction
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public CashTransactionType TransactionType { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public int? SalesOrderId { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public int? CustomerId { get; set; } // Added for RepairViewModel
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }
    }
    
    public class SalesOrder { 
        public int Id { get; set; } 
        public int CustomerId { get; set; } 
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.Now;
        public string PaymentMethod { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal TotalAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
        public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Completed;
        public virtual Customer Customer { get; set; } = null!; 
        public virtual System.Collections.Generic.ICollection<SalesOrderItem> Items { get; set; } = new System.Collections.Generic.List<SalesOrderItem>(); 
        public virtual System.Collections.Generic.ICollection<SalesOrderPayment> Payments { get; set; } = new System.Collections.Generic.List<SalesOrderPayment>();
    }

    public class SalesOrderItem { 
        public int Id { get; set; } 
        public int SalesOrderId { get; set; } 
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public int TaxRate { get; set; }
        public decimal LineTotal { get; set; }
        public virtual SalesOrder SalesOrder { get; set; } = null!; 
    }

    /// <summary>
    /// Split-payment kaydı — bir SalesOrder'a birden fazla ödeme yöntemi
    /// </summary>
    public class SalesOrderPayment {
        public int Id { get; set; }
        public int SalesOrderId { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
        [ForeignKey(nameof(SalesOrderId))]
        public virtual SalesOrder SalesOrder { get; set; } = null!;
    }
    public class Category { public int Id { get; set; } public string Name { get; set; } = ""; public int? ParentId { get; set; } public virtual Category? ParentCategory { get; set; } public virtual System.Collections.Generic.ICollection<Category> SubCategories { get; set; } = new System.Collections.Generic.List<Category>(); }

    // Address Models
    public class City { public int Id { get; set; } public string Name { get; set; } = ""; public virtual System.Collections.Generic.ICollection<District> Districts { get; set; } = new System.Collections.Generic.List<District>(); }
    public class District { public int Id { get; set; } public string Name { get; set; } = ""; public int CityId { get; set; } public virtual System.Collections.Generic.ICollection<Neighborhood> Neighborhoods { get; set; } = new System.Collections.Generic.List<Neighborhood>(); }
    public class Neighborhood { public int Id { get; set; } public string Name { get; set; } = ""; public int DistrictId { get; set; } }

    // Other Stubs
    public class JobDetailBase { public int Id { get; set; } }
    public class MaintenanceContract { 
        public int Id { get; set; } 
        public string Title { get; set; } = ""; 
        public bool IsActive { get; set; }
        public DateTime NextDueDate { get; set; }
        // Added Customer reference
        public int CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer Customer { get; set; } = null!;
        // New Props
        public string JobDescriptionTemplate { get; set; } = "";
        public decimal PricePerVisit { get; set; }
        public int FrequencyInMonths { get; set; }
    }

    public class Attachment { 
        public int Id { get; set; } 
        public string FileName { get; set; } = ""; 
        public string FilePath { get; set; } = ""; // Path alias
        public string Path { get; set; } = ""; 
        public long FileSize { get; set; }
        public string ContentType { get; set; } = "";
        public DateTime UploadDate { get; set; } = DateTime.Now;
        public string UploadedBy { get; set; } = "";
        public string Description { get; set; } = "";
        public AttachmentEntityType EntityType { get; set; } 
        public int EntityId { get; set; } 
    }
    public class ProductSerial { public int Id { get; set; } public string SerialNumber { get; set; } = ""; public int ProductId { get; set; } }
    public class ActivityLog { 
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string? Username { get; set; }
        public string ActionType { get; set; } = ""; // String usage in AuditService
        public string Action { get; set; } = "";
        public string? EntityName { get; set; }
        public string? RecordId { get; set; }
        public string? Description { get; set; }
        public string? AdditionalData { get; set; }
        public DateTime Timestamp { get; set; }
        
        // Missing properties
        public string? ReferenceId { get; set; }
        public long DurationMs { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }
    
    public class ScopeNode { 
        public int Id { get; set; } 
        public string Name { get; set; } = ""; 
        public NodeType Type { get; set; } 
        public decimal RecursiveTotal { get; set; }
        public decimal RecursiveTotalCost { get; set; }
        // Missing Members
        public decimal SubTotal { get; set; }
        public bool IsExpanded { get; set; } // UI property
        public ScopeNode? Parent { get; set; }
        public virtual System.Collections.Generic.ICollection<ScopeNode> Children { get; set; } = new System.Collections.Generic.List<ScopeNode>();
        public virtual System.Collections.Generic.ICollection<ScopeNodeItem> Items { get; set; } = new System.Collections.Generic.List<ScopeNodeItem>();

        public void RestoreParentReferences() {}
        public void AddChild(ScopeNode child) {}
        public void AddChild(ScopeNodeItem item, int quantity) {} 
        public ScopeNode AddChild(string name, NodeType type) { return new ScopeNode(); } // New overload
        public void NotifyTotalsChanged() {}


        public void CopyItemsTo(ScopeNode other) {}
        public ScopeNode Clone() { return new ScopeNode(); }
        public ScopeNode Clone(string name) { return new ScopeNode { Name = name }; }
    }

    public class ScopeNodeItem { 
        public int Id { get; set; } 
        public string Name { get; set; } = ""; 
        // Missing
        public static ScopeNodeItem FromProduct(Product p) { return new ScopeNodeItem(); }
        // Added Props
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public Action OnItemChanged { get; set; } = delegate { }; // Mock event/delegate
    }

    public class StructureTreeItem { 
        public int Id { get; set; } 
        public string Name { get; set; } = ""; 
        public NodeType Type { get; set; }
        public void AddChild(StructureTreeItem item) {}
        // Overload returning Item
        public StructureTreeItem AddChild(string name, NodeType type) { return new StructureTreeItem(); }
    }

    public class CategorySelectItem { 
        public int Id { get; set; } 
        public string Name { get; set; } = "";
        // Missing
        public bool IsSelected { get; set; }
        public JobCategory Category { get; set; }
        public string DisplayName => Category.ToString();
    }

}

namespace KamatekCrm.Shared.Models.Specs
{
    public class ProductSpecBase { public int Id { get; set; } public string Notes { get; set; } = string.Empty; }
    public class CameraSpecs : ProductSpecBase { public string Resolution { get; set; } = ""; public string LensType { get; set; } = ""; public string CameraType { get; set; } = ""; public string IRDistance { get; set; } = ""; public string IpRating { get; set; } = ""; public string Compression { get; set; } = ""; public bool IsPoE { get; set; } public bool HasAudio { get; set; } public bool HasColorVu { get; set; } }
    public class IntercomSpecs : ProductSpecBase { public string ScreenSize { get; set; } = ""; public string ConnectionType { get; set; } = ""; public string MountingType { get; set; } = ""; public bool HasWiFi { get; set; } public bool HasMobileApp { get; set; } public bool HasMemory { get; set; } }
    public class FireAlarmSpecs : ProductSpecBase { public string DetectorType { get; set; } = ""; public string SystemType { get; set; } = ""; public string ComponentType { get; set; } = ""; public bool IsWireless { get; set; } public bool HasRelay { get; set; } }
    public class BurglarAlarmSpecs : ProductSpecBase { public string ConnectionType { get; set; } = ""; public string ComponentType { get; set; } = ""; public string DetectionType { get; set; } = ""; public bool HasGSM { get; set; } public bool HasWiFi { get; set; } public bool IsPetImmune { get; set; } }
    public class SmartHomeSpecs : ProductSpecBase { public string Protocol { get; set; } = ""; public string ModuleType { get; set; } = ""; public string LoadType { get; set; } = ""; public bool RequiresHub { get; set; } public bool HasSceneSupport { get; set; } }
    public class AccessControlSpecs : ProductSpecBase { public string ReaderFrequency { get; set; } = ""; public string ComponentType { get; set; } = ""; public string CommunicationType { get; set; } = ""; public bool IsWaterproof { get; set; } public bool HasFingerprint { get; set; } public bool HasFaceRecognition { get; set; } }
    public class SatelliteSpecs : ProductSpecBase { public string ComponentType { get; set; } = ""; public string DishDiameter { get; set; } = ""; public string LnbOutputs { get; set; } = ""; public string Material { get; set; } = ""; public bool IsMotorized { get; set; } public bool HasCardSlot { get; set; } }
    public class FiberSpecs : ProductSpecBase { public string FiberMode { get; set; } = ""; public string CoreCount { get; set; } = ""; public string CableType { get; set; } = ""; public string ConnectorType { get; set; } = ""; public bool IsArmored { get; set; } }
    public class GeneralSpecs : ProductSpecBase { public string Material { get; set; } = ""; public string Color { get; set; } = ""; public string Warranty { get; set; } = ""; }
}

namespace KamatekCrm.Shared.Models.JobDetails
{
    public class CctvJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
    public class VideoIntercomJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
    public class FireAlarmJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
    public class BurglarAlarmJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
    public class SmartHomeJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
    public class AccessControlJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
    public class SatelliteJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
    public class FiberOpticJobDetail : KamatekCrm.Shared.Models.JobDetailBase { }
}
