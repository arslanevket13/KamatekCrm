using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IAuditTrailService
{
    Task<Result> WriteAsync(
        AuditActionType actionType,
        string? entityName = null,
        string? recordId = null,
        string? description = null,
        string? additionalData = null,
        CancellationToken cancellationToken = default);
}
