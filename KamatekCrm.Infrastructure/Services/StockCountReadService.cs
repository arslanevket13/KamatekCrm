using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Inventory;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Infrastructure.Services;

public sealed class StockCountReadService : IStockCountReadService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IApplicationAuthorizationService _authorization;

    public StockCountReadService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IApplicationAuthorizationService authorization)
    {
        _dbContextFactory = dbContextFactory;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<StockCountWarehouseDto>>> GetWarehousesAsync(
        CancellationToken cancellationToken = default)
    {
        var authorization = AuthorizeRead();
        if (authorization.IsFailure) return Result.Failure<IReadOnlyList<StockCountWarehouseDto>>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Warehouses.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new StockCountWarehouseDto(item.Id, item.Name, item.IsQuarantine))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<StockCountWarehouseDto>>(rows);
    }

    public async Task<Result<IReadOnlyList<StockCountProductDto>>> GetWarehouseSnapshotAsync(
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        var authorization = AuthorizeRead();
        if (authorization.IsFailure) return Result.Failure<IReadOnlyList<StockCountProductDto>>(authorization.Error);
        if (warehouseId <= 0) return Result.Failure<IReadOnlyList<StockCountProductDto>>("Geçerli bir depo seçilmelidir.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Inventories.AsNoTracking()
            .Where(item => item.WarehouseId == warehouseId && item.ProductId.HasValue && item.Product != null)
            .OrderBy(item => item.Product!.ProductName)
            .Select(item => new StockCountProductDto(
                item.ProductId!.Value,
                item.Product!.SKU,
                item.Product.Barcode,
                item.Product.ProductName,
                item.Product.ModelName,
                item.Product.Unit,
                item.Quantity,
                item.Product.PurchasePrice))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<StockCountProductDto>>(rows);
    }

    public async Task<Result<IReadOnlyList<StockCountProductDto>>> SearchProductsAsync(
        int warehouseId,
        string searchText,
        int take = 15,
        CancellationToken cancellationToken = default)
    {
        var authorization = AuthorizeRead();
        if (authorization.IsFailure) return Result.Failure<IReadOnlyList<StockCountProductDto>>(authorization.Error);
        if (warehouseId <= 0) return Result.Failure<IReadOnlyList<StockCountProductDto>>("Geçerli bir depo seçilmelidir.");
        string normalized = searchText.Trim().ToLower();
        if (normalized.Length < 2) return Result.Success<IReadOnlyList<StockCountProductDto>>([]);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Products.AsNoTracking()
            .Where(item => item.ProductName.ToLower().Contains(normalized) ||
                           item.SKU.ToLower().Contains(normalized) ||
                           item.Barcode.ToLower().Contains(normalized) ||
                           item.ModelName.ToLower().Contains(normalized))
            .OrderBy(item => item.ProductName)
            .Take(Math.Clamp(take, 1, 100))
            .Select(item => new StockCountProductDto(
                item.Id,
                item.SKU,
                item.Barcode,
                item.ProductName,
                item.ModelName,
                item.Unit,
                context.Inventories
                    .Where(inventory => inventory.ProductId == item.Id && inventory.WarehouseId == warehouseId)
                    .Select(inventory => inventory.Quantity)
                    .FirstOrDefault(),
                item.PurchasePrice))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<StockCountProductDto>>(rows);
    }

    public async Task<Result<IReadOnlyList<StockCountHistoryDto>>> GetHistoryAsync(
        int take = 30,
        CancellationToken cancellationToken = default)
    {
        var authorization = AuthorizeRead();
        if (authorization.IsFailure) return Result.Failure<IReadOnlyList<StockCountHistoryDto>>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        int limit = Math.Clamp(take, 1, 200);
        var sessions = await context.StockCountSessions.AsNoTracking()
            .OrderByDescending(item => item.CountedAt)
            .Take(limit)
            .Select(item => new StockCountHistoryDto(
                item.Id,
                item.CountedAt,
                item.Warehouse.Name,
                item.Mode,
                item.ProductCount,
                item.TotalPositiveDifference + item.TotalNegativeDifference,
                item.FinancialDifference,
                item.ReferenceNumber,
                item.CountedBy))
            .ToListAsync(cancellationToken);

        var legacyTransactions = await context.StockTransactions.AsNoTracking()
            .Where(item => item.ReferenceId != null &&
                           (item.ReferenceId.StartsWith("COUNT-") || item.ReferenceId.StartsWith("MANUAL-")) &&
                           (item.TransactionType == StockTransactionType.AdjustmentPlus ||
                            item.TransactionType == StockTransactionType.AdjustmentMinus))
            .Include(item => item.SourceWarehouse)
            .Include(item => item.TargetWarehouse)
            .OrderByDescending(item => item.Date)
            .Take(limit * 20)
            .ToListAsync(cancellationToken);
        var legacy = legacyTransactions
            .GroupBy(item => item.ReferenceId)
            .Select(group =>
            {
                var first = group.First();
                int positive = group.Where(item => item.TransactionType == StockTransactionType.AdjustmentPlus)
                    .Sum(item => item.Quantity);
                int negative = group.Where(item => item.TransactionType == StockTransactionType.AdjustmentMinus)
                    .Sum(item => item.Quantity);
                return new StockCountHistoryDto(
                    null,
                    first.Date,
                    first.TargetWarehouse?.Name ?? first.SourceWarehouse?.Name ?? "Bilinmiyor",
                    group.Key.StartsWith("MANUAL-", StringComparison.OrdinalIgnoreCase)
                        ? StockCountMode.Manual
                        : StockCountMode.FullWarehouse,
                    group.Count(),
                    positive - negative,
                    group.Sum(item => (item.TransactionType == StockTransactionType.AdjustmentPlus ? 1 : -1) *
                                      item.Quantity * item.UnitCost),
                    group.Key,
                    string.IsNullOrWhiteSpace(first.UserId) ? "Eski kayıt" : first.UserId);
            });

        var combined = sessions.Concat(legacy)
            .OrderByDescending(item => item.CountedAt)
            .Take(limit)
            .ToList();
        return Result.Success<IReadOnlyList<StockCountHistoryDto>>(combined);
    }

    public async Task<Result<IReadOnlyList<StockCountHistoryLineDto>>> GetHistoryDetailAsync(
        int? sessionId,
        string referenceNumber,
        CancellationToken cancellationToken = default)
    {
        var authorization = AuthorizeRead();
        if (authorization.IsFailure) return Result.Failure<IReadOnlyList<StockCountHistoryLineDto>>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (sessionId.HasValue)
        {
            var rows = await context.StockCountSessionItems.AsNoTracking()
                .Where(item => item.StockCountSessionId == sessionId.Value)
                .OrderBy(item => item.ProductName)
                .Select(item => new StockCountHistoryLineDto(
                    item.ProductCode,
                    item.ProductName,
                    item.SystemQuantity,
                    item.CountedQuantity,
                    item.Difference,
                    item.FinancialDifference))
                .ToListAsync(cancellationToken);
            return Result.Success<IReadOnlyList<StockCountHistoryLineDto>>(rows);
        }

        if (string.IsNullOrWhiteSpace(referenceNumber))
            return Result.Failure<IReadOnlyList<StockCountHistoryLineDto>>("Sayım geçmişi referansı geçersiz.");
        var legacyRows = await context.StockTransactions.AsNoTracking()
            .Where(item => item.ReferenceId == referenceNumber)
            .OrderBy(item => item.Product!.ProductName)
            .Select(item => new StockCountHistoryLineDto(
                item.Product != null ? item.Product.SKU : $"P-{item.ProductId}",
                item.Product != null ? item.Product.ProductName : "Ürün",
                0,
                0,
                item.TransactionType == StockTransactionType.AdjustmentPlus ? item.Quantity : -item.Quantity,
                (item.TransactionType == StockTransactionType.AdjustmentPlus ? 1 : -1) * item.Quantity * item.UnitCost))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<StockCountHistoryLineDto>>(legacyRows);
    }

    private Result AuthorizeRead() => _authorization.Authorize(ApplicationPermission.AdjustInventory);
}
