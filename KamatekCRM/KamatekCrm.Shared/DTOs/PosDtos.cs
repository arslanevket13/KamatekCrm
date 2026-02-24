namespace KamatekCrm.Shared.DTOs
{
    /// <summary>
    /// DTO sent from WPF → API to process a POS sale
    /// </summary>
    public class PosTransactionDto
    {
        public int? CustomerId { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public decimal CashAmount { get; set; }
        public decimal CardAmount { get; set; }
        public string? Notes { get; set; }
        public List<PosTransactionLineDto> Lines { get; set; } = new();
    }

    public class PosTransactionLineDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string DiscountType { get; set; } = "Percentage"; // Percentage | FlatAmount
        public decimal DiscountValue { get; set; }
        public int VatRate { get; set; }
    }

    /// <summary>
    /// API → WPF response after successful POS transaction
    /// </summary>
    public class PosTransactionResultDto
    {
        public int TransactionId { get; set; }
        public string TransactionNumber { get; set; } = string.Empty;
        public decimal GrandTotal { get; set; }
        public decimal ChangeAmount { get; set; }
        public DateTime Date { get; set; }
    }
}
