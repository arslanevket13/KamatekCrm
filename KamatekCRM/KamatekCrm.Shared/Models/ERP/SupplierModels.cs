using System;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
    public class Supplier : KamatekCrm.Shared.Models.Common.BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public bool IsActive { get; set; } = true;
        public virtual System.Collections.Generic.ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new System.Collections.Generic.List<PurchaseOrder>();
    }

    public class PurchaseOrder : KamatekCrm.Shared.Models.Common.BaseEntity
    {
        public int SupplierId { get; set; }
        public PurchaseStatus Status { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public DateTime OrderDate { get; set; } = DateTime.Now;
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

    public class PurchaseInvoice : KamatekCrm.Shared.Models.Common.BaseEntity
    {
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string InvoiceNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public decimal SubTotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public PurchaseStatus Status { get; set; }
        public PurchaseInvoicePaymentStatus PaymentStatus { get; set; } = PurchaseInvoicePaymentStatus.Unpaid;
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string? OcrRawText { get; set; }
        public string? Notes { get; set; }
        [ForeignKey(nameof(SupplierId))]
        public virtual Supplier Supplier { get; set; } = null!;
        public virtual System.Collections.Generic.ICollection<PurchaseInvoiceLine> Lines { get; set; } = new System.Collections.Generic.List<PurchaseInvoiceLine>();
    }

    public class PurchaseInvoiceLine
    {
        public int Id { get; set; }
        public int PurchaseInvoiceId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public int VatRate { get; set; }
        public decimal VatAmount { get; set; }
        public decimal LineTotal { get; set; }
        public decimal OldAverageCost { get; set; }
        public decimal NewAverageCost { get; set; }
        [ForeignKey(nameof(PurchaseInvoiceId))]
        public virtual PurchaseInvoice PurchaseInvoice { get; set; } = null!;
        [ForeignKey(nameof(ProductId))]
        public virtual Product Product { get; set; } = null!;
    }
}
