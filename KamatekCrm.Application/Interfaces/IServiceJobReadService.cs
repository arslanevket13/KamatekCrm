using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IServiceJobReadService
{
    Task<Result<ServiceJobWorkspaceDto>> GetWorkspaceAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ServiceJobRowDto>>> SearchAsync(ServiceJobSearchRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ServiceJobAssetLookupDto>>> GetCustomerAssetsAsync(int customerId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ServiceJobProjectLookupDto>>> GetCustomerProjectsAsync(int customerId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ServiceJobMaterialDto>>> GetMaterialsAsync(int jobId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ServiceJobHistoryDto>>> GetHistoryAsync(int jobId, CancellationToken cancellationToken = default);
    Task<Result<ServiceJobDashboardDto>> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<Result<ServiceJobDocumentDto>> GetDocumentAsync(int jobId, CancellationToken cancellationToken = default);
}
