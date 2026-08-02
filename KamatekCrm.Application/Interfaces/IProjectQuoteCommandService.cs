using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ProjectQuotes;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IProjectQuoteCommandService
{
    Task<Result<ProjectQuoteSaveResult>> SaveAsync(
        SaveProjectQuoteCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<ProjectQuoteOperationResult>> ChangeStatusAsync(
        ChangeProjectQuoteStatusCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<ProjectQuoteDuplicateResult>> DuplicateAsync(
        DuplicateProjectQuoteCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<ProjectQuoteOperationResult>> DeleteDraftAsync(
        DeleteProjectQuoteCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<ProjectQuoteWorkOrderResult>> ConvertApprovedToWorkOrderAsync(
        ConvertApprovedQuoteToWorkOrderCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<ExpireProjectQuotesResult>> ExpireOverdueAsync(
        CancellationToken cancellationToken = default);
}
