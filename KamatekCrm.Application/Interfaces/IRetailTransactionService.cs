using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Transactions;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IRetailTransactionService
{
    Task<Result<SaleTransactionResult>> CompleteSaleAsync(CompleteSaleCommand command, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SaleSummaryDto>>> SearchSalesAsync(SaleSearchQuery query, CancellationToken cancellationToken = default);
    Task<Result<ReturnableSaleDto>> GetReturnableSaleAsync(int salesOrderId, CancellationToken cancellationToken = default);
    Task<Result<ReturnTransactionResult>> ReturnSaleAsync(ReturnSaleCommand command, CancellationToken cancellationToken = default);
    Task<Result<LegacyLedgerPreviewDto>> PreviewLegacyLedgerAsync(CancellationToken cancellationToken = default);
    Task<Result<int>> ApplyLegacyLedgerCorrectionsAsync(IReadOnlyCollection<string> reconciliationKeys, string appliedBy, CancellationToken cancellationToken = default);
}
