using System.Data;
using System.Text.Json;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ProjectQuotes;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KamatekCrm.Infrastructure.Services;

public sealed class ProjectQuoteCommandService : IProjectQuoteCommandService
{
    private const string AuditEntity = "ServiceProject";
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IApplicationAuthorizationService _authorization;
    private readonly ICurrentUserContext _currentUser;

    public ProjectQuoteCommandService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IApplicationAuthorizationService authorization,
        ICurrentUserContext currentUser)
    {
        _dbContextFactory = dbContextFactory;
        _authorization = authorization;
        _currentUser = currentUser;
    }

    public async Task<Result<ProjectQuoteSaveResult>> SaveAsync(
        SaveProjectQuoteCommand command,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ManageQuotes);
        if (authorization.IsFailure)
            return Result.Failure<ProjectQuoteSaveResult>(authorization.Error);
        if (command.IdempotencyKey == Guid.Empty)
            return Result.Failure<ProjectQuoteSaveResult>("İşlem anahtarı oluşturulamadı.");
        if (command.CustomerId <= 0)
            return Result.Failure<ProjectQuoteSaveResult>("Müşteri seçilmelidir.");
        var title = command.Title.Trim();
        if (title.Length is < 2 or > 200)
            return Result.Failure<ProjectQuoteSaveResult>("Proje adı 2 ile 200 karakter arasında olmalıdır.");

        var pricingResult = ProjectQuotePricingPolicy.Calculate(
            command.ProjectScopeJson, command.DiscountPercent, command.KdvRate);
        if (pricingResult.IsFailure || pricingResult.Value is null)
            return Result.Failure<ProjectQuoteSaveResult>(pricingResult.Error);
        var pricing = pricingResult.Value;
        if (pricing.IncludedLineCount == 0)
            return Result.Failure<ProjectQuoteSaveResult>("Teklifte en az bir zorunlu kalem bulunmalıdır.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
            var operationReference = OperationReference(command.IdempotencyKey);
            var previousOperation = await context.ActivityLogs
                .AsNoTracking()
                .SingleOrDefaultAsync(log => log.EntityName == AuditEntity && log.ReferenceId == operationReference,
                    cancellationToken);
            if (previousOperation is not null && int.TryParse(previousOperation.RecordId, out var previousProjectId))
            {
                var replay = await BuildReplayResultAsync(context, previousProjectId, pricing, cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return replay;
            }

            if (!await context.Customers.AnyAsync(customer => customer.Id == command.CustomerId, cancellationToken))
                return await FailAsync<ProjectQuoteSaveResult>(transaction, "Seçilen müşteri artık mevcut değil.", cancellationToken);

            ServiceProject project;
            var isNew = command.ProjectId is null or <= 0;
            var wasNoOp = false;
            var now = DateTime.UtcNow;

            if (isNew)
            {
                project = new ServiceProject
                {
                    Title = title,
                    Name = title,
                    CustomerId = command.CustomerId,
                    CreatedDate = now,
                    ProjectCode = await NextNumberAsync(context, "PRJ", now.Year, cancellationToken),
                    QuoteNumber = await NextNumberAsync(context, "TEK", now.Year, cancellationToken),
                    RevisionNumber = 1,
                    QuoteStatus = QuoteStatus.Draft,
                    Status = ProjectStatus.Draft
                };
                context.ServiceProjects.Add(project);
            }
            else
            {
                project = await context.ServiceProjects.SingleOrDefaultAsync(
                    item => item.Id == command.ProjectId!.Value, cancellationToken)
                    ?? throw new ProjectQuoteValidationException("Güncellenecek proje teklifi bulunamadı.");

                if (project.RevisionNumber != command.ExpectedRevisionNumber)
                    throw new ProjectQuoteValidationException(
                        $"Teklif başka bir kullanıcı tarafından R{project.RevisionNumber} olarak güncellendi. " +
                        "Verileri yenileyip değişikliklerinizi yeniden uygulayın.");

                wasNoOp = string.Equals(project.Title.Trim(), title, StringComparison.Ordinal) &&
                          project.CustomerId == command.CustomerId &&
                          project.DiscountPercent == command.DiscountPercent &&
                          project.KdvRate == command.KdvRate &&
                          BusinessScopeEquals(project.ProjectScopeJson, command.ProjectScopeJson);

                if (!wasNoOp)
                {
                    var revisions = DeserializeRevisions(project.RevisionsJson);
                    revisions.Add(new QuoteRevision
                    {
                        RevisionNumber = project.RevisionNumber,
                        CreatedDate = now,
                        ChangeDescription = $"R{project.RevisionNumber} arşivlendi; R{project.RevisionNumber + 1} oluşturuldu",
                        TotalBudget = project.TotalBudget,
                        DiscountPercent = project.DiscountPercent,
                        ScopeSnapshotJson = project.ProjectScopeJson
                    });
                    project.RevisionsJson = JsonSerializer.Serialize(revisions);
                    project.RevisionNumber++;
                    if (project.QuoteStatus != QuoteStatus.Draft)
                    {
                        project.QuoteStatus = QuoteStatus.Revised;
                        project.PipelineStage = PipelineStage.Negotiation;
                        project.Status = ProjectStatus.Draft;
                        project.SentDate = null;
                        project.ValidUntil = null;
                        project.ApprovedDate = null;
                        project.RejectedDate = null;
                        project.RejectionReason = null;
                    }
                }
            }

            if (!wasNoOp)
            {
                project.Title = title;
                project.Name = string.IsNullOrWhiteSpace(project.Name) ? title : project.Name;
                project.CustomerId = command.CustomerId;
                project.ProjectScopeJson = command.ProjectScopeJson;
                project.DiscountPercent = command.DiscountPercent;
                project.KdvRate = command.KdvRate;
                project.TotalBudget = pricing.NetRevenue;
                project.TotalCost = pricing.TotalCost;
                project.TotalProfit = pricing.TotalProfit;
            }

            await context.SaveChangesAsync(cancellationToken);
            context.ActivityLogs.Add(new ActivityLog
            {
                UserId = _currentUser.UserId,
                Username = _currentUser.Username,
                Action = isNew ? "ProjectQuoteCreated" : wasNoOp ? "ProjectQuoteSaveNoOp" : "ProjectQuoteRevised",
                ActionType = isNew ? "Create" : "Update",
                EntityName = AuditEntity,
                RecordId = project.Id.ToString(),
                ReferenceId = operationReference,
                Description = isNew
                    ? $"{project.QuoteNumber} numaralı teklif oluşturuldu."
                    : wasNoOp
                        ? $"{project.QuoteNumber} numaralı teklifte değişiklik bulunmadı."
                        : $"{project.QuoteNumber} numaralı teklif R{project.RevisionNumber} olarak kaydedildi.",
                AdditionalData = JsonSerializer.Serialize(new
                {
                    command.IdempotencyKey,
                    project.RevisionNumber,
                    pricing.NetRevenue,
                    pricing.GrandTotal
                }),
                Timestamp = now,
                UserAgent = "WPF Client"
            });
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);

            return Result.Success(new ProjectQuoteSaveResult(
                project.Id, project.ProjectCode, project.QuoteNumber ?? string.Empty,
                project.RevisionNumber, project.QuoteStatus, pricing, false, wasNoOp));
        }
        catch (ProjectQuoteValidationException exception)
        {
            return await FailAsync<ProjectQuoteSaveResult>(transaction, exception.Message, cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            return await FailAsync<ProjectQuoteSaveResult>(transaction,
                $"Teklif kaydı veritabanına yazılamadı: {exception.GetBaseException().Message}", cancellationToken);
        }
        catch (Exception exception)
        {
            return await FailAsync<ProjectQuoteSaveResult>(transaction,
                $"Teklif kaydı tamamlanamadı: {exception.Message}", cancellationToken);
        }
    }, IsolationLevel.Serializable, cancellationToken);
}

    public async Task<Result<ProjectQuoteOperationResult>> ChangeStatusAsync(
        ChangeProjectQuoteStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ManageQuotes);
        if (authorization.IsFailure) return Result.Failure<ProjectQuoteOperationResult>(authorization.Error);
        if (command.IdempotencyKey == Guid.Empty)
            return Result.Failure<ProjectQuoteOperationResult>("İşlem anahtarı oluşturulamadı.");
        if (command.ValidityDays is < 1 or > 365)
            return Result.Failure<ProjectQuoteOperationResult>("Teklif geçerlilik süresi 1 ile 365 gün arasında olmalıdır.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var reference = OperationReference("STATUS", command.IdempotencyKey);
                var previous = await context.ActivityLogs.AsNoTracking()
                    .SingleOrDefaultAsync(log => log.EntityName == AuditEntity && log.ReferenceId == reference,
                        cancellationToken);
                if (previous is not null && int.TryParse(previous.RecordId, out var replayId))
                {
                    var replayProject = await context.ServiceProjects.AsNoTracking()
                        .SingleOrDefaultAsync(project => project.Id == replayId, cancellationToken);
                    if (replayProject is null)
                        return await FailAsync<ProjectQuoteOperationResult>(transaction,
                            "Önceki durum işlemi bulundu ancak teklif kaydı bulunamadı.", cancellationToken);
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    return Result.Success(new ProjectQuoteOperationResult(
                        replayProject.Id, replayProject.QuoteStatus, replayProject.RevisionNumber, true));
                }

                var project = await context.ServiceProjects.SingleOrDefaultAsync(
                    item => item.Id == command.ProjectId, cancellationToken)
                    ?? throw new ProjectQuoteValidationException("Proje teklifi bulunamadı.");
                EnsureExpectedState(project, command.ExpectedRevisionNumber, command.ExpectedStatus);

                var now = DateTime.UtcNow;
                var validation = ProjectQuoteLifecyclePolicy.ValidateTransition(
                    project.QuoteStatus, command.TargetStatus, project.ValidUntil, now, command.Reason);
                if (validation.IsFailure) throw new ProjectQuoteValidationException(validation.Error);

                if (project.QuoteStatus != command.TargetStatus)
                {
                    project.QuoteStatus = command.TargetStatus;
                    switch (command.TargetStatus)
                    {
                        case QuoteStatus.Sent:
                            project.SentDate = now;
                            project.ValidUntil = now.AddDays(command.ValidityDays);
                            project.ApprovedDate = null;
                            project.RejectedDate = null;
                            project.RejectionReason = null;
                            project.PipelineStage = PipelineStage.Proposal;
                            project.Status = ProjectStatus.PendingApproval;
                            break;
                        case QuoteStatus.Approved:
                            project.ApprovedDate = now;
                            project.PipelineStage = PipelineStage.Won;
                            project.Status = ProjectStatus.Active;
                            break;
                        case QuoteStatus.Rejected:
                            project.RejectedDate = now;
                            project.RejectionReason = command.Reason!.Trim();
                            project.PipelineStage = PipelineStage.Lost;
                            project.Status = ProjectStatus.Cancelled;
                            break;
                        case QuoteStatus.Expired:
                            project.PipelineStage = PipelineStage.Lost;
                            project.Status = ProjectStatus.Cancelled;
                            break;
                    }
                }

                AddAudit(context, "ProjectQuoteStatusChanged", "Update", project.Id,
                    $"{project.QuoteNumber} teklifi {ProjectQuoteLifecyclePolicy.Display(project.QuoteStatus)} durumuna geçirildi.",
                    reference, new { command.ExpectedStatus, command.TargetStatus, command.Reason });
                await context.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return Result.Success(new ProjectQuoteOperationResult(
                    project.Id, project.QuoteStatus, project.RevisionNumber, false));
            }
            catch (ProjectQuoteValidationException exception)
            {
                return await FailAsync<ProjectQuoteOperationResult>(transaction, exception.Message, cancellationToken);
            }
            catch (Exception exception)
            {
                return await FailAsync<ProjectQuoteOperationResult>(transaction,
                    $"Teklif durumu güncellenemedi: {exception.Message}", cancellationToken);
            }
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task<Result<ProjectQuoteDuplicateResult>> DuplicateAsync(
        DuplicateProjectQuoteCommand command,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ManageQuotes);
        if (authorization.IsFailure) return Result.Failure<ProjectQuoteDuplicateResult>(authorization.Error);
        if (command.IdempotencyKey == Guid.Empty)
            return Result.Failure<ProjectQuoteDuplicateResult>("İşlem anahtarı oluşturulamadı.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var reference = OperationReference("DUPLICATE", command.IdempotencyKey);
                var previous = await context.ActivityLogs.AsNoTracking()
                    .SingleOrDefaultAsync(log => log.EntityName == AuditEntity && log.ReferenceId == reference,
                        cancellationToken);
                if (previous is not null && int.TryParse(previous.RecordId, out var replayId))
                {
                    var replay = await context.ServiceProjects.AsNoTracking()
                        .SingleOrDefaultAsync(project => project.Id == replayId, cancellationToken)
                        ?? throw new ProjectQuoteValidationException("Önceki kopyalama işleminin teklif kaydı bulunamadı.");
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    return Result.Success(new ProjectQuoteDuplicateResult(
                        replay.Id, replay.ProjectCode, replay.QuoteNumber ?? string.Empty, true));
                }

                var source = await context.ServiceProjects.AsNoTracking()
                    .SingleOrDefaultAsync(project => project.Id == command.SourceProjectId, cancellationToken)
                    ?? throw new ProjectQuoteValidationException("Kopyalanacak teklif bulunamadı.");
                if (source.RevisionNumber != command.ExpectedRevisionNumber)
                    throw new ProjectQuoteValidationException(
                        $"Teklif R{source.RevisionNumber} olarak güncellendi. Listeyi yenileyip tekrar deneyin.");
                if (!source.CustomerId.HasValue ||
                    !await context.Customers.AnyAsync(customer => customer.Id == source.CustomerId.Value, cancellationToken))
                    throw new ProjectQuoteValidationException("Teklifin bağlı müşterisi bulunamadı.");

                var pricingResult = ProjectQuotePricingPolicy.Calculate(
                    source.ProjectScopeJson, source.DiscountPercent, source.KdvRate);
                if (pricingResult.IsFailure || pricingResult.Value is null || pricingResult.Value.IncludedLineCount == 0)
                    throw new ProjectQuoteValidationException(
                        "Eski teklif kapsamı doğrulanamadı. Teklifi editörde açıp kaydettikten sonra kopyalayın.");

                var now = DateTime.UtcNow;
                var copy = new ServiceProject
                {
                    Title = $"{source.Title} (Kopya)",
                    Name = $"{source.Title} (Kopya)",
                    CustomerId = source.CustomerId,
                    ProjectCode = await NextNumberAsync(context, "PRJ", now.Year, cancellationToken),
                    QuoteNumber = await NextNumberAsync(context, "TEK", now.Year, cancellationToken),
                    ProjectScopeJson = source.ProjectScopeJson,
                    TotalBudget = pricingResult.Value.NetRevenue,
                    TotalCost = pricingResult.Value.TotalCost,
                    TotalProfit = pricingResult.Value.TotalProfit,
                    DiscountPercent = source.DiscountPercent,
                    KdvRate = source.KdvRate,
                    CreatedDate = now,
                    QuoteStatus = QuoteStatus.Draft,
                    RevisionNumber = 1,
                    Status = ProjectStatus.Draft,
                    PipelineStage = PipelineStage.New,
                    TotalUnitCount = source.TotalUnitCount,
                    SurveyNotes = source.SurveyNotes,
                    QuoteItemsJson = source.QuoteItemsJson,
                    Notes = source.Notes,
                    PaymentTerms = source.PaymentTerms,
                    ValidUntil = null,
                    RevisionsJson = null
                };
                context.ServiceProjects.Add(copy);
                await context.SaveChangesAsync(cancellationToken);
                AddAudit(context, "ProjectQuoteDuplicated", "Create", copy.Id,
                    $"{source.QuoteNumber} teklifinden {copy.QuoteNumber} taslağı oluşturuldu.",
                    reference, new { SourceProjectId = source.Id, SourceRevision = source.RevisionNumber });
                await context.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return Result.Success(new ProjectQuoteDuplicateResult(
                    copy.Id, copy.ProjectCode, copy.QuoteNumber ?? string.Empty, false));
            }
            catch (ProjectQuoteValidationException exception)
            {
                return await FailAsync<ProjectQuoteDuplicateResult>(transaction, exception.Message, cancellationToken);
            }
            catch (Exception exception)
            {
                return await FailAsync<ProjectQuoteDuplicateResult>(transaction,
                    $"Teklif kopyalanamadı: {exception.Message}", cancellationToken);
            }
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task<Result<ProjectQuoteOperationResult>> DeleteDraftAsync(
        DeleteProjectQuoteCommand command,
        CancellationToken cancellationToken = default)
    {
        var manage = _authorization.Authorize(ApplicationPermission.ManageQuotes);
        if (manage.IsFailure) return Result.Failure<ProjectQuoteOperationResult>(manage.Error);
        var delete = _authorization.Authorize(ApplicationPermission.DeleteRecords);
        if (delete.IsFailure) return Result.Failure<ProjectQuoteOperationResult>(delete.Error);
        if (command.IdempotencyKey == Guid.Empty)
            return Result.Failure<ProjectQuoteOperationResult>("İşlem anahtarı oluşturulamadı.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var reference = OperationReference("DELETE", command.IdempotencyKey);
                var previous = await context.ActivityLogs.AsNoTracking()
                    .SingleOrDefaultAsync(log => log.EntityName == AuditEntity && log.ReferenceId == reference,
                        cancellationToken);
                if (previous is not null && int.TryParse(previous.RecordId, out var replayId))
                {
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    return Result.Success(new ProjectQuoteOperationResult(
                        replayId, QuoteStatus.Draft, command.ExpectedRevisionNumber, true));
                }

                var project = await context.ServiceProjects.SingleOrDefaultAsync(
                    item => item.Id == command.ProjectId, cancellationToken)
                    ?? throw new ProjectQuoteValidationException("Silinecek teklif bulunamadı.");
                EnsureExpectedState(project, command.ExpectedRevisionNumber, command.ExpectedStatus);
                if (project.QuoteStatus != QuoteStatus.Draft)
                    throw new ProjectQuoteValidationException(
                        "Yalnızca hiç gönderilmemiş taslak teklifler silinebilir. Diğer teklifler denetim geçmişi için korunur.");
                if (await context.ServiceJobs.AnyAsync(job => job.ServiceProjectId == project.Id, cancellationToken))
                    throw new ProjectQuoteValidationException("İş emrine bağlı teklif silinemez.");

                AddAudit(context, "ProjectQuoteDeleted", "Delete", project.Id,
                    $"{project.QuoteNumber} numaralı taslak teklif silindi.", reference,
                    new { project.ProjectCode, project.RevisionNumber });
                context.ServiceProjects.Remove(project);
                await context.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return Result.Success(new ProjectQuoteOperationResult(
                    project.Id, QuoteStatus.Draft, project.RevisionNumber, false));
            }
            catch (ProjectQuoteValidationException exception)
            {
                return await FailAsync<ProjectQuoteOperationResult>(transaction, exception.Message, cancellationToken);
            }
            catch (Exception exception)
            {
                return await FailAsync<ProjectQuoteOperationResult>(transaction,
                    $"Teklif silinemedi: {exception.Message}", cancellationToken);
            }
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task<Result<ProjectQuoteWorkOrderResult>> ConvertApprovedToWorkOrderAsync(
        ConvertApprovedQuoteToWorkOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var quoteAuthorization = _authorization.Authorize(ApplicationPermission.ManageQuotes);
        if (quoteAuthorization.IsFailure) return Result.Failure<ProjectQuoteWorkOrderResult>(quoteAuthorization.Error);
        var jobAuthorization = _authorization.Authorize(ApplicationPermission.ManageServiceJobs);
        if (jobAuthorization.IsFailure) return Result.Failure<ProjectQuoteWorkOrderResult>(jobAuthorization.Error);
        if (command.IdempotencyKey == Guid.Empty)
            return Result.Failure<ProjectQuoteWorkOrderResult>("İşlem anahtarı oluşturulamadı.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var existingJob = await context.ServiceJobs.AsNoTracking()
                    .FirstOrDefaultAsync(job => job.ServiceProjectId == command.ProjectId &&
                                                job.Source == "ApprovedProjectQuote" && !job.IsDeleted,
                        cancellationToken);
                if (existingJob is not null)
                {
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    return Result.Success(new ProjectQuoteWorkOrderResult(
                        command.ProjectId, existingJob.Id, true));
                }

                var project = await context.ServiceProjects.SingleOrDefaultAsync(
                    item => item.Id == command.ProjectId, cancellationToken)
                    ?? throw new ProjectQuoteValidationException("İş emrine dönüştürülecek teklif bulunamadı.");
                EnsureExpectedState(project, command.ExpectedRevisionNumber, command.ExpectedStatus);
                if (project.QuoteStatus != QuoteStatus.Approved)
                    throw new ProjectQuoteValidationException("Yalnızca onaylanmış teklif iş emrine dönüştürülebilir.");
                if (!project.CustomerId.HasValue)
                    throw new ProjectQuoteValidationException("Teklifin bağlı müşterisi bulunmuyor.");

                var pricingResult = ProjectQuotePricingPolicy.Calculate(
                    project.ProjectScopeJson, project.DiscountPercent, project.KdvRate);
                if (pricingResult.IsFailure || pricingResult.Value is null)
                    throw new ProjectQuoteValidationException(pricingResult.Error);
                var now = DateTime.UtcNow;
                var job = new ServiceJob
                {
                    CustomerId = project.CustomerId.Value,
                    ServiceProjectId = project.Id,
                    Title = $"Kurulum - {project.Title}",
                    Description = $"{project.QuoteNumber} numaralı onaylı tekliften oluşturuldu.",
                    WorkOrderType = WorkOrderType.Installation,
                    ServiceJobType = ServiceJobType.Project,
                    WorkflowStatus = WorkflowStatus.Approved,
                    Status = JobStatus.Pending,
                    Priority = JobPriority.Normal,
                    Source = "ApprovedProjectQuote",
                    Price = pricingResult.Value.NetRevenue,
                    TaxAmount = pricingResult.Value.VatAmount,
                    TotalAmount = pricingResult.Value.GrandTotal,
                    CreatedDate = now,
                    CreatedBy = _currentUser.Username
                };
                context.ServiceJobs.Add(job);
                project.Status = ProjectStatus.Active;
                await context.SaveChangesAsync(cancellationToken);
                context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = job.Id,
                    Date = now,
                    JobStatusChange = JobStatus.Pending,
                    TechnicianNote = "Onaylı proje teklifinden kurulum iş emri oluşturuldu.",
                    Action = "CreatedFromApprovedQuote",
                    Notes = project.QuoteNumber,
                    UserId = _currentUser.Username,
                    PerformedAt = now
                });
                AddAudit(context, "ProjectQuoteConvertedToWorkOrder", "Create", project.Id,
                    $"{project.QuoteNumber} teklifinden #{job.Id} kurulum iş emri oluşturuldu.",
                    OperationReference("WORKORDER", command.IdempotencyKey), new { WorkOrderId = job.Id });
                await context.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return Result.Success(new ProjectQuoteWorkOrderResult(project.Id, job.Id, false));
            }
            catch (ProjectQuoteValidationException exception)
            {
                return await FailAsync<ProjectQuoteWorkOrderResult>(transaction, exception.Message, cancellationToken);
            }
            catch (Exception exception)
            {
                return await FailAsync<ProjectQuoteWorkOrderResult>(transaction,
                    $"İş emri oluşturulamadı: {exception.Message}", cancellationToken);
            }
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task<Result<ExpireProjectQuotesResult>> ExpireOverdueAsync(
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ManageQuotes);
        if (authorization.IsFailure) return Result.Failure<ExpireProjectQuotesResult>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var now = DateTime.UtcNow;
                var overdue = await context.ServiceProjects
                    .Where(project => project.QuoteStatus == QuoteStatus.Sent &&
                                      project.ValidUntil.HasValue && project.ValidUntil.Value < now)
                    .ToListAsync(cancellationToken);
                foreach (var project in overdue)
                {
                    project.QuoteStatus = QuoteStatus.Expired;
                    project.PipelineStage = PipelineStage.Lost;
                    project.Status = ProjectStatus.Cancelled;
                    AddAudit(context, "ProjectQuoteExpired", "Update", project.Id,
                        $"{project.QuoteNumber} teklifinin geçerlilik süresi doldu.",
                        $"QUOTE-EXPIRED:{project.Id}:{project.ValidUntil:O}", new { project.ValidUntil });
                }
                await context.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return Result.Success(new ExpireProjectQuotesResult(overdue.Count));
            }
            catch (Exception exception)
            {
                return await FailAsync<ExpireProjectQuotesResult>(transaction,
                    $"Süresi dolan teklifler güncellenemedi: {exception.Message}", cancellationToken);
            }
        }, IsolationLevel.Serializable, cancellationToken);
    }

    private static async Task<Result<ProjectQuoteSaveResult>> BuildReplayResultAsync(
        AppDbContext context,
        int projectId,
        ProjectQuotePricingResult fallbackPricing,
        CancellationToken cancellationToken)
    {
        var project = await context.ServiceProjects.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
            return Result.Failure<ProjectQuoteSaveResult>("Önceki teklif işlemi bulundu ancak proje kaydı bulunamadı.");

        var currentPricing = ProjectQuotePricingPolicy.Calculate(
            project.ProjectScopeJson, project.DiscountPercent, project.KdvRate);
        return Result.Success(new ProjectQuoteSaveResult(
            project.Id, project.ProjectCode, project.QuoteNumber ?? string.Empty,
            project.RevisionNumber, project.QuoteStatus,
            currentPricing.Value ?? fallbackPricing, true, false));
    }

    private static async Task<string> NextNumberAsync(
        AppDbContext context,
        string prefix,
        int year,
        CancellationToken cancellationToken)
    {
        var values = prefix == "TEK"
            ? await context.ServiceProjects.AsNoTracking()
                .Where(item => item.QuoteNumber != null && item.QuoteNumber.StartsWith($"{prefix}-{year}-"))
                .Select(item => item.QuoteNumber!)
                .ToListAsync(cancellationToken)
            : await context.ServiceProjects.AsNoTracking()
                .Where(item => item.ProjectCode.StartsWith($"{prefix}-{year}-"))
                .Select(item => item.ProjectCode)
                .ToListAsync(cancellationToken);

        var maximum = values.Select(value => value.Split('-').LastOrDefault())
            .Select(value => int.TryParse(value, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}-{year}-{maximum + 1:D3}";
    }

    private static List<QuoteRevision> DeserializeRevisions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<QuoteRevision>>(json) ?? [];
        }
        catch (JsonException)
        {
            throw new ProjectQuoteValidationException(
                "Mevcut revizyon geçmişi okunamadığı için teklif güvenle güncellenemedi.");
        }
    }

    private static bool BusinessScopeEquals(string leftJson, string rightJson)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var left = JsonSerializer.Deserialize<List<ScopeNode>>(leftJson, options) ?? [];
            var right = JsonSerializer.Deserialize<List<ScopeNode>>(rightJson, options) ?? [];
            return NodeListsEqual(left, right);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool NodeListsEqual(IReadOnlyList<ScopeNode> left, IReadOnlyList<ScopeNode> right)
    {
        if (left.Count != right.Count) return false;
        for (var index = 0; index < left.Count; index++)
        {
            var first = left[index];
            var second = right[index];
            if (!string.Equals(first.Name, second.Name, StringComparison.Ordinal) || first.Type != second.Type)
                return false;
            if (first.Items.Count != second.Items.Count) return false;
            for (var itemIndex = 0; itemIndex < first.Items.Count; itemIndex++)
            {
                var a = first.Items[itemIndex];
                var b = second.Items[itemIndex];
                if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal) ||
                    !string.Equals(a.ProductName, b.ProductName, StringComparison.Ordinal) ||
                    a.ProductId != b.ProductId || a.Quantity != b.Quantity ||
                    a.UnitPrice != b.UnitPrice || a.UnitCost != b.UnitCost ||
                    a.LaborCost != b.LaborCost || a.IsOptional != b.IsOptional)
                    return false;
            }
            if (!NodeListsEqual(first.Children, second.Children)) return false;
        }
        return true;
    }

    private static void EnsureExpectedState(
        ServiceProject project,
        int expectedRevisionNumber,
        QuoteStatus expectedStatus)
    {
        if (project.RevisionNumber != expectedRevisionNumber || project.QuoteStatus != expectedStatus)
            throw new ProjectQuoteValidationException(
                $"Teklif artık {ProjectQuoteLifecyclePolicy.Display(project.QuoteStatus)} / R{project.RevisionNumber}. " +
                "Listeyi yenileyip işlemi tekrar deneyin.");
    }

    private void AddAudit(
        AppDbContext context,
        string action,
        string actionType,
        int projectId,
        string description,
        string reference,
        object? additionalData = null)
    {
        context.ActivityLogs.Add(new ActivityLog
        {
            UserId = _currentUser.UserId,
            Username = _currentUser.Username,
            Action = action,
            ActionType = actionType,
            EntityName = AuditEntity,
            RecordId = projectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ReferenceId = reference,
            Description = description,
            AdditionalData = additionalData is null ? null : JsonSerializer.Serialize(additionalData),
            Timestamp = DateTime.UtcNow,
            UserAgent = "WPF Client"
        });
    }

    private static string OperationReference(Guid key) => OperationReference("SAVE", key);
    private static string OperationReference(string operation, Guid key) => $"QUOTE-{operation}:{key:N}";

    private static async Task<IDbContextTransaction?> BeginTransactionAsync(
        AppDbContext context,
        CancellationToken cancellationToken) =>
        context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

    private static async Task<Result<T>> FailAsync<T>(
        IDbContextTransaction? transaction,
        string error,
        CancellationToken cancellationToken)
    {
        if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
        return Result.Failure<T>(error);
    }

    private sealed class ProjectQuoteValidationException(string message) : Exception(message);
}
