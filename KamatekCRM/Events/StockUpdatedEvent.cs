using System;
using System.Collections.Generic;
using System.Linq;

namespace KamatekCrm.Events
{
    /// <summary>
    /// Stok güncellendiğinde yayınlanan event (Tekil ve Toplu/Batch desteği ile)
    /// </summary>
    public class StockUpdatedEvent
    {
        public int ProductId { get; }
        public int WarehouseId { get; }
        public int OldQuantity { get; }
        public int NewQuantity { get; }
        public string Reason { get; }
        public DateTime UpdatedAt { get; }
        public IReadOnlyList<int> AffectedProductIds { get; }
        public bool IsBatch { get; }

        public StockUpdatedEvent(int productId, int warehouseId, int oldQuantity, int newQuantity, string reason)
        {
            ProductId = productId;
            WarehouseId = warehouseId;
            OldQuantity = oldQuantity;
            NewQuantity = newQuantity;
            Reason = reason;
            UpdatedAt = DateTime.UtcNow;
            AffectedProductIds = new[] { productId };
            IsBatch = false;
        }

        public StockUpdatedEvent(IEnumerable<int> affectedProductIds, int warehouseId, string reason)
        {
            AffectedProductIds = affectedProductIds?.Distinct().ToList() ?? new List<int>();
            ProductId = AffectedProductIds.Count == 1 ? AffectedProductIds[0] : 0;
            WarehouseId = warehouseId;
            OldQuantity = 0;
            NewQuantity = 0;
            Reason = reason;
            UpdatedAt = DateTime.UtcNow;
            IsBatch = true;
        }
    }
}
