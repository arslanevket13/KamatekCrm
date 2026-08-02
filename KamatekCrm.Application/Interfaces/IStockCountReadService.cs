using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Inventory;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IStockCountReadService
{
    Task<Result<IReadOnlyList<StockCountWarehouseDto>>> GetWarehousesAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<StockCountProductDto>>> GetWarehouseSnapshotAsync(int warehouseId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<StockCountProductDto>>> SearchProductsAsync(int warehouseId, string searchText, int take = 15, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<StockCountHistoryDto>>> GetHistoryAsync(int take = 30, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<StockCountHistoryLineDto>>> GetHistoryDetailAsync(int? sessionId, string referenceNumber, CancellationToken cancellationToken = default);
}
