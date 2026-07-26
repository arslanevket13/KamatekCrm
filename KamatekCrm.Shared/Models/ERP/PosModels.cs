using System;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
    public class PosTransaction : KamatekCrm.Shared.Models.Common.BaseEntity
    {
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string TransactionNumber { get; set; } = string.Empty;
        
        /// <summary>
        /// Fiş numarası (müşteriye verilen)
        /// </summary>
        public string ReceiptNumber { get; set; } = string.Empty;
        
        /// <summary>
        /// Fiş tekrar yazdırıldı mı?
        /// </summary>
        public bool IsReprinted { get; set; }
        
        /// <summary>
        /// Kaç kez yazdırıldı
        /// </summary>
        public int PrintCount { get; set; }
        
        public decimal SubTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public decimal CashAmount { get; set; }
        public decimal CardAmount { get; set; }
        public PosTransactionStatus Status { get; set; } = PosTransactionStatus.Completed;
        public string? Notes { get; set; }
        public int? CashierUserId { get; set; }
        [ForeignKey(nameof(CashierUserId))]
        public virtual User? CashierUser { get; set; }
        public int? CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }
        public virtual System.Collections.Generic.ICollection<PosTransactionLine> Lines { get; set; } = new System.Collections.Generic.List<PosTransactionLine>();
    }

    public class PosTransactionLine
    {
        public int Id { get; set; }
        public int PosTransactionId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public DiscountType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal DiscountAmount { get; set; }
        public int VatRate { get; set; }
        public decimal VatAmount { get; set; }
        public decimal NetTotal { get; set; }
        public decimal LineTotal { get; set; }
        [ForeignKey(nameof(PosTransactionId))]
        public virtual PosTransaction PosTransaction { get; set; } = null!;
        [ForeignKey(nameof(ProductId))]
        public virtual Product Product { get; set; } = null!;
    }
}
