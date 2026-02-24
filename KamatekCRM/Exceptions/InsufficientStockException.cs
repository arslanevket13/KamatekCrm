using System;

namespace KamatekCrm.Exceptions
{
    /// <summary>
    /// Yetersiz stok durumunda fırlatılan özel exception
    /// </summary>
    public class InsufficientStockException : Exception
    {
        public int ProductId { get; }
        public string? ProductName { get; }
        public int RequestedQuantity { get; }
        public int AvailableQuantity { get; }
        public int WarehouseId { get; }

        public InsufficientStockException(int productId, string productName, int requestedQuantity, int availableQuantity, int warehouseId)
            : base($"Yetersiz stok: '{productName}' için {requestedQuantity} adet istendi, mevcut: {availableQuantity}")
        {
            ProductId = productId;
            ProductName = productName;
            RequestedQuantity = requestedQuantity;
            AvailableQuantity = availableQuantity;
            WarehouseId = warehouseId;
        }

        public InsufficientStockException(string message) : base(message)
        {
        }

        public InsufficientStockException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
