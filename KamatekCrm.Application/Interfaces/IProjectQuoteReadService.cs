using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ProjectQuotes;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IProjectQuoteReadService
{
    Task<Result<ProjectQuoteWorkspaceDto>> GetWorkspaceAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectQuoteListItemDto>>> GetListAsync(CancellationToken cancellationToken = default);
    Task<Result<ProjectQuoteDetailDto>> GetAsync(int projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectQuoteExportDto>> GetExportAsync(int projectId, CancellationToken cancellationToken = default);
}
