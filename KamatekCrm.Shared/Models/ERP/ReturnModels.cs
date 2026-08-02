using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models;

public class SalesReturn
{
    public int Id { get; set; }
    public int SalesOrderId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public ReturnStatus Status { get; set; } = ReturnStatus.Completed;
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public int PrintCount { get; set; }
    public virtual SalesOrder SalesOrder { get; set; } = null!;
    public virtual ICollection<SalesReturnItem> Items { get; set; } = new List<SalesReturnItem>();
    public virtual ICollection<SalesReturnPayment> Payments { get; set; } = new List<SalesReturnPayment>();
}

public class SalesReturnItem
{
    public int Id { get; set; }
    public int SalesReturnId { get; set; }
    public int SalesOrderItemId { get; set; }
    public int ProductId { get; set; }
    public int DestinationWarehouseId { get; set; }
    public int Quantity { get; set; }
    public ReturnDisposition Disposition { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public virtual SalesReturn SalesReturn { get; set; } = null!;
    public virtual SalesOrderItem SalesOrderItem { get; set; } = null!;
}

public class SalesReturnPayment
{
    public int Id { get; set; }
    public int SalesReturnId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public string Reference { get; set; } = string.Empty;
    public virtual SalesReturn SalesReturn { get; set; } = null!;
}

public class PurchaseReturn
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public ReturnStatus Status { get; set; } = ReturnStatus.Completed;
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentMethod SettlementMethod { get; set; }
    public string SettlementReference { get; set; } = string.Empty;
    public bool LegacySettlementOverride { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    public virtual ICollection<PurchaseReturnItem> Items { get; set; } = new List<PurchaseReturnItem>();
}

public class PurchaseReturnItem
{
    public int Id { get; set; }
    public int PurchaseReturnId { get; set; }
    public int PurchaseOrderItemId { get; set; }
    public int ProductId { get; set; }
    public int SourceWarehouseId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
    public virtual PurchaseReturn PurchaseReturn { get; set; } = null!;
    public virtual PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;
}
