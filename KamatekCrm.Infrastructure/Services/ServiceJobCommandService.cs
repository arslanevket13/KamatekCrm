using System.Data;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Models.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KamatekCrm.Infrastructure.Services;

/// <summary>
/// İş emri yazma işlemlerinin transaction sınırıdır. UI katmanı artık iş emri,
/// kalem ve stok rezervasyonlarını ayrı DbContext'lerle kaydetmez.
/// </summary>
public sealed class ServiceJobCommandService : IServiceJobCommandService
{
    private const string ReservationReferenceType = "ServiceJob";
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IServiceJobStatusPolicy _statusPolicy;
    private readonly IApplicationAuthorizationService _authorizationService;

    public ServiceJobCommandService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IServiceJobStatusPolicy statusPolicy,
        IApplicationAuthorizationService authorizationService)
    {
        _dbContextFactory = dbContextFactory;
        _statusPolicy = statusPolicy;
        _authorizationService = authorizationService;
    }

    public async Task<Result<ServiceJobSaveResult>> SaveAsync(
        ServiceJobSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorizationService.Authorize(ApplicationPermission.ManageServiceJobs);
        if (authorization.IsFailure)
        {
            return Result.Failure<ServiceJobSaveResult>(authorization.Error);
        }

        if (request.Job.CustomerId <= 0 && request.QuickCustomer is null)
        {
            return Result.Failure<ServiceJobSaveResult>("İş emri için geçerli bir müşteri seçilmelidir.");
        }

        if (request.IsEditing && request.QuickCustomer is not null)
        {
            return Result.Failure<ServiceJobSaveResult>("Mevcut iş emri düzenlenirken hızlı müşteri oluşturulamaz.");
        }

        var validItems = request.Items
            .Where(item => item.ProductId.HasValue && item.ProductId.Value > 0 && item.QuantityUsed > 0)
            .ToList();

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var stockValidation = await ValidateRequestedStockAsync(
                    context,
                    validItems,
                    request.IsEditing ? request.Job.Id : null,
                    cancellationToken);
                if (stockValidation.IsFailure)
                {
                    return Result.Failure<ServiceJobSaveResult>(stockValidation.Error);
                }

                int customerId = request.Job.CustomerId;
                if (request.QuickCustomer is not null)
                {
                    string fullName = request.QuickCustomer.FullName.Trim();
                    string phoneNumber = request.QuickCustomer.PhoneNumber.Trim();
                    if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phoneNumber))
                    {
                        return Result.Failure<ServiceJobSaveResult>("Hızlı müşteri için ad soyad ve telefon zorunludur.");
                    }

                    var customer = new Customer
                    {
                        CustomerCode = $"SJ{Guid.NewGuid():N}"[..18],
                        FullName = fullName,
                        PhoneNumber = phoneNumber,
                        Type = CustomerType.Individual,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = NormalizeUser(request.ChangedBy)
                    };
                    context.Customers.Add(customer);
                    await context.SaveChangesAsync(cancellationToken);
                    customerId = customer.Id;
                }
                else if (!await context.Customers.AnyAsync(item => item.Id == customerId, cancellationToken))
                {
                    return Result.Failure<ServiceJobSaveResult>("Seçilen müşteri bulunamadı.");
                }

                int? customerAssetId = request.Job.CustomerAssetId;
                if (request.NewAsset is not null)
                {
                    string brand = request.NewAsset.Brand.Trim();
                    string model = request.NewAsset.Model.Trim();
                    if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(model))
                    {
                        return Result.Failure<ServiceJobSaveResult>("Yeni cihaz için marka ve model zorunludur.");
                    }

                    var asset = new CustomerAsset
                    {
                        CustomerId = customerId,
                        Category = request.NewAsset.Category,
                        Brand = brand,
                        Model = model,
                        SerialNumber = NormalizeOptional(request.NewAsset.SerialNumber),
                        Location = NormalizeOptional(request.NewAsset.Location),
                        Status = AssetStatus.NeedsRepair,
                        CreatedDate = DateTime.UtcNow
                    };
                    context.CustomerAssets.Add(asset);
                    await context.SaveChangesAsync(cancellationToken);
                    customerAssetId = asset.Id;
                }
                else if (customerAssetId.HasValue && !await context.CustomerAssets.AnyAsync(
                             item => item.Id == customerAssetId.Value && item.CustomerId == customerId,
                             cancellationToken))
                {
                    return Result.Failure<ServiceJobSaveResult>("Seçilen cihaz bu müşteriye ait değil veya bulunamadı.");
                }

                ServiceJob trackedJob;
                if (request.IsEditing)
                {
                    trackedJob = await context.ServiceJobs
                        .FirstOrDefaultAsync(job => job.Id == request.Job.Id, cancellationToken)
                        ?? throw new InvalidOperationException($"İş emri bulunamadı (ID: {request.Job.Id}).");

                    context.Entry(trackedJob).CurrentValues.SetValues(request.Job);
                }
                else
                {
                    trackedJob = new ServiceJob();
                    context.Entry(trackedJob).CurrentValues.SetValues(request.Job);
                    trackedJob.Id = 0;
                    context.ServiceJobs.Add(trackedJob);
                }

                trackedJob.CustomerId = customerId;
                trackedJob.CustomerAssetId = customerAssetId;

                trackedJob.ModifiedDate = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);

                // Montaj emri planlanmış işlerde stok kalemlerinin kaynağı montaj malzemeleridir
                // (SaveInstallationAsync). Genel editör bu kalemleri ezmemeli; aksi halde montaj
                // rezervasyonu/tüketimi bozulur.
                bool hasInstallation = await context.InstallationOrders
                    .AnyAsync(i => i.ServiceJobId == trackedJob.Id, cancellationToken);
                if (request.IsEditing && !hasInstallation)
                {
                    var oldItems = await context.ServiceJobItems
                        .Where(item => item.ServiceJobId == trackedJob.Id)
                        .ToListAsync(cancellationToken);
                    context.ServiceJobItems.RemoveRange(oldItems);
                }

                context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = trackedJob.Id,
                    Date = DateTime.UtcNow,
                    JobStatusChange = trackedJob.Status,
                    TechnicianNote = request.IsEditing ? "İş emri güncellendi." : "İş emri oluşturuldu.",
                    Action = request.IsEditing ? "Updated" : "Created",
                    UserId = NormalizeUser(request.ChangedBy),
                    PerformedAt = DateTime.UtcNow
                });

                foreach (var item in validItems.Where(_ => !hasInstallation))
                {
                    context.ServiceJobItems.Add(new ServiceJobItem
                    {
                        ServiceJobId = trackedJob.Id,
                        ProductId = item.ProductId,
                        QuantityUsed = item.QuantityUsed,
                        UnitPrice = item.UnitPrice,
                        UnitCost = item.UnitCost
                    });
                }

                var reservationResult = await SynchronizeReservationsAsync(
                    context,
                    trackedJob,
                    hasInstallation ? [] : validItems,
                    NormalizeUser(request.ChangedBy),
                    cancellationToken);

                if (reservationResult.IsFailure)
                {
                    await RollbackIfPresentAsync(transaction, cancellationToken);
                    return Result.Failure<ServiceJobSaveResult>(reservationResult.Error);
                }

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);

                return Result.Success(new ServiceJobSaveResult(
                    trackedJob.Id,
                    trackedJob.IsStockReserved,
                    reservationResult.Value,
                    customerId,
                    customerAssetId));
            }
            catch (Exception ex)
            {
                await RollbackIfPresentAsync(transaction, cancellationToken);
                return Result.Failure<ServiceJobSaveResult>($"İş emri kaydedilemedi: {ex.Message}");
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<Result<ServiceJobStatusChangeResult>> ChangeStatusAsync(
        int jobId,
        JobStatus requestedStatus,
        string changedBy,
        CancellationToken cancellationToken = default) =>
        await ChangeStatusCoreAsync(
            jobId, requestedStatus, null, null, null, changedBy, cancellationToken);

    public async Task<Result<ServiceJobStatusChangeResult>> CompleteAsync(
        int jobId,
        decimal? laborCost,
        decimal? discountAmount,
        string? completionNote,
        string changedBy,
        CancellationToken cancellationToken = default) =>
        await ChangeStatusCoreAsync(
            jobId,
            JobStatus.Completed,
            laborCost,
            discountAmount,
            completionNote,
            changedBy,
            cancellationToken);

    public async Task<Result<ServiceJobQuoteConversionResult>> ConvertToQuoteAsync(
        int jobId,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorizationService.Authorize(ApplicationPermission.ManageServiceJobs);
        if (authorization.IsFailure)
        {
            return Result.Failure<ServiceJobQuoteConversionResult>(authorization.Error);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var job = await context.ServiceJobs
                    .FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
                if (job is null)
                {
                    return Result.Failure<ServiceJobQuoteConversionResult>($"İş emri bulunamadı (ID: {jobId}).");
                }

                var previousStatus = job.Status;
                if (job.IsConvertedToQuote || previousStatus == JobStatus.ConvertedToQuote)
                {
                    return Result.Failure<ServiceJobQuoteConversionResult>("Bu iş emri zaten teklife dönüştürülmüştür.");
                }

                var validation = _statusPolicy.ValidateTransition(previousStatus, JobStatus.ConvertedToQuote);
                if (validation.IsFailure)
                {
                    return Result.Failure<ServiceJobQuoteConversionResult>(validation.Error);
                }

                // 1. Keşif raporunu oluştur/güncelle (keşif verilerinin anlık görüntüsü)
                var now = DateTime.UtcNow;
                var items = await context.ServiceJobItems
                    .Where(i => i.ServiceJobId == job.Id)
                    .ToListAsync(cancellationToken);

                var productIds = items
                    .Where(i => i.ProductId.HasValue)
                    .Select(i => i.ProductId!.Value)
                    .Distinct()
                    .ToArray();
                var productNames = await context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.ProductName })
                    .ToDictionaryAsync(p => p.Id, p => p.ProductName, cancellationToken);

                var discovery = await context.DiscoveryReports
                    .FirstOrDefaultAsync(d => d.ServiceJobId == job.Id, cancellationToken);
                if (discovery is null)
                {
                    discovery = new DiscoveryReport { ServiceJobId = job.Id, CreatedDate = now };
                    context.DiscoveryReports.Add(discovery);
                    await context.SaveChangesAsync(cancellationToken);
                }

                discovery.TechnicalNotes = job.DiscoveryTechnicalNotes ?? job.TechnicianNotes;
                discovery.RecommendedSolution = job.TechnicianNotes;
                discovery.PhotoPathsJson = job.PhotoPathsJson;
                discovery.EstimatedLaborHours = job.EstimatedLaborHours;
                discovery.TechnicianName = job.AssignedTechnician;

                var oldDiscoveryMaterials = await context.DiscoveryMaterials
                    .Where(m => m.DiscoveryReportId == discovery.Id)
                    .ToListAsync(cancellationToken);
                context.DiscoveryMaterials.RemoveRange(oldDiscoveryMaterials);

                var createdMaterials = new List<DiscoveryMaterial>();
                foreach (var item in items)
                {
                    var material = new DiscoveryMaterial
                    {
                        DiscoveryReportId = discovery.Id,
                        ProductId = item.ProductId,
                        ProductName = item.ProductId.HasValue
                            ? productNames.GetValueOrDefault(item.ProductId.Value) ?? $"Ürün #{item.ProductId.Value}"
                            : "Açıklanan malzeme",
                        Quantity = item.QuantityUsed,
                        Notes = null
                    };
                    context.DiscoveryMaterials.Add(material);
                    createdMaterials.Add(material);
                }

                // 2. Teklif oluştur (yalnızca bir kez; ikinci çağrı yukarıdaki idempotentlik
                //    kontrolüyle reddedilir)
                var quote = new WorkOrderQuotation
                {
                    ServiceJobId = job.Id,
                    QuotationNumber = $"TEK-{now:yyyyMMdd}-{job.Id}",
                    Status = QuotationStatus.Draft,
                    IssuedDate = now,
                    ValidUntil = now.AddDays(15),
                    TaxRate = 20m,
                    Description = job.Description
                };
                context.WorkOrderQuotations.Add(quote);
                await context.SaveChangesAsync(cancellationToken);

                // 3. DiscoveryMaterials → QuotationItems kopyala (eski kayıt değişmeden kalır)
                int itemSequence = 0;
                foreach (var material in createdMaterials)
                {
                    context.QuotationItems.Add(new QuotationItem
                    {
                        QuotationId = quote.Id,
                        ProductId = material.ProductId,
                        ProductName = material.ProductName,
                        Quantity = material.Quantity,
                        UnitPrice = 0m,
                        DiscountPercent = 0m,
                        TaxPercent = quote.TaxRate,
                        LineTotal = 0m,
                        Sequence = itemSequence++
                    });
                }

                // 4. İş emri durumunu ConvertedToQuote yap
                job.Status = JobStatus.ConvertedToQuote;
                job.IsConvertedToQuote = true;
                job.ModifiedDate = now;

                context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = job.Id,
                    Date = now,
                    JobStatusChange = JobStatus.ConvertedToQuote,
                    TechnicianNote = $"Keşif kaydı teklife dönüştürüldü (Teklif #{quote.Id}, No: {quote.QuotationNumber}, {createdMaterials.Count} kalem kopyalandı).",
                    Action = "ConvertedToQuote",
                    Notes = $"Önceki durum: {previousStatus}",
                    UserId = NormalizeUser(changedBy),
                    PerformedAt = now
                });

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);

                return Result.Success(new ServiceJobQuoteConversionResult(
                    job.Id, job.CustomerId, previousStatus, job.Status));
            }
            catch (Exception ex)
            {
                await RollbackIfPresentAsync(transaction, cancellationToken);
                return Result.Failure<ServiceJobQuoteConversionResult>($"İş emri teklife dönüştürülemedi: {ex.Message}");
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<Result<DiscoverySaveResult>> SaveDiscoveryAsync(
        SaveDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorizationService.Authorize(ApplicationPermission.ManageServiceJobs);
        if (authorization.IsFailure)
        {
            return Result.Failure<DiscoverySaveResult>(authorization.Error);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var job = await context.ServiceJobs
                    .FirstOrDefaultAsync(item => item.Id == request.JobId, cancellationToken);
                if (job is null)
                {
                    return Result.Failure<DiscoverySaveResult>($"İş emri bulunamadı (ID: {request.JobId}).");
                }

                var now = DateTime.UtcNow;
                var report = await context.DiscoveryReports
                    .Include(r => r.Materials)
                    .FirstOrDefaultAsync(r => r.ServiceJobId == job.Id, cancellationToken);
                bool isNew = report is null;
                if (report is null)
                {
                    report = new DiscoveryReport { ServiceJobId = job.Id, CreatedDate = now };
                    context.DiscoveryReports.Add(report);
                    await context.SaveChangesAsync(cancellationToken);
                }

                report.TechnicalNotes = NormalizeOptional(request.TechnicalNotes);
                report.RecommendedSolution = NormalizeOptional(request.RecommendedSolution);
                report.EstimatedLaborHours = Math.Max(0, request.EstimatedLaborHours);
                report.TechnicianName = NormalizeOptional(request.TechnicianName);
                report.PhotoPathsJson = request.PhotoPaths is { Count: > 0 }
                    ? System.Text.Json.JsonSerializer.Serialize(request.PhotoPaths)
                    : null;

                // Malzemeler: diff tabanlı güncelleme (ID korunarak). Boş adı olan satırlar
                // teklif düzenleyiciyle aynı kural gereği yok sayılır (geçersiz satır NRE üretmez).
                var validMaterials = request.Materials
                    .Where(m => !string.IsNullOrWhiteSpace(m.ProductName))
                    .ToList();
                var existingMaterials = report.Materials.ToList();
                var existingById = existingMaterials.ToDictionary(m => m.Id);
                var retainedIds = new HashSet<int>();
                foreach (var input in validMaterials)
                {
                    if (input.Id.HasValue && existingById.TryGetValue(input.Id.Value, out var existing))
                    {
                        existing.ProductId = input.ProductId;
                        existing.ProductName = input.ProductName.Trim();
                        existing.Quantity = Math.Max(0, input.Quantity);
                        existing.Notes = NormalizeOptional(input.Notes);
                        retainedIds.Add(input.Id.Value);
                    }
                    else
                    {
                        context.DiscoveryMaterials.Add(new DiscoveryMaterial
                        {
                            DiscoveryReportId = report.Id,
                            ProductId = input.ProductId,
                            ProductName = input.ProductName.Trim(),
                            Quantity = Math.Max(0, input.Quantity),
                            Notes = NormalizeOptional(input.Notes)
                        });
                    }
                }
                foreach (var removed in existingMaterials.Where(m => !retainedIds.Contains(m.Id)))
                {
                    context.DiscoveryMaterials.Remove(removed);
                }

                // Ziyaretler: diff tabanlı güncelleme
                var existingVisits = await context.DiscoveryVisits
                    .Where(v => v.ServiceJobId == job.Id)
                    .ToListAsync(cancellationToken);
                var visitsById = existingVisits.ToDictionary(v => v.Id);
                var retainedVisitIds = new HashSet<int>();
                foreach (var input in request.Visits)
                {
                    if (input.Id.HasValue && visitsById.TryGetValue(input.Id.Value, out var existingVisit))
                    {
                        existingVisit.VisitDate = input.VisitDate == default ? now : input.VisitDate;
                        existingVisit.TechnicianName = NormalizeOptional(input.TechnicianName);
                        existingVisit.Notes = NormalizeOptional(input.Notes);
                        existingVisit.PhotoPathsJson = input.PhotoPaths is { Count: > 0 }
                            ? System.Text.Json.JsonSerializer.Serialize(input.PhotoPaths)
                            : null;
                        retainedVisitIds.Add(input.Id.Value);
                    }
                    else
                    {
                        context.DiscoveryVisits.Add(new DiscoveryVisit
                        {
                            ServiceJobId = job.Id,
                            VisitDate = input.VisitDate == default ? now : input.VisitDate,
                            TechnicianName = NormalizeOptional(input.TechnicianName),
                            Notes = NormalizeOptional(input.Notes),
                            PhotoPathsJson = input.PhotoPaths is { Count: > 0 }
                                ? System.Text.Json.JsonSerializer.Serialize(input.PhotoPaths)
                                : null,
                            CreatedDate = now
                        });
                    }
                }
                foreach (var removed in existingVisits.Where(v => !retainedVisitIds.Contains(v.Id)))
                {
                    context.DiscoveryVisits.Remove(removed);
                }

                // İş emrinin keşif alanlarını raporla senkronize et (listeleme/görünüm tutarlılığı)
                job.DiscoveryTechnicalNotes = report.TechnicalNotes;
                job.PhotoPathsJson = report.PhotoPathsJson;
                job.EstimatedLaborHours = report.EstimatedLaborHours;
                job.ModifiedDate = now;

                context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = job.Id,
                    Date = now,
                    JobStatusChange = null,
                    TechnicianNote = isNew
                        ? $"Keşif raporu oluşturuldu ({request.Materials.Count} malzeme, {request.Visits.Count} ziyaret)."
                        : $"Keşif raporu güncellendi ({request.Materials.Count} malzeme, {request.Visits.Count} ziyaret).",
                    Action = isNew ? "DiscoveryCreated" : "DiscoveryUpdated",
                    UserId = NormalizeUser(request.ChangedBy),
                    PerformedAt = now
                });

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);

                return Result.Success(new DiscoverySaveResult(report.Id));
            }
            catch (Exception ex)
            {
                await RollbackIfPresentAsync(transaction, cancellationToken);
                return Result.Failure<DiscoverySaveResult>($"Keşif kaydı kaydedilemedi: {ex.Message}");
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<Result<ServiceJobStatusChangeResult>> CompleteDiscoveryAsync(
        int jobId,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorizationService.Authorize(ApplicationPermission.ManageServiceJobs);
        if (authorization.IsFailure)
        {
            return Result.Failure<ServiceJobStatusChangeResult>(authorization.Error);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var job = await context.ServiceJobs
                    .FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
                if (job is null)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>($"İş emri bulunamadı (ID: {jobId}).");
                }

                // Keşif tamamlama doğrulaması: rapor var mı, teknik notlar yeterli mi, malzeme/ziyaret kaydı var mı?
                var report = await context.DiscoveryReports
                    .Include(r => r.Materials)
                    .FirstOrDefaultAsync(r => r.ServiceJobId == job.Id, cancellationToken);
                if (report is null)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>(
                        "Keşif raporu bulunamadı. Önce keşif kaydını oluşturup kaydedin.");
                }
                if (string.IsNullOrWhiteSpace(report.TechnicalNotes) && string.IsNullOrWhiteSpace(report.RecommendedSolution))
                {
                    return Result.Failure<ServiceJobStatusChangeResult>(
                        "Keşif tamamlamak için teknik tespit notları veya önerilen çözüm girilmelidir.");
                }
                int materialCount = report.Materials.Count;
                int visitCount = await context.DiscoveryVisits.CountAsync(v => v.ServiceJobId == job.Id, cancellationToken);
                if (materialCount == 0 && visitCount == 0)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>(
                        "Keşif tamamlamak için en az bir tahmini malzeme veya bir ziyaret kaydı girilmelidir.");
                }

                var previousStatus = job.Status;
                if (previousStatus == JobStatus.DiscoveryCompleted)
                {
                    return Result.Success(new ServiceJobStatusChangeResult(
                        job.Id, previousStatus, job.Status, job.CompletedDate));
                }

                var validation = _statusPolicy.ValidateTransition(previousStatus, JobStatus.DiscoveryCompleted);
                if (validation.IsFailure)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>(validation.Error);
                }

                var now = DateTime.UtcNow;
                job.Status = JobStatus.DiscoveryCompleted;
                job.ModifiedDate = now;

                context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = job.Id,
                    Date = now,
                    JobStatusChange = JobStatus.DiscoveryCompleted,
                    TechnicianNote = $"Keşif tamamlandı ({materialCount} malzeme, {visitCount} ziyaret). Teklif oluşturmaya hazır.",
                    Action = "DiscoveryCompleted",
                    Notes = $"Önceki durum: {previousStatus}",
                    UserId = NormalizeUser(changedBy),
                    PerformedAt = now
                });

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);

                return Result.Success(new ServiceJobStatusChangeResult(
                    job.Id, previousStatus, job.Status, job.CompletedDate));
            }
            catch (Exception ex)
            {
                await RollbackIfPresentAsync(transaction, cancellationToken);
                return Result.Failure<ServiceJobStatusChangeResult>($"Keşif tamamlanamadı: {ex.Message}");
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<Result<WorkOrderQuotationResult>> UpdateQuotationAsync(
        UpdateWorkOrderQuotationRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorizationService.Authorize(ApplicationPermission.ManageServiceJobs);
        if (authorization.IsFailure)
        {
            return Result.Failure<WorkOrderQuotationResult>(authorization.Error);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var quote = await context.WorkOrderQuotations
                    .Include(q => q.Items)
                    .FirstOrDefaultAsync(q => q.Id == request.QuotationId, cancellationToken);
                if (quote is null)
                {
                    return Result.Failure<WorkOrderQuotationResult>($"Teklif bulunamadı (ID: {request.QuotationId}).");
                }

                if (quote.Status is QuotationStatus.Accepted or QuotationStatus.Rejected
                    or QuotationStatus.Cancelled or QuotationStatus.Expired)
                {
                    return Result.Failure<WorkOrderQuotationResult>(
                        "Kabul edilmiş, reddedilmiş veya iptal edilmiş teklif düzenlenemez.");
                }

                quote.Description = NormalizeOptional(request.Description);
                quote.Warranty = NormalizeOptional(request.Warranty);
                quote.DeliveryTime = NormalizeOptional(request.DeliveryTime);
                quote.PaymentTerms = NormalizeOptional(request.PaymentTerms);
                quote.LaborCost = Math.Max(0m, request.LaborCost);
                quote.ShippingCost = Math.Max(0m, request.ShippingCost);
                quote.DiscountAmount = Math.Max(0m, request.DiscountAmount);
                quote.TaxRate = Math.Max(0m, request.TaxRate);

                // Diff tabanlı kalem güncellemesi: mevcut satırlar ID korunarak güncellenir,
                // yeni satırlar eklenir, listeden çıkarılanlar silinir (sil-yeniden-oluştur yerine).
                var validItems = request.Items
                    .Where(item => item.Quantity > 0 && !string.IsNullOrWhiteSpace(item.ProductName))
                    .ToList();

                // Yeni eklenen kalemler EF ilişki düzeltmesiyle quote.Items navigasyonuna anında
                // eklenir; silinecekler orijinal yüklü koleksiyondan önceden yakalanır (yeni satırlar silinmez).
                var existingItems = quote.Items.ToList();
                var existingById = existingItems.ToDictionary(item => item.Id);
                var retainedIds = new HashSet<int>();
                foreach (var item in validItems)
                {
                    var unitPrice = Math.Max(0m, item.UnitPrice);
                    var discountPercent = Math.Max(0m, item.DiscountPercent);
                    var taxPercent = Math.Max(0m, item.TaxPercent);
                    var lineNet = Math.Round(item.Quantity * unitPrice * (1m - discountPercent / 100m), 2);

                    if (item.Id.HasValue && existingById.TryGetValue(item.Id.Value, out var existingItem))
                    {
                        existingItem.ProductId = item.ProductId;
                        existingItem.ProductName = item.ProductName.Trim();
                        existingItem.Quantity = item.Quantity;
                        existingItem.UnitPrice = unitPrice;
                        existingItem.DiscountPercent = discountPercent;
                        existingItem.TaxPercent = taxPercent;
                        existingItem.LineTotal = lineNet;
                        existingItem.Sequence = item.Sequence;
                        retainedIds.Add(item.Id.Value);
                    }
                    else
                    {
                        context.QuotationItems.Add(new QuotationItem
                        {
                            QuotationId = quote.Id,
                            ProductId = item.ProductId,
                            ProductName = item.ProductName.Trim(),
                            Quantity = item.Quantity,
                            UnitPrice = unitPrice,
                            DiscountPercent = discountPercent,
                            TaxPercent = taxPercent,
                            LineTotal = lineNet,
                            Sequence = item.Sequence
                        });
                    }
                }

                foreach (var removed in existingItems.Where(item => !retainedIds.Contains(item.Id)))
                {
                    context.QuotationItems.Remove(removed);
                }

                // Satır bazlı KDV: her satır kendi TaxPercent oranıyla vergilendirilir;
                // quote.TaxRate toplam hesabına karışmaz (yalnızca yeni satırların varsayılanıdır).
                decimal itemsNetTotal = validItems.Sum(item =>
                    Math.Round(item.Quantity * Math.Max(0m, item.UnitPrice) * (1m - Math.Max(0m, item.DiscountPercent) / 100m), 2));
                decimal itemsTaxTotal = validItems.Sum(item =>
                {
                    decimal net = Math.Round(item.Quantity * Math.Max(0m, item.UnitPrice) * (1m - Math.Max(0m, item.DiscountPercent) / 100m), 2);
                    return Math.Round(net * Math.Max(0m, item.TaxPercent) / 100m, 2);
                });
                decimal netTotal = itemsNetTotal - quote.DiscountAmount + quote.LaborCost + quote.ShippingCost;
                quote.TaxAmount = Math.Round(itemsTaxTotal, 2);
                quote.TotalAmount = Math.Round(netTotal + quote.TaxAmount, 2);

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);

                return Result.Success(new WorkOrderQuotationResult(quote.Id, quote.Status, quote.TotalAmount));
            }
            catch (Exception ex)
            {
                await RollbackIfPresentAsync(transaction, cancellationToken);
                return Result.Failure<WorkOrderQuotationResult>($"Teklif güncellenemedi: {ex.Message}");
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<Result<WorkOrderQuotationResult>> AcceptQuotationAsync(
        int quotationId,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorizationService.Authorize(ApplicationPermission.ManageServiceJobs);
        if (authorization.IsFailure)
        {
            return Result.Failure<WorkOrderQuotationResult>(authorization.Error);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var quote = await context.WorkOrderQuotations
                    .FirstOrDefaultAsync(q => q.Id == quotationId, cancellationToken);
                if (quote is null)
                {
                    return Result.Failure<WorkOrderQuotationResult>($"Teklif bulunamadı (ID: {quotationId}).");
                }

                if (quote.Status == QuotationStatus.Accepted)
                {
                    return Result.Success(new WorkOrderQuotationResult(quote.Id, quote.Status, quote.TotalAmount));
                }

                if (quote.Status is QuotationStatus.Rejected or QuotationStatus.Cancelled or QuotationStatus.Expired)
                {
                    return Result.Failure<WorkOrderQuotationResult>(
                        $"{quote.Status} durumundaki teklif kabul edilemez.");
                }

                quote.Status = QuotationStatus.Accepted;
                quote.AcceptedAt = DateTime.UtcNow;

                await AppendWorkflowHistoryAsync(
                    context,
                    quote.ServiceJobId,
                    "Teklif müşteri tarafından kabul edildi.",
                    "QuotationAccepted",
                    changedBy,
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);

                return Result.Success(new WorkOrderQuotationResult(quote.Id, quote.Status, quote.TotalAmount));
            }
            catch (Exception ex)
            {
                await RollbackIfPresentAsync(transaction, cancellationToken);
                return Result.Failure<WorkOrderQuotationResult>($"Teklif kabul edilemedi: {ex.Message}");
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<Result<WorkOrderQuotationResult>> RejectQuotationAsync(
        int quotationId,
        string? reason,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorizationService.Authorize(ApplicationPermission.ManageServiceJobs);
        if (authorization.IsFailure)
        {
            return Result.Failure<WorkOrderQuotationResult>(authorization.Error);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var quote = await context.WorkOrderQuotations
                    .FirstOrDefaultAsync(q => q.Id == quotationId, cancellationToken);
                if (quote is null)
                {
                    return Result.Failure<WorkOrderQuotationResult>($"Teklif bulunamadı (ID: {quotationId}).");
                }

                if (quote.Status is QuotationStatus.Accepted or QuotationStatus.Rejected
                    or QuotationStatus.Cancelled or QuotationStatus.Expired)
                {
                    return Result.Failure<WorkOrderQuotationResult>(
                        $"{quote.Status} durumundaki teklif reddedilemez.");
                }

                quote.Status = QuotationStatus.Rejected;
                quote.RejectedAt = DateTime.UtcNow;
                quote.RejectionReason = NormalizeOptional(reason);

                await AppendWorkflowHistoryAsync(
                    context,
                    quote.ServiceJobId,
                    string.IsNullOrWhiteSpace(reason)
                        ? "Teklif reddedildi."
                        : $"Teklif reddedildi: {reason.Trim()}",
                    "QuotationRejected",
                    changedBy,
                    cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);

                return Result.Success(new WorkOrderQuotationResult(quote.Id, quote.Status, quote.TotalAmount));
            }
            catch (Exception ex)
            {
                await RollbackIfPresentAsync(transaction, cancellationToken);
                return Result.Failure<WorkOrderQuotationResult>($"Teklif reddedilemedi: {ex.Message}");
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<Result<QuotationRevisionResult>> CreateRevisionAsync(
        int quotationId,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorizationService.Authorize(ApplicationPermission.ManageServiceJobs);
        if (authorization.IsFailure)
        {
            return Result.Failure<QuotationRevisionResult>(authorization.Error);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var source = await context.WorkOrderQuotations
                    .Include(q => q.Items)
                    .FirstOrDefaultAsync(q => q.Id == quotationId, cancellationToken);
                if (source is null)
                {
                    return Result.Failure<QuotationRevisionResult>($"Teklif bulunamadı (ID: {quotationId}).");
                }

                if (source.Status == QuotationStatus.Draft)
                {
                    return Result.Failure<QuotationRevisionResult>(
                        "Taslak teklif doğrudan düzenlenebilir; revizyon oluşturmaya gerek yok.");
                }

                // Tekrarlanan çağrılarda mükerrer revizyon oluşmasını engelle: bekleyen taslak revizyon varsa reddet.
                var hasPendingRevision = await context.WorkOrderQuotations.AnyAsync(
                    q => q.ParentQuotationId == source.Id && q.Status == QuotationStatus.Draft,
                    cancellationToken);
                if (hasPendingRevision)
                {
                    return Result.Failure<QuotationRevisionResult>(
                        "Bu teklif için bekleyen (Taslak) bir revizyon zaten var; önce onu düzenleyin.");
                }

                var now = DateTime.UtcNow;
                var revision = new WorkOrderQuotation
                {
                    ServiceJobId = source.ServiceJobId,
                    ParentQuotationId = source.Id,
                    RevisionNumber = source.RevisionNumber + 1,
                    QuotationNumber = source.QuotationNumber,
                    Status = QuotationStatus.Draft,
                    IssuedDate = now,
                    ValidUntil = source.ValidUntil ?? now.AddDays(15),
                    Description = source.Description,
                    Warranty = source.Warranty,
                    DeliveryTime = source.DeliveryTime,
                    PaymentTerms = source.PaymentTerms,
                    LaborCost = source.LaborCost,
                    ShippingCost = source.ShippingCost,
                    DiscountAmount = source.DiscountAmount,
                    TaxRate = source.TaxRate
                };
                context.WorkOrderQuotations.Add(revision);
                await context.SaveChangesAsync(cancellationToken);

                // Kalemler sırayla kopyalanır; kaynak kalemler değişmeden kalır.
                var sourceItems = source.Items.OrderBy(i => i.Sequence).ThenBy(i => i.Id).ToList();
                foreach (var item in sourceItems)
                {
                    context.QuotationItems.Add(new QuotationItem
                    {
                        QuotationId = revision.Id,
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        DiscountPercent = item.DiscountPercent,
                        TaxPercent = item.TaxPercent,
                        LineTotal = item.LineTotal,
                        Sequence = item.Sequence
                    });
                }

                // Toplamlar aynı satır bazlı mantıkla yeniden hesaplanır.
                decimal itemsNet = sourceItems.Sum(i => i.LineTotal);
                decimal itemsTax = sourceItems.Sum(i => Math.Round(i.LineTotal * i.TaxPercent / 100m, 2));
                decimal netTotal = itemsNet - revision.DiscountAmount + revision.LaborCost + revision.ShippingCost;
                revision.TaxAmount = Math.Round(itemsTax, 2);
                revision.TotalAmount = Math.Round(netTotal + revision.TaxAmount, 2);

                context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = source.ServiceJobId,
                    Date = now,
                    JobStatusChange = null,
                    TechnicianNote = $"Teklif #{source.QuotationNumber} için Revizyon {revision.RevisionNumber} oluşturuldu (Teklif #{revision.Id}).",
                    Action = "QuotationRevisionCreated",
                    Notes = $"Kaynak teklif: {source.Id} (durum: {source.Status})",
                    UserId = NormalizeUser(changedBy),
                    PerformedAt = now
                });

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);

                return Result.Success(new QuotationRevisionResult(revision.Id, revision.RevisionNumber));
            }
            catch (Exception ex)
            {
                await RollbackIfPresentAsync(transaction, cancellationToken);
                return Result.Failure<QuotationRevisionResult>($"Teklif revizyonu oluşturulamadı: {ex.Message}");
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<Result<ServiceJobStatusChangeResult>> PlanInstallationAsync(
        PlanInstallationRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorizationService.Authorize(ApplicationPermission.ManageServiceJobs);
        if (authorization.IsFailure)
        {
            return Result.Failure<ServiceJobStatusChangeResult>(authorization.Error);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var job = await context.ServiceJobs
                    .FirstOrDefaultAsync(item => item.Id == request.JobId, cancellationToken);
                if (job is null)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>($"İş emri bulunamadı (ID: {request.JobId}).");
                }

                var previousStatus = job.Status;
                var validation = _statusPolicy.ValidateTransition(previousStatus, JobStatus.InstallationPlanned);
                if (validation.IsFailure)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>(validation.Error);
                }

                // Montaj yalnızca kabul edilmiş teklif için planlanabilir
                var quote = await context.WorkOrderQuotations
                    .Where(q => q.ServiceJobId == job.Id)
                    .OrderByDescending(q => q.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (quote is null || quote.Status != QuotationStatus.Accepted)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>(
                        "Montaj yalnızca kabul edilmiş teklif için planlanabilir. Önce teklifi kabul edin.");
                }

                var now = DateTime.UtcNow;
                var installation = await context.InstallationOrders
                    .FirstOrDefaultAsync(i => i.ServiceJobId == job.Id, cancellationToken);
                if (installation is null)
                {
                    installation = new InstallationOrder
                    {
                        ServiceJobId = job.Id,
                        QuotationId = quote.Id,
                        CreatedDate = now
                    };
                    context.InstallationOrders.Add(installation);
                    await context.SaveChangesAsync(cancellationToken);

                    // QuotationItems → InstallationMaterials kopyala
                    var quoteItems = await context.QuotationItems
                        .Where(i => i.QuotationId == quote.Id)
                        .ToListAsync(cancellationToken);
                    foreach (var item in quoteItems)
                    {
                        context.InstallationMaterials.Add(new InstallationMaterial
                        {
                            InstallationOrderId = installation.Id,
                            ProductId = item.ProductId,
                            ProductName = item.ProductName,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            Notes = null
                        });
                    }

                    context.InstallationTasks.Add(new InstallationTask
                    {
                        InstallationOrderId = installation.Id,
                        Title = "Montaj öncesi saha kontrolü",
                        Description = "Montaj yapılacak bölgenin hazırlığı kontrol edilir."
                    });
                    context.InstallationTasks.Add(new InstallationTask
                    {
                        InstallationOrderId = installation.Id,
                        Title = "Cihaz montajı ve kablolama",
                        Description = "Cihazlar kurulur ve kablolama tamamlanır."
                    });
                    context.InstallationTasks.Add(new InstallationTask
                    {
                        InstallationOrderId = installation.Id,
                        Title = "Sistem testi ve devreye alma",
                        Description = "Sistem test edilir ve müşteriye teslim edilir."
                    });
                }
                else
                {
                    installation.QuotationId = quote.Id;
                }

                installation.TechnicianId = request.TechnicianId ?? job.AssignedTechnicianId;
                installation.TechnicianName = request.TechnicianName ?? job.AssignedTechnician;
                installation.InstallationDate = request.InstallationDate;
                installation.Notes = request.Notes;

                job.AssignedTechnicianId = installation.TechnicianId;
                job.AssignedTechnician = installation.TechnicianName;
                job.Status = JobStatus.InstallationPlanned;
                job.ModifiedDate = now;

                context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = job.Id,
                    Date = now,
                    JobStatusChange = JobStatus.InstallationPlanned,
                    TechnicianNote = $"Montaj planlandı (Montaj emri #{installation.Id}, Teknisyen: {installation.TechnicianName ?? "Atanmadı"}).",
                    Action = "InstallationPlanned",
                    Notes = $"Önceki durum: {previousStatus} | Teklif: {quote.QuotationNumber}",
                    UserId = NormalizeUser(request.ChangedBy),
                    PerformedAt = now
                });

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);

                return Result.Success(new ServiceJobStatusChangeResult(
                    job.Id, previousStatus, job.Status, job.CompletedDate));
            }
            catch (Exception ex)
            {
                await RollbackIfPresentAsync(transaction, cancellationToken);
                return Result.Failure<ServiceJobStatusChangeResult>($"Montaj planlanamadı: {ex.Message}");
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<Result<InstallationSaveResult>> SaveInstallationAsync(
        SaveInstallationRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorizationService.Authorize(ApplicationPermission.ManageServiceJobs);
        if (authorization.IsFailure)
        {
            return Result.Failure<InstallationSaveResult>(authorization.Error);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var job = await context.ServiceJobs
                    .FirstOrDefaultAsync(item => item.Id == request.JobId, cancellationToken);
                if (job is null)
                {
                    return Result.Failure<InstallationSaveResult>($"İş emri bulunamadı (ID: {request.JobId}).");
                }

                var installation = await context.InstallationOrders
                    .Include(i => i.Materials)
                    .Include(i => i.Tasks)
                    .FirstOrDefaultAsync(i => i.ServiceJobId == job.Id, cancellationToken);
                if (installation is null)
                {
                    return Result.Failure<InstallationSaveResult>(
                        "Montaj emri bulunamadı; önce montajı planlayın.");
                }

                var now = DateTime.UtcNow;

                // Başlık bilgileri
                installation.TechnicianId = request.TechnicianId ?? job.AssignedTechnicianId;
                installation.TechnicianName = NormalizeOptional(request.TechnicianName) ?? job.AssignedTechnician;
                installation.InstallationDate = request.InstallationDate;
                installation.Notes = NormalizeOptional(request.Notes);
                installation.LaborHours = Math.Max(0m, request.LaborHours);

                job.AssignedTechnicianId = installation.TechnicianId;
                job.AssignedTechnician = installation.TechnicianName;
                job.ModifiedDate = now;

                // Malzemeler: diff tabanlı güncelleme (ID korumalı). Boş adı olan satırlar yok sayılır.
                var validMaterials = request.Materials
                    .Where(m => !string.IsNullOrWhiteSpace(m.ProductName))
                    .ToList();
                var existingMaterials = installation.Materials.ToList();
                var materialsById = existingMaterials.ToDictionary(m => m.Id);
                var retainedMaterialIds = new HashSet<int>();
                foreach (var input in validMaterials)
                {
                    if (input.Id.HasValue && materialsById.TryGetValue(input.Id.Value, out var existing))
                    {
                        existing.ProductId = input.ProductId;
                        existing.ProductName = input.ProductName.Trim();
                        existing.Quantity = Math.Max(0m, input.Quantity);
                        existing.UnitPrice = Math.Max(0m, input.UnitPrice);
                        existing.Notes = NormalizeOptional(input.Notes);
                        retainedMaterialIds.Add(input.Id.Value);
                    }
                    else
                    {
                        context.InstallationMaterials.Add(new InstallationMaterial
                        {
                            InstallationOrderId = installation.Id,
                            ProductId = input.ProductId,
                            ProductName = input.ProductName.Trim(),
                            Quantity = Math.Max(0m, input.Quantity),
                            UnitPrice = Math.Max(0m, input.UnitPrice),
                            Notes = NormalizeOptional(input.Notes)
                        });
                    }
                }
                foreach (var removed in existingMaterials.Where(m => !retainedMaterialIds.Contains(m.Id)))
                {
                    context.InstallationMaterials.Remove(removed);
                }

                // Görevler: diff tabanlı güncelleme (tamamlanma durumu korunur)
                var validTasks = request.Tasks.Where(t => !string.IsNullOrWhiteSpace(t.Title)).ToList();
                var existingTasks = installation.Tasks.ToList();
                var tasksById = existingTasks.ToDictionary(t => t.Id);
                var retainedTaskIds = new HashSet<int>();
                foreach (var input in validTasks)
                {
                    if (input.Id.HasValue && tasksById.TryGetValue(input.Id.Value, out var existingTask))
                    {
                        existingTask.Title = input.Title.Trim();
                        existingTask.Description = NormalizeOptional(input.Description);
                        if (input.IsCompleted && !existingTask.IsCompleted)
                        {
                            existingTask.IsCompleted = true;
                            existingTask.CompletedAt = now;
                        }
                        else if (!input.IsCompleted)
                        {
                            existingTask.IsCompleted = false;
                            existingTask.CompletedAt = null;
                        }
                        retainedTaskIds.Add(input.Id.Value);
                    }
                    else
                    {
                        context.InstallationTasks.Add(new InstallationTask
                        {
                            InstallationOrderId = installation.Id,
                            Title = input.Title.Trim(),
                            Description = NormalizeOptional(input.Description),
                            IsCompleted = input.IsCompleted,
                            CompletedAt = input.IsCompleted ? now : null
                        });
                    }
                }
                foreach (var removed in existingTasks.Where(t => !retainedTaskIds.Contains(t.Id)))
                {
                    context.InstallationTasks.Remove(removed);
                }

                // Stok rezervasyonu: ProductId'li montaj malzemeleri, iş emri stok kalemlerine
                // senkronize edilir ve mevcut rezervasyon mekanizmasıyla ayrılır (çekme akışı).
                // Not: ServiceJobItem.QuantityUsed int olduğu için kesirli miktarlar üste yuvarlanır
                // (2.5m kablo → 3 adet rezerve); tüketim de aynı değer üzerinden yapılır, tutarlıdır.
                var stockItems = validMaterials
                    .Where(m => m.ProductId.HasValue && m.ProductId.Value > 0 && m.Quantity > 0)
                    .Select(m => new ServiceJobItem
                    {
                        ServiceJobId = job.Id,
                        ProductId = m.ProductId,
                        QuantityUsed = Math.Max(1, (int)Math.Ceiling(m.Quantity)),
                        UnitPrice = Math.Max(0m, m.UnitPrice),
                        UnitCost = 0m
                    })
                    .ToList();

                var oldStockItems = await context.ServiceJobItems
                    .Where(i => i.ServiceJobId == job.Id)
                    .ToListAsync(cancellationToken);
                context.ServiceJobItems.RemoveRange(oldStockItems);
                foreach (var item in stockItems) context.ServiceJobItems.Add(item);

                var reservationResult = await SynchronizeReservationsAsync(
                    context,
                    job,
                    stockItems,
                    NormalizeUser(request.ChangedBy),
                    cancellationToken);
                if (reservationResult.IsFailure)
                {
                    await RollbackIfPresentAsync(transaction, cancellationToken);
                    return Result.Failure<InstallationSaveResult>(reservationResult.Error);
                }

                context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = job.Id,
                    Date = now,
                    JobStatusChange = null,
                    TechnicianNote = $"Montaj emri güncellendi ({validMaterials.Count} malzeme, {validTasks.Count} görev, {validTasks.Count(t => t.IsCompleted)} görev tamam).",
                    Action = "InstallationUpdated",
                    UserId = NormalizeUser(request.ChangedBy),
                    PerformedAt = now
                });

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);

                return Result.Success(new InstallationSaveResult(installation.Id));
            }
            catch (Exception ex)
            {
                await RollbackIfPresentAsync(transaction, cancellationToken);
                return Result.Failure<InstallationSaveResult>($"Montaj emri kaydedilemedi: {ex.Message}");
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<Result<ServiceJobStatusChangeResult>> CompleteInstallationAsync(
        CompleteInstallationRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorizationService.Authorize(ApplicationPermission.ManageServiceJobs);
        if (authorization.IsFailure)
        {
            return Result.Failure<ServiceJobStatusChangeResult>(authorization.Error);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var job = await context.ServiceJobs
                    .FirstOrDefaultAsync(item => item.Id == request.JobId, cancellationToken);
                if (job is null)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>($"İş emri bulunamadı (ID: {request.JobId}).");
                }

                var previousStatus = job.Status;
                var validation = _statusPolicy.ValidateTransition(previousStatus, JobStatus.InstallationCompleted);
                if (validation.IsFailure)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>(validation.Error);
                }

                var installation = await context.InstallationOrders
                    .Include(i => i.Materials)
                    .FirstOrDefaultAsync(i => i.ServiceJobId == job.Id, cancellationToken);
                if (installation is null)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>(
                        "Montaj emri bulunamadı; önce montaj planlanmalıdır.");
                }

                // Montaj tamamlama doğrulaması: en az bir malzeme ve işçilik saati > 0.
                int materialCount = installation.Materials.Count;
                if (materialCount == 0)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>(
                        "Montajı tamamlamak için en az bir malzeme girilmelidir.");
                }
                if (request.LaborHours <= 0m)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>(
                        "Montajı tamamlamak için işçilik saati (LaborHours) girilmelidir.");
                }

                var now = DateTime.UtcNow;

                // Stok tüketimi ve müşteri aktivitesi (varsa rezervasyonlardan)
                var completion = await ApplyStockCompletionAsync(context, job, cancellationToken);
                if (completion.IsFailure)
                {
                    await RollbackIfPresentAsync(transaction, cancellationToken);
                    return Result.Failure<ServiceJobStatusChangeResult>(completion.Error);
                }

                job.CompletedDate = now;
                job.RepairStatus = RepairStatus.Delivered;
                job.Status = JobStatus.InstallationCompleted;
                job.ModifiedDate = now;
                await ApplyCustomerCompletionAsync(context, job, NormalizeUser(request.ChangedBy), cancellationToken);

                // Gerçek kullanılan malzemeler / tamamlanma verileri montaj emrinde saklanır
                installation.CompletedAt = now;
                installation.CompletionTechnician = NormalizeOptional(request.CompletionTechnician) ?? job.AssignedTechnician;
                installation.DeliveryNote = NormalizeOptional(request.DeliveryNote);
                installation.CustomerSignature = request.CustomerSignature;
                installation.LaborHours = Math.Max(0m, request.LaborHours);

                context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = job.Id,
                    Date = now,
                    JobStatusChange = JobStatus.InstallationCompleted,
                    TechnicianNote = string.IsNullOrWhiteSpace(request.DeliveryNote)
                        ? "Montaj tamamlandı."
                        : $"Montaj tamamlandı: {request.DeliveryNote.Trim()}",
                    Action = "InstallationCompleted",
                    Notes = $"Önceki durum: {previousStatus}",
                    UserId = NormalizeUser(request.ChangedBy),
                    PerformedAt = now
                });

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);

                return Result.Success(new ServiceJobStatusChangeResult(
                    job.Id, previousStatus, job.Status, job.CompletedDate));
            }
            catch (Exception ex)
            {
                await RollbackIfPresentAsync(transaction, cancellationToken);
                return Result.Failure<ServiceJobStatusChangeResult>($"Montaj tamamlanamadı: {ex.Message}");
            }
        }, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// İşi teslim eder (Paket 7): teslim kaydı oluşturur/günceller, ödeme bilgilerini saklar
    /// ve işi Delivered durumuna alır. Doğrulama: durum geçişi (InstallationCompleted → Delivered)
    /// ve ödeme tutarlılığı — kısmi/ödenmiş durumda tahsilat tutarı &gt; 0, ödenmemiş durumda 0 olmalı.
    /// </summary>
    public async Task<Result<ServiceJobStatusChangeResult>> CompleteDeliveryAsync(
        CompleteDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorizationService.Authorize(ApplicationPermission.ManageServiceJobs);
        if (authorization.IsFailure)
        {
            return Result.Failure<ServiceJobStatusChangeResult>(authorization.Error);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var job = await context.ServiceJobs
                    .FirstOrDefaultAsync(item => item.Id == request.JobId, cancellationToken);
                if (job is null)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>($"İş emri bulunamadı (ID: {request.JobId}).");
                }

                // Ödeme tutarlılığı doğrulaması (teslim edilmiş kayıtlarda güncelleme için de geçerli)
                if (request.PaidAmount < 0m)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>("Tahsilat tutarı negatif olamaz.");
                }
                if (request.PaymentStatus == PaymentStatus.Unpaid && request.PaidAmount > 0m)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>(
                        "Ödeme durumu 'Tahsilat Bekleniyor' iken tahsilat tutarı girilemez; önce ödeme durumunu güncelleyin.");
                }
                if (request.PaymentStatus != PaymentStatus.Unpaid && request.PaidAmount <= 0m)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>(
                        "Kısmi ödendi / ödendi durumu için tahsilat tutarı girilmelidir.");
                }

                var previousStatus = job.Status;
                bool isNewDelivery = previousStatus != JobStatus.Delivered;

                if (isNewDelivery)
                {
                    var validation = _statusPolicy.ValidateTransition(previousStatus, JobStatus.Delivered);
                    if (validation.IsFailure)
                    {
                        return Result.Failure<ServiceJobStatusChangeResult>(validation.Error);
                    }
                }

                var now = DateTime.UtcNow;
                var delivery = await context.JobDeliveries
                    .FirstOrDefaultAsync(d => d.ServiceJobId == job.Id, cancellationToken);
                if (delivery is null)
                {
                    delivery = new JobDelivery { ServiceJobId = job.Id, CreatedDate = now };
                    context.JobDeliveries.Add(delivery);
                }

                delivery.DeliveryDate = now;
                delivery.DeliveredBy = NormalizeOptional(request.DeliveredBy);
                delivery.DeliveryNote = NormalizeOptional(request.DeliveryNote);
                delivery.CustomerSignature = string.IsNullOrWhiteSpace(request.CustomerSignature)
                    ? null
                    : request.CustomerSignature;
                delivery.PaymentStatus = request.PaymentStatus;
                delivery.PaymentMethod = request.PaymentMethod;
                delivery.PaidAmount = request.PaidAmount;
                delivery.InvoiceNumber = NormalizeOptional(request.InvoiceNumber);

                job.Status = JobStatus.Delivered;
                job.CompletedDate ??= now;
                job.IsCustomerApproved = true;
                job.CustomerSignature = delivery.CustomerSignature ?? job.CustomerSignature;
                job.ModifiedDate = now;

                context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = job.Id,
                    Date = now,
                    JobStatusChange = JobStatus.Delivered,
                    TechnicianNote = $"İş teslim edildi. Ödeme: {PaymentStatusLabels.Map(request.PaymentStatus)} ({request.PaidAmount:N2} ₺).",
                    Action = "DeliveryCompleted",
                    Notes = isNewDelivery ? $"Önceki durum: {previousStatus}" : "Teslim kaydı güncellendi (ödeme).",
                    UserId = NormalizeUser(request.ChangedBy),
                    PerformedAt = now
                });

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);

                return Result.Success(new ServiceJobStatusChangeResult(
                    job.Id, previousStatus, job.Status, job.CompletedDate));
            }
            catch (Exception ex)
            {
                await RollbackIfPresentAsync(transaction, cancellationToken);
                return Result.Failure<ServiceJobStatusChangeResult>($"İş teslim edilemedi: {ex.Message}");
            }
        }, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// İş emri tarihçesine akış odaklı bir kayıt ekler (kaydetme çağrıyı yapan tarafa aittir).
    /// </summary>
    private static async Task AppendWorkflowHistoryAsync(
        AppDbContext context,
        int serviceJobId,
        string note,
        string action,
        string changedBy,
        CancellationToken cancellationToken)
    {
        context.ServiceJobHistories.Add(new ServiceJobHistory
        {
            ServiceJobId = serviceJobId,
            Date = DateTime.UtcNow,
            JobStatusChange = null,
            TechnicianNote = note,
            Action = action,
            UserId = NormalizeUser(changedBy),
            PerformedAt = DateTime.UtcNow
        });
    }

    public async Task<Result<ServiceJobDeleteResult>> DeleteAsync(
        int jobId,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var manageAuthorization = _authorizationService.Authorize(ApplicationPermission.ManageServiceJobs);
        if (manageAuthorization.IsFailure) return Result.Failure<ServiceJobDeleteResult>(manageAuthorization.Error);
        var deleteAuthorization = _authorizationService.Authorize(ApplicationPermission.DeleteRecords);
        if (deleteAuthorization.IsFailure) return Result.Failure<ServiceJobDeleteResult>(deleteAuthorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var job = await context.ServiceJobs.FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
                if (job is null) return Result.Failure<ServiceJobDeleteResult>($"İş emri bulunamadı (ID: {jobId}).");
                if (job.Status == JobStatus.Completed || job.IsStockDeducted)
                {
                    return Result.Failure<ServiceJobDeleteResult>(
                        "Tamamlanmış veya stok tüketimi yapılmış iş emri silinemez; yeni bir telafi kaydı oluşturulmalıdır.");
                }

                string referenceId = job.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var reservations = await context.StockReservations
                    .Where(item => item.ReferenceType == ReservationReferenceType &&
                                   item.ReferenceId == referenceId && item.IsActive)
                    .ToListAsync(cancellationToken);
                foreach (var reservation in reservations) reservation.IsActive = false;

                context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = job.Id,
                    Date = DateTime.UtcNow,
                    JobStatusChange = job.Status,
                    TechnicianNote = "İş emri kullanıcı talebiyle silindi.",
                    Action = "Deleted",
                    Notes = $"Silinmeden önceki durum: {job.Status}",
                    UserId = NormalizeUser(changedBy),
                    PerformedAt = DateTime.UtcNow
                });
                job.IsDeleted = true;
                job.DeletedAt = DateTime.UtcNow;
                job.DeletedBy = NormalizeUser(changedBy);
                job.ModifiedDate = DateTime.UtcNow;
                job.ModifiedBy = NormalizeUser(changedBy);
                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);
                return Result.Success(new ServiceJobDeleteResult(jobId));
            }
            catch (Exception ex)
            {
                await RollbackIfPresentAsync(transaction, cancellationToken);
                return Result.Failure<ServiceJobDeleteResult>($"İş emri silinemedi: {ex.Message}");
            }
        }, cancellationToken: cancellationToken);
    }

    private async Task<Result<ServiceJobStatusChangeResult>> ChangeStatusCoreAsync(
        int jobId,
        JobStatus requestedStatus,
        decimal? laborCost,
        decimal? discountAmount,
        string? transitionNote,
        string changedBy,
        CancellationToken cancellationToken)
    {
        var authorization = _authorizationService.Authorize(ApplicationPermission.ManageServiceJobs);
        if (authorization.IsFailure)
        {
            return Result.Failure<ServiceJobStatusChangeResult>(authorization.Error);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExecuteInTransactionAsync(async transaction =>
        {
            try
            {
                var job = await context.ServiceJobs
                    .FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
                if (job is null)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>($"İş emri bulunamadı (ID: {jobId}).");
                }

                var previousStatus = job.Status;
                var validation = _statusPolicy.ValidateTransition(previousStatus, requestedStatus);
                if (validation.IsFailure)
                {
                    return Result.Failure<ServiceJobStatusChangeResult>(validation.Error);
                }

                // Montaj aşamaları yalnızca iş akışı verileri mevcutsa geçerli olur
                if (requestedStatus == JobStatus.InstallationPlanned)
                {
                    var acceptedQuote = await context.WorkOrderQuotations
                        .AnyAsync(q => q.ServiceJobId == job.Id && q.Status == QuotationStatus.Accepted, cancellationToken);
                    if (!acceptedQuote)
                    {
                        return Result.Failure<ServiceJobStatusChangeResult>(
                            "Montaj yalnızca kabul edilmiş teklif için planlanabilir. Önce teklifi kabul edin.");
                    }
                }

                if (requestedStatus == JobStatus.InstallationCompleted)
                {
                    var hasInstallation = await context.InstallationOrders
                        .AnyAsync(i => i.ServiceJobId == job.Id, cancellationToken);
                    if (!hasInstallation)
                    {
                        return Result.Failure<ServiceJobStatusChangeResult>(
                            "Montaj emri bulunamadı; önce montaj planlanmalıdır.");
                    }
                }

                if (previousStatus == requestedStatus)
                {
                    return Result.Success(new ServiceJobStatusChangeResult(
                        job.Id, previousStatus, job.Status, job.CompletedDate));
                }

                if (laborCost.HasValue)
                {
                    job.LaborCost = Math.Max(0m, laborCost.Value);
                }

                if (discountAmount.HasValue)
                {
                    job.DiscountAmount = Math.Max(0m, discountAmount.Value);
                }

                if (requestedStatus == JobStatus.Completed || requestedStatus == JobStatus.InstallationCompleted)
                {
                    var completion = await ApplyStockCompletionAsync(context, job, cancellationToken);
                    if (completion.IsFailure)
                    {
                        await RollbackIfPresentAsync(transaction, cancellationToken);
                        return Result.Failure<ServiceJobStatusChangeResult>(completion.Error);
                    }

                    job.CompletedDate = DateTime.UtcNow;
                    job.RepairStatus = RepairStatus.Delivered;
                    await ApplyCustomerCompletionAsync(context, job, NormalizeUser(changedBy), cancellationToken);
                }

                if (requestedStatus == JobStatus.Cancelled)
                {
                    string referenceId = job.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var activeReservations = await context.StockReservations
                        .Where(item => item.ReferenceType == ReservationReferenceType &&
                                       item.ReferenceId == referenceId && item.IsActive)
                        .ToListAsync(cancellationToken);
                    foreach (var res in activeReservations) res.IsActive = false;
                    job.IsStockReserved = false;
                }

                job.Status = requestedStatus;
                job.ModifiedDate = DateTime.UtcNow;

                context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = job.Id,
                    Date = DateTime.UtcNow,
                    JobStatusChange = requestedStatus,
                    TechnicianNote = string.IsNullOrWhiteSpace(transitionNote)
                        ? $"Durum değiştirildi: {previousStatus} → {requestedStatus}"
                        : transitionNote.Trim(),
                    Action = "StatusChanged",
                    Notes = $"Önceki durum: {previousStatus}",
                    UserId = NormalizeUser(changedBy),
                    PerformedAt = DateTime.UtcNow
                });

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);

                return Result.Success(new ServiceJobStatusChangeResult(
                    job.Id, previousStatus, job.Status, job.CompletedDate));
            }
            catch (Exception ex)
            {
                await RollbackIfPresentAsync(transaction, cancellationToken);
                return Result.Failure<ServiceJobStatusChangeResult>($"Durum güncelleme hatası: {ex.Message}");
            }
        }, cancellationToken: cancellationToken);
    }

    private static async Task<Result<int>> SynchronizeReservationsAsync(
        AppDbContext context,
        ServiceJob job,
        IReadOnlyCollection<ServiceJobItem> items,
        string changedBy,
        CancellationToken cancellationToken)
    {
        string referenceId = job.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var existingReservations = await context.StockReservations
            .Where(reservation => reservation.ReferenceType == ReservationReferenceType &&
                                  reservation.ReferenceId == referenceId &&
                                  reservation.IsActive)
            .ToListAsync(cancellationToken);

        var requestedByProduct = items
            .GroupBy(item => item.ProductId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.QuantityUsed));
        var existingByProduct = existingReservations
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        if (DictionariesEqual(requestedByProduct, existingByProduct))
        {
            job.IsStockReserved = existingReservations.Count > 0;
            return Result.Success(existingReservations.Count);
        }

        foreach (var reservation in existingReservations)
        {
            reservation.IsActive = false;
        }

        if (requestedByProduct.Count == 0)
        {
            job.IsStockReserved = false;
            return Result.Success(0);
        }

        var productIds = requestedByProduct.Keys.ToArray();
        var inventories = await context.Inventories
            .Where(inventory => inventory.ProductId.HasValue &&
                                inventory.WarehouseId.HasValue &&
                                productIds.Contains(inventory.ProductId.Value))
            .OrderBy(inventory => inventory.WarehouseId)
            .ToListAsync(cancellationToken);

        var reservationsByOthers = await context.StockReservations
            .Where(reservation => productIds.Contains(reservation.ProductId) &&
                                  reservation.IsActive &&
                                  (!reservation.ExpiresAt.HasValue || reservation.ExpiresAt > DateTime.UtcNow) &&
                                  !(reservation.ReferenceType == ReservationReferenceType && reservation.ReferenceId == referenceId))
            .GroupBy(reservation => new { reservation.ProductId, reservation.WarehouseId })
            .Select(group => new { group.Key.ProductId, group.Key.WarehouseId, Quantity = group.Sum(item => item.Quantity) })
            .ToListAsync(cancellationToken);

        var reservedLookup = reservationsByOthers.ToDictionary(
            item => (item.ProductId, item.WarehouseId),
            item => item.Quantity);
        var newReservations = new List<StockReservation>();

        foreach (var requested in requestedByProduct)
        {
            int remaining = requested.Value;
            foreach (var inventory in inventories.Where(item => item.ProductId == requested.Key))
            {
                int warehouseId = inventory.WarehouseId!.Value;
                int reserved = reservedLookup.GetValueOrDefault((requested.Key, warehouseId));
                int available = Math.Max(0, inventory.Quantity - reserved);
                if (available == 0) continue;

                int allocation = Math.Min(available, remaining);
                newReservations.Add(new StockReservation
                {
                    ProductId = requested.Key,
                    WarehouseId = warehouseId,
                    Quantity = allocation,
                    ReferenceType = ReservationReferenceType,
                    ReferenceId = referenceId,
                    ReservedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    ReservedBy = changedBy,
                    IsActive = true
                });
                remaining -= allocation;
                if (remaining == 0) break;
            }

            if (remaining > 0)
            {
                return Result.Failure<int>(
                    $"Ürün #{requested.Key} için yeterli kullanılabilir stok yok. Eksik miktar: {remaining}.");
            }
        }

        context.StockReservations.AddRange(newReservations);
        job.IsStockReserved = true;
        return Result.Success(newReservations.Count);
    }

    private static async Task<Result> ValidateRequestedStockAsync(
        AppDbContext context,
        IReadOnlyCollection<ServiceJobItem> items,
        int? currentJobId,
        CancellationToken cancellationToken)
    {
        var requestedByProduct = items
            .GroupBy(item => item.ProductId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.QuantityUsed));
        if (requestedByProduct.Count == 0)
        {
            return Result.Success();
        }

        var productIds = requestedByProduct.Keys.ToArray();
        var inventoryByProduct = await context.Inventories
            .Where(item => item.ProductId.HasValue && productIds.Contains(item.ProductId.Value))
            .GroupBy(item => item.ProductId!.Value)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToDictionaryAsync(item => item.ProductId, item => item.Quantity, cancellationToken);

        string? currentReferenceId = currentJobId?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var reservedByProduct = await context.StockReservations
            .Where(item => productIds.Contains(item.ProductId) &&
                           item.IsActive &&
                           (!item.ExpiresAt.HasValue || item.ExpiresAt > DateTime.UtcNow) &&
                           (currentReferenceId == null ||
                            item.ReferenceType != ReservationReferenceType ||
                            item.ReferenceId != currentReferenceId))
            .GroupBy(item => item.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToDictionaryAsync(item => item.ProductId, item => item.Quantity, cancellationToken);

        foreach (var requested in requestedByProduct)
        {
            int available = inventoryByProduct.GetValueOrDefault(requested.Key) -
                            reservedByProduct.GetValueOrDefault(requested.Key);
            if (available < requested.Value)
            {
                return Result.Failure(
                    $"Ürün #{requested.Key} için yeterli kullanılabilir stok yok. Mevcut: {Math.Max(0, available)}, İstenen: {requested.Value}.");
            }
        }

        return Result.Success();
    }

    private static async Task<Result> ApplyStockCompletionAsync(
        AppDbContext context,
        ServiceJob job,
        CancellationToken cancellationToken)
    {
        if (job.IsStockDeducted)
        {
            return Result.Success();
        }

        var requiredByProduct = await context.ServiceJobItems
            .Where(item => item.ServiceJobId == job.Id && item.ProductId.HasValue)
            .GroupBy(item => item.ProductId!.Value)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(item => item.QuantityUsed) })
            .ToListAsync(cancellationToken);

        if (requiredByProduct.Count == 0)
        {
            job.IsStockReserved = false;
            job.IsStockDeducted = true;
            return Result.Success();
        }

        string referenceId = job.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var reservations = await context.StockReservations
            .Where(item => item.ReferenceType == ReservationReferenceType &&
                           item.ReferenceId == referenceId &&
                           item.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var required in requiredByProduct)
        {
            int reservedQuantity = reservations
                .Where(item => item.ProductId == required.ProductId)
                .Sum(item => item.Quantity);
            if (reservedQuantity < required.Quantity)
            {
                return Result.Failure(
                    $"Ürün #{required.ProductId} için stok rezervasyonu eksik. İş tamamlanmadan önce malzemeleri yeniden kaydedin.");
            }
        }

        var reservationKeys = reservations
            .Select(item => new { item.ProductId, item.WarehouseId })
            .Distinct()
            .ToList();
        var productIds = reservationKeys.Select(item => item.ProductId).Distinct().ToArray();
        var warehouseIds = reservationKeys.Select(item => item.WarehouseId).Distinct().ToArray();
        var inventories = await context.Inventories
            .Where(item => item.ProductId.HasValue && item.WarehouseId.HasValue &&
                           productIds.Contains(item.ProductId.Value) &&
                           warehouseIds.Contains(item.WarehouseId.Value))
            .ToListAsync(cancellationToken);

        foreach (var reservation in reservations)
        {
            var inventory = inventories.FirstOrDefault(item =>
                item.ProductId == reservation.ProductId && item.WarehouseId == reservation.WarehouseId);
            if (inventory is null || inventory.Quantity < reservation.Quantity)
            {
                return Result.Failure(
                    $"Ürün #{reservation.ProductId} için depo stoğu rezervasyonu karşılamıyor.");
            }

            inventory.Quantity -= reservation.Quantity;
            reservation.IsActive = false;
        }

        var products = await context.Products
            .Where(product => productIds.Contains(product.Id))
            .ToListAsync(cancellationToken);
        foreach (var product in products)
        {
            int deducted = requiredByProduct
                .Where(item => item.ProductId == product.Id)
                .Sum(item => item.Quantity);
            product.TotalStockQuantity = Math.Max(0, product.TotalStockQuantity - deducted);
        }

        job.IsStockReserved = false;
        job.IsStockDeducted = true;
        return Result.Success();
    }

    private static async Task ApplyCustomerCompletionAsync(
        AppDbContext context,
        ServiceJob job,
        string changedBy,
        CancellationToken cancellationToken)
    {
        var customer = await context.Customers.FindAsync([job.CustomerId], cancellationToken);
        if (customer is not null)
        {
            customer.LastInteractionDate = DateTime.UtcNow;
            customer.LastPurchaseDate = DateTime.UtcNow;
            customer.TotalSpent += job.TotalAmount;
            customer.LoyaltyPoints += (int)(job.TotalAmount / 100m);
        }

        context.CustomerActivities.Add(new CustomerActivity
        {
            CustomerId = job.CustomerId,
            Type = ActivityType.ServiceJobCompleted,
            Description = $"İş emri tamamlandı: #{job.Id} - Toplam: {job.TotalAmount:N2} ₺",
            RelatedId = job.Id,
            RelatedType = ReservationReferenceType,
            CreatedBy = changedBy,
            CreatedDate = DateTime.UtcNow
        });
    }

    private static bool DictionariesEqual(
        IReadOnlyDictionary<int, int> left,
        IReadOnlyDictionary<int, int> right) =>
        left.Count == right.Count && left.All(pair => right.GetValueOrDefault(pair.Key) == pair.Value);

    private static string NormalizeUser(string? changedBy) =>
        string.IsNullOrWhiteSpace(changedBy) ? "Sistem" : changedBy.Trim()[..Math.Min(changedBy.Trim().Length, 100)];

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(
        AppDbContext context,
        CancellationToken cancellationToken) =>
        context.Database.IsRelational()
            ? await context.Database.CreateExecutionStrategy().ExecuteAsync(async () => await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
            : null;

    private static Task CommitIfPresentAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    private static Task RollbackIfPresentAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction?.RollbackAsync(cancellationToken) ?? Task.CompletedTask;
}
