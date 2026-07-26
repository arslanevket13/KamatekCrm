namespace KamatekCrm.Shared.DTOs
{
    /// <summary>
    /// DTO sent from WPF → API to process a purchase invoice
    /// </summary>
    public class PurchaseInvoiceDto
    {
        public int SupplierId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }
        public string? OcrRawText { get; set; }
        public List<PurchaseInvoiceLineDto> Lines { get; set; } = new();
    }

    public class PurchaseInvoiceLineDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public int VatRate { get; set; }
    }

    /// <summary>
    /// API → WPF response after successful purchase invoice processing
    /// </summary>
    public class PurchaseInvoiceResultDto
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal GrandTotal { get; set; }
        public decimal SupplierNewBalance { get; set; }
        public DateTime Date { get; set; }
    }
}
