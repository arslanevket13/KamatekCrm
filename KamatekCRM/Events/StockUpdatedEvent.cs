using System;

namespace KamatekCrm.Events
{
    /// <summary>
    /// Stok güncellendiğinde yayınlanan event
    /// </summary>
    public class StockUpdatedEvent
    {
        public int ProductId { get; }
        public int WarehouseId { get; }
        public int OldQuantity { get; }
        public int NewQuantity { get; }
        public string Reason { get; }
        public DateTime UpdatedAt { get; }

        public StockUpdatedEvent(int productId, int warehouseId, int oldQuantity, int newQuantity, string reason)
        {
            ProductId = productId;
            WarehouseId = warehouseId;
            OldQuantity = oldQuantity;
            NewQuantity = newQuantity;
            Reason = reason;
            UpdatedAt = DateTime.Now;
        }
    }
}
