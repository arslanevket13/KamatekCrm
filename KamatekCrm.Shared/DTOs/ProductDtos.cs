namespace KamatekCrm.Shared.DTOs
{
    /// <summary>
    /// Lightweight product DTO for search/listing (POS barcode scan, dropdowns)
    /// </summary>
    public class ProductListDto
    {
        public int Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public decimal SalePrice { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal AverageCost { get; set; }
        public int VatRate { get; set; }
        public int TotalStockQuantity { get; set; }
        public string? ImageUrl { get; set; }
    }

    /// <summary>
    /// API → WPF response after image upload
    /// </summary>
    public class ProductImageUploadResultDto
    {
        public int ProductId { get; set; }
        public string RelativePath { get; set; } = string.Empty;
        public string FullUrl { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
    }
}
