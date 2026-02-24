using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Services.Domain
{
    /// <summary>
    /// Stok işlemlerini yöneten domain service interface
    /// </summary>
    public interface IInventoryDomainService
    {
        // === SYNC METODLAR ===
        
        /// <summary>
        /// Depolar arası stok transferi gerçekleştirir (Transaction içinde)
        /// </summary>
        TransferResult TransferStock(TransferRequest request);

        /// <summary>
        /// Belirli bir ürünün belirli bir depodaki stok miktarını döndürür
        /// </summary>
        int GetAvailableStock(int productId, int warehouseId);

        /// <summary>
        /// Stok miktarını günceller ve StockTransaction kaydı oluşturur
        /// </summary>
        void AdjustStock(StockAdjustmentRequest request);

        /// <summary>
        /// Yeni stok girişi yapar (Satın Alma) ve Maliyet Hesaplar
        /// </summary>
        void AddStock(int productId, int warehouseId, int quantity, decimal unitCost, string referenceId);

        // === ASYNC METODLAR ===

        /// <summary>
        /// Async depolar arası stok transferi
        /// </summary>
        Task<TransferResult> TransferStockAsync(TransferRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Async stok sorgulama
        /// </summary>
        Task<int> GetAvailableStockAsync(int productId, int warehouseId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Async stok düzeltme
        /// </summary>
        Task AdjustStockAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Async stok girişi
        /// </summary>
        Task AddStockAsync(int productId, int warehouseId, int quantity, decimal unitCost, string referenceId, CancellationToken cancellationToken = default);

        // === STOK DEĞERLEME VE RAPORLAMA ===

        /// <summary>
        /// Toplam stok değerini döndürür (WAC bazlı)
        /// </summary>
        Task<decimal> GetTotalInventoryValueAsync(int? warehouseId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Kritik stok seviyesindeki ürünleri getirir
        /// </summary>
        Task<IEnumerable<LowStockProduct>> GetLowStockProductsAsync(int? warehouseId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stok hareketlerini sorgular
        /// </summary>
        Task<IEnumerable<StockTransaction>> GetStockTransactionsAsync(StockTransactionQuery query, CancellationToken cancellationToken = default);

        // === STOK REZERVASYON ===

        /// <summary>
        /// Stok rezerve eder (sipariş için ayırır)
        /// </summary>
        Task<ReservationResult> ReserveStockAsync(StockReservationRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rezervasyonu iptal eder
        /// </summary>
        Task<bool> CancelReservationAsync(int reservationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mevcut rezerve miktarını döndürür
        /// </summary>
        Task<int> GetReservedQuantityAsync(int productId, int warehouseId, CancellationToken cancellationToken = default);

        // === STOK GÖRSELLERİ ===

        /// <summary>
        /// Stok görseli ekler
        /// </summary>
        Task<InventoryImage> AddInventoryImageAsync(InventoryImageRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stok görsellerini getirir
        /// </summary>
        Task<IEnumerable<InventoryImage>> GetInventoryImagesAsync(int productId, int warehouseId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stok görselini siler
        /// </summary>
        Task<bool> DeleteInventoryImageAsync(int imageId, CancellationToken cancellationToken = default);

        // === BATCH OPERATIONS ===

        /// <summary>
        /// Toplu stok girişi (birden fazla ürün)
        /// </summary>
        Task<BatchOperationResult> AddStockBatchAsync(IEnumerable<BatchStockEntry> entries, CancellationToken cancellationToken = default);

        // === SON KULLANMA TARİHİ ===

        /// <summary>
        /// Son kullanma tarihi yaklaşan ürünleri getirir
        /// </summary>
        Task<IEnumerable<ExpiringStock>> GetExpiringStockAsync(int daysThreshold = 30, int? warehouseId = null, CancellationToken cancellationToken = default);
    }

    // === DTOs ===

    public class TransferRequest
    {
        public int ProductId { get; set; }
        public int SourceWarehouseId { get; set; }
        public int TargetWarehouseId { get; set; }
        public int Quantity { get; set; }
        public string? Description { get; set; }
    }

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

    public class StockAdjustmentRequest
    {
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public int QuantityChange { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? ReferenceId { get; set; }
    }

    public class LowStockProduct
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int MinStockLevel { get; set; }
        public int Deficiency => MinStockLevel - CurrentStock;
    }

    public class StockTransactionQuery
    {
        public int? ProductId { get; set; }
        public int? WarehouseId { get; set; }
        public StockTransactionType? TransactionType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class StockReservationRequest
    {
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public int Quantity { get; set; }
        public string ReferenceType { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
    }

    public class ReservationResult
    {
        public bool Success { get; set; }
        public int ReservationId { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public static ReservationResult Ok(int reservationId)
            => new() { Success = true, ReservationId = reservationId };

        public static ReservationResult Fail(string error)
            => new() { Success = false, ErrorMessage = error };
    }

    public class InventoryImageRequest
    {
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public string? Description { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
    }

    public class BatchStockEntry
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public string ReferenceId { get; set; } = string.Empty;
    }

    public class BatchOperationResult
    {
        public bool Success { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class ExpiringStock
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public int DaysUntilExpiry { get; set; }
        public int Quantity { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
    }
}
