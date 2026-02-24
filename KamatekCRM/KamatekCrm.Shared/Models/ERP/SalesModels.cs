using System;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
    public class SalesOrder
    {
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
        
        /// <summary>
        /// Fiş tekrar yazdırıldı mı?
        /// </summary>
        public bool IsReprinted { get; set; }
        
        /// <summary>
        /// Kaç kez yazdırıldı
        /// </summary>
        public int PrintCount { get; set; }
        
        public virtual Customer Customer { get; set; } = null!;
        public virtual System.Collections.Generic.ICollection<SalesOrderItem> Items { get; set; } = new System.Collections.Generic.List<SalesOrderItem>();
        public virtual System.Collections.Generic.ICollection<SalesOrderPayment> Payments { get; set; } = new System.Collections.Generic.List<SalesOrderPayment>();
    }

    public class SalesOrderItem
    {
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
    public class SalesOrderPayment
    {
        public int Id { get; set; }
        public int SalesOrderId { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
        [ForeignKey(nameof(SalesOrderId))]
        public virtual SalesOrder SalesOrder { get; set; } = null!;
    }

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
        public int? CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }
    }
}
