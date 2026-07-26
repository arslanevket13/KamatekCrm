using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
    public class Quote
    {
        public int Id { get; set; }
        public string QuoteNumber { get; set; } = string.Empty;
        
        public int? CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }
        
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public DateTime ValidUntil { get; set; } = DateTime.UtcNow.AddDays(15);
        
        public QuoteStatus Status { get; set; } = QuoteStatus.Draft;
        
        public decimal SubTotal { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal TotalTax { get; set; }
        public decimal GrandTotal { get; set; }
        
        public string TermsAndConditions { get; set; } = string.Empty;
        
        public virtual ICollection<QuoteLine> Lines { get; set; } = new List<QuoteLine>();
    }

    public class QuoteLine
    {
        public int Id { get; set; }
        
        public int QuoteId { get; set; }
        [ForeignKey(nameof(QuoteId))]
        public virtual Quote Quote { get; set; } = null!;
        
        public int ProductId { get; set; }
        [ForeignKey(nameof(ProductId))]
        public virtual Product Product { get; set; } = null!;
        
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        
        public int Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal TaxPercent { get; set; }
        
        public decimal LineTotal { get; set; }
        
        [NotMapped]
        public int CurrentStockQuantity { get; set; }
    }
}
