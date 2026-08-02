using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Inventory;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IStockCountCommandService
{
    Task<Result<StockCountResult>> ApplyAsync(
        ApplyStockCountCommand command,
        CancellationToken cancellationToken = default);
}
