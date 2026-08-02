using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Quotes;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IStandardQuoteReadService
{
    Task<Result<StandardQuoteWorkspaceDto>> GetWorkspaceAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<StandardQuoteProductDto>>> SearchProductsAsync(
        string searchText,
        int take = 20,
        CancellationToken cancellationToken = default);
    Task<Result<StandardQuoteDocumentDto>> GetDocumentAsync(
        int quoteId,
        CancellationToken cancellationToken = default);
}
