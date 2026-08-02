using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Infrastructure.Services;

public sealed class AuditTrailService : IAuditTrailService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ICurrentUserContext _currentUser;

    public AuditTrailService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ICurrentUserContext currentUser)
    {
        _dbContextFactory = dbContextFactory;
        _currentUser = currentUser;
    }

    public async Task<Result> WriteAsync(
        AuditActionType actionType,
        string? entityName = null,
        string? recordId = null,
        string? description = null,
        string? additionalData = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            context.ActivityLogs.Add(new ActivityLog
            {
                UserId = _currentUser.UserId,
                Username = _currentUser.IsAuthenticated ? _currentUser.Username : "System/Anonymous",
                ActionType = actionType.ToString(),
                Action = actionType.ToString(),
                EntityName = entityName,
                RecordId = recordId,
                ReferenceId = recordId,
                Description = description,
                AdditionalData = additionalData,
                Timestamp = DateTime.UtcNow,
                DurationMs = 0,
                IpAddress = "127.0.0.1",
                UserAgent = "WPF Client"
            });
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception)
        {
            return Result.Failure($"Audit kaydı yazılamadı: {exception.Message}");
        }
    }
}
