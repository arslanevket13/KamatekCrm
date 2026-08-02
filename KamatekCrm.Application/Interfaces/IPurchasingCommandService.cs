using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Transactions;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IPurchasingCommandService
{
    Task<Result<PurchaseCommandResult>> CreatePurchaseAsync(CreatePurchaseCommand command, CancellationToken cancellationToken = default);
    Task<Result<PurchaseCommandResult>> ReceivePurchaseAsync(ReceivePurchaseCommand command, CancellationToken cancellationToken = default);
    Task<Result> CancelPurchaseAsync(CancelPurchaseCommand command, CancellationToken cancellationToken = default);
    Task<Result<ReturnablePurchaseDto>> GetReturnablePurchaseAsync(int purchaseOrderId, CancellationToken cancellationToken = default);
    Task<Result<ReturnTransactionResult>> ReturnPurchaseAsync(ReturnPurchaseCommand command, CancellationToken cancellationToken = default);
}
