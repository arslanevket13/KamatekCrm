using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Quotes;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IStandardQuoteCommandService
{
    Task<Result<StandardQuoteSaveResult>> SaveAsync(
        SaveStandardQuoteCommand command,
        CancellationToken cancellationToken = default);
}
