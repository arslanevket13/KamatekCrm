using System;

namespace KamatekCrm.Events
{
    /// <summary>
    /// Satış tamamlandığında yayınlanan event
    /// </summary>
    public class SaleCompletedEvent
    {
        public string OrderNumber { get; }
        public decimal TotalAmount { get; }
        public int WarehouseId { get; }
        public DateTime CompletedAt { get; }
        public int ItemCount { get; }

        public SaleCompletedEvent(string orderNumber, decimal totalAmount, int warehouseId, int itemCount)
        {
            OrderNumber = orderNumber;
            TotalAmount = totalAmount;
            WarehouseId = warehouseId;
            ItemCount = itemCount;
            CompletedAt = DateTime.Now;
        }
    }
}
