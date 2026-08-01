using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IServiceJobCommandService
{
    Task<Result<ServiceJobSaveResult>> SaveAsync(
        ServiceJobSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ServiceJobStatusChangeResult>> ChangeStatusAsync(
        int jobId,
        JobStatus requestedStatus,
        string changedBy,
        CancellationToken cancellationToken = default);

    Task<Result<ServiceJobStatusChangeResult>> CompleteAsync(
        int jobId,
        decimal? laborCost,
        decimal? discountAmount,
        string? completionNote,
        string changedBy,
        CancellationToken cancellationToken = default);

    Task<Result<ServiceJobQuoteConversionResult>> ConvertToQuoteAsync(
        int jobId,
        string changedBy,
        CancellationToken cancellationToken = default);
}
