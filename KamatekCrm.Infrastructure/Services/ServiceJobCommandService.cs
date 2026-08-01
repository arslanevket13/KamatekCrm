using System.Data;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
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

        if (request.Job.CustomerId <= 0)
        {
            return Result.Failure<ServiceJobSaveResult>("İş emri için geçerli bir müşteri seçilmelidir.");
        }

        var validItems = request.Items
            .Where(item => item.ProductId.HasValue && item.ProductId.Value > 0 && item.QuantityUsed > 0)
            .ToList();

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginTransactionIfSupportedAsync(context, cancellationToken);

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

            ServiceJob trackedJob;
            if (request.IsEditing)
            {
                trackedJob = await context.ServiceJobs
                    .FirstOrDefaultAsync(job => job.Id == request.Job.Id, cancellationToken)
                    ?? throw new InvalidOperationException($"İş emri bulunamadı (ID: {request.Job.Id}).");

                context.Entry(trackedJob).CurrentValues.SetValues(request.Job);

                var oldItems = await context.ServiceJobItems
                    .Where(item => item.ServiceJobId == trackedJob.Id)
                    .ToListAsync(cancellationToken);
                context.ServiceJobItems.RemoveRange(oldItems);
            }
            else
            {
                trackedJob = new ServiceJob();
                context.Entry(trackedJob).CurrentValues.SetValues(request.Job);
                trackedJob.Id = 0;
                context.ServiceJobs.Add(trackedJob);
            }

            trackedJob.ModifiedDate = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            foreach (var item in validItems)
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
                validItems,
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
                reservationResult.Value));
        }
        catch (Exception ex)
        {
            await RollbackIfPresentAsync(transaction, cancellationToken);
            return Result.Failure<ServiceJobSaveResult>($"İş emri kaydedilemedi: {ex.Message}");
        }
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
        await using var transaction = await BeginTransactionIfSupportedAsync(context, cancellationToken);

        try
        {
            var job = await context.ServiceJobs
                .FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
            if (job is null)
            {
                return Result.Failure<ServiceJobQuoteConversionResult>($"İş emri bulunamadı (ID: {jobId}).");
            }

            var previousStatus = job.Status;
            var validation = _statusPolicy.ValidateTransition(previousStatus, JobStatus.Quoting);
            if (validation.IsFailure)
            {
                return Result.Failure<ServiceJobQuoteConversionResult>(validation.Error);
            }

            if (previousStatus != JobStatus.Quoting || !job.IsConvertedToQuote)
            {
                job.Status = JobStatus.Quoting;
                job.IsConvertedToQuote = true;
                job.ModifiedDate = DateTime.UtcNow;

                context.ServiceJobHistories.Add(new ServiceJobHistory
                {
                    ServiceJobId = job.Id,
                    Date = DateTime.UtcNow,
                    JobStatusChange = JobStatus.Quoting,
                    TechnicianNote = "Keşif kaydı teklife dönüştürüldü.",
                    Action = "ConvertedToQuote",
                    Notes = $"Önceki durum: {previousStatus}",
                    UserId = NormalizeUser(changedBy),
                    PerformedAt = DateTime.UtcNow
                });

                await context.SaveChangesAsync(cancellationToken);
                await CommitIfPresentAsync(transaction, cancellationToken);
            }

            return Result.Success(new ServiceJobQuoteConversionResult(
                job.Id, job.CustomerId, previousStatus, job.Status));
        }
        catch (Exception ex)
        {
            await RollbackIfPresentAsync(transaction, cancellationToken);
            return Result.Failure<ServiceJobQuoteConversionResult>($"İş emri teklife dönüştürülemedi: {ex.Message}");
        }
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
        await using var transaction = await BeginTransactionIfSupportedAsync(context, cancellationToken);

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

            if (requestedStatus == JobStatus.Completed)
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
            return Result.Failure<ServiceJobStatusChangeResult>($"İş emri durumu güncellenemedi: {ex.Message}");
        }
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

    private static async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(
        AppDbContext context,
        CancellationToken cancellationToken) =>
        context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

    private static Task CommitIfPresentAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

    private static Task RollbackIfPresentAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction?.RollbackAsync(cancellationToken) ?? Task.CompletedTask;
}
