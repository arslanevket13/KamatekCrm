using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Tests;

internal sealed class TestAuditTrailService : IAuditTrailService
{
    public List<(AuditActionType Action, string? Entity, string? RecordId)> Entries { get; } = new();

    public Task<Result> WriteAsync(
        AuditActionType actionType,
        string? entityName = null,
        string? recordId = null,
        string? description = null,
        string? additionalData = null,
        CancellationToken cancellationToken = default)
    {
        Entries.Add((actionType, entityName, recordId));
        return Task.FromResult(Result.Success());
    }
}
