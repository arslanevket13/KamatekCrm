namespace KamatekCrm.Services.Domain
{
    /// <summary>
    /// Stok işlemlerini yöneten domain service interface
    /// </summary>
    public interface IInventoryDomainService
    {
        /// <summary>
        /// Depolar arası stok transferi gerçekleştirir (Transaction içinde)
        /// </summary>
        /// <param name="request">Transfer isteği detayları</param>
        /// <returns>Transfer sonucu</returns>
        TransferResult TransferStock(TransferRequest request);

        /// <summary>
        /// Belirli bir ürünün belirli bir depodaki stok miktarını döndürür
        /// </summary>
        int GetAvailableStock(int productId, int warehouseId);

        /// <summary>
        /// Stok miktarını günceller ve StockTransaction kaydı oluşturur
        /// </summary>
        void AdjustStock(StockAdjustmentRequest request);
    }

    /// <summary>
    /// Transfer isteği DTO
    /// </summary>
    public class TransferRequest
    {
        public int ProductId { get; set; }
        public int SourceWarehouseId { get; set; }
        public int TargetWarehouseId { get; set; }
        public int Quantity { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// Transfer sonucu DTO
    /// </summary>
    public class TransferResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int TransactionId { get; set; }

        public static TransferResult Ok(int transactionId)
            => new() { Success = true, TransactionId = transactionId };

        public static TransferResult Fail(string error)
            => new() { Success = false, ErrorMessage = error };
    }

    /// <summary>
    /// Stok düzeltme isteği DTO
    /// </summary>
    public class StockAdjustmentRequest
    {
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public int QuantityChange { get; set; } // Pozitif: artış, Negatif: azalış
        public string Reason { get; set; } = string.Empty;
        public string? ReferenceId { get; set; }
    }
}
