using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Transactions;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface ITransactionReadService
{
    Task<Result<IReadOnlyList<WarehouseLookupDto>>> GetActiveWarehousesAsync(
        bool includeQuarantine,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PurchaseHistoryDto>>> GetPurchaseHistoryAsync(
        int take = 150,
        CancellationToken cancellationToken = default);

    Task<Result<SalesReturnReceiptDto>> GetSalesReturnReceiptAsync(
        int salesReturnId,
        CancellationToken cancellationToken = default);

    Task<Result<PurchasingWorkspaceDto>> GetPurchasingWorkspaceAsync(
        int historyTake = 50,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PurchaseProductLookupDto>>> SearchPurchaseProductsAsync(
        string searchText,
        int take = 10,
        CancellationToken cancellationToken = default);
}
