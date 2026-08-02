using System.Data;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Inventory;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KamatekCrm.Infrastructure.Services;

public sealed class StockCountCommandService : IStockCountCommandService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IApplicationAuthorizationService _authorization;

    public StockCountCommandService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IApplicationAuthorizationService authorization)
    {
        _dbContextFactory = dbContextFactory;
        _authorization = authorization;
    }

    public async Task<Result<StockCountResult>> ApplyAsync(
        ApplyStockCountCommand command,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.AdjustInventory);
        if (authorization.IsFailure) return Result.Failure<StockCountResult>(authorization.Error);
        var validation = Validate(command);
        if (validation is not null) return Result.Failure<StockCountResult>(validation);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginTransactionIfSupportedAsync(context, cancellationToken);
        try
        {
            string idempotencyKey = command.IdempotencyKey.ToString("D");
            var existing = await context.StockCountSessions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return Result.Success(ToResult(existing, wasAlreadyApplied: true));
            }

            var warehouse = await context.Warehouses
                .SingleOrDefaultAsync(item => item.Id == command.WarehouseId && item.IsActive, cancellationToken);
            if (warehouse is null) return Result.Failure<StockCountResult>("Seçilen aktif depo bulunamadı.");

            var changedLines = command.Lines.Where(item => item.SystemQuantity != item.CountedQuantity).ToList();
            int[] productIds = changedLines.Select(item => item.ProductId).Distinct().ToArray();
            var products = await context.Products
                .Where(item => productIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
            if (products.Count != productIds.Length)
            {
                return Result.Failure<StockCountResult>("Sayım listesindeki ürünlerden biri bulunamadı.");
            }

            var allInventories = await context.Inventories
                .Where(item => item.ProductId.HasValue && productIds.Contains(item.ProductId.Value))
                .ToListAsync(cancellationToken);
            var targetInventories = allInventories
                .Where(item => item.WarehouseId == command.WarehouseId && item.ProductId.HasValue)
                .ToDictionary(item => item.ProductId!.Value);

            foreach (var line in changedLines)
            {
                if (targetInventories.TryGetValue(line.ProductId, out var inventory))
                {
                    if (inventory.Quantity != line.SystemQuantity)
                    {
                        await RollbackIfPresentAsync(transaction, cancellationToken);
                        return Result.Failure<StockCountResult>(
                            $"'{products[line.ProductId].ProductName}' stoğu sayım sırasında değişti. " +
                            $"Ekrandaki miktar: {line.SystemQuantity}, güncel miktar: {inventory.Quantity}. Listeyi yenileyip tekrar sayın.");
                    }
                }
                else
                {
                    if (command.Mode != StockCountMode.Manual || line.SystemQuantity != 0)
                    {
                        await RollbackIfPresentAsync(transaction, cancellationToken);
                        return Result.Failure<StockCountResult>(
                            $"'{products[line.ProductId].ProductName}' için depo stok satırı bulunamadı veya snapshot geçersiz.");
                    }

                    inventory = new Inventory
                    {
                        ProductId = line.ProductId,
                        WarehouseId = command.WarehouseId,
                        Quantity = 0,
                        AverageCost = products[line.ProductId].AverageCost
                    };
                    context.Inventories.Add(inventory);
                    allInventories.Add(inventory);
                    targetInventories[line.ProductId] = inventory;
                }
            }

            DateTime countedAt = command.CountedAt.Kind == DateTimeKind.Utc
                ? command.CountedAt
                : command.CountedAt.ToUniversalTime();
            string referenceNumber = $"SC-{DateTime.UtcNow:yyyyMMddHHmmss}-{command.IdempotencyKey.ToString("N")[..12]}";
            string changedBy = NormalizeUser(command.ChangedBy);
            var session = new StockCountSession
            {
                IdempotencyKey = idempotencyKey,
                ReferenceNumber = referenceNumber,
                WarehouseId = command.WarehouseId,
                CountedAt = countedAt,
                Mode = command.Mode,
                CountedBy = changedBy,
                ProductCount = changedLines.Count,
                TotalPositiveDifference = changedLines.Where(item => item.CountedQuantity > item.SystemQuantity)
                    .Sum(item => item.CountedQuantity - item.SystemQuantity),
                TotalNegativeDifference = changedLines.Where(item => item.CountedQuantity < item.SystemQuantity)
                    .Sum(item => item.CountedQuantity - item.SystemQuantity),
                FinancialDifference = changedLines.Sum(item =>
                    (item.CountedQuantity - item.SystemQuantity) * products[item.ProductId].PurchasePrice)
            };
            context.StockCountSessions.Add(session);

            foreach (var line in changedLines)
            {
                var product = products[line.ProductId];
                var inventory = targetInventories[line.ProductId];
                int difference = line.CountedQuantity - line.SystemQuantity;
                inventory.Quantity = line.CountedQuantity;

                var stockTransaction = new StockTransaction
                {
                    Date = countedAt,
                    ProductId = line.ProductId,
                    SourceWarehouseId = difference < 0 ? command.WarehouseId : null,
                    TargetWarehouseId = difference > 0 ? command.WarehouseId : null,
                    Quantity = Math.Abs(difference),
                    UnitCost = product.PurchasePrice,
                    TransactionType = difference > 0
                        ? StockTransactionType.AdjustmentPlus
                        : StockTransactionType.AdjustmentMinus,
                    Description = $"Stok sayımı - {warehouse.Name}. Sistem: {line.SystemQuantity}, " +
                                  $"Sayılan: {line.CountedQuantity}, Fark: {difference}",
                    ReferenceId = referenceNumber,
                    UserId = changedBy,
                    InventoryId = inventory.Id == 0 ? null : inventory.Id
                };
                context.StockTransactions.Add(stockTransaction);
                session.Items.Add(new StockCountSessionItem
                {
                    ProductId = line.ProductId,
                    ProductCode = product.SKU,
                    ProductName = product.ProductName,
                    SystemQuantity = line.SystemQuantity,
                    CountedQuantity = line.CountedQuantity,
                    Difference = difference,
                    UnitCost = product.PurchasePrice,
                    FinancialDifference = difference * product.PurchasePrice,
                    StockTransaction = stockTransaction
                });
            }

            foreach (int productId in productIds)
            {
                products[productId].TotalStockQuantity = allInventories
                    .Where(item => item.ProductId == productId)
                    .Sum(item => item.Quantity);
            }

            await context.SaveChangesAsync(cancellationToken);
            await CommitIfPresentAsync(transaction, cancellationToken);
            return Result.Success(ToResult(session, wasAlreadyApplied: false));
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackIfPresentAsync(transaction, cancellationToken);
            return Result.Failure<StockCountResult>(
                "Stok sayım sırasında başka bir kullanıcı tarafından değiştirildi. Listeyi yenileyip tekrar deneyin.");
        }
        catch (DbUpdateException ex)
        {
            await RollbackIfPresentAsync(transaction, cancellationToken);
            await using var verification = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            string idempotencyKey = command.IdempotencyKey.ToString("D");
            var existing = await verification.StockCountSessions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
            return existing is not null
                ? Result.Success(ToResult(existing, wasAlreadyApplied: true))
                : Result.Failure<StockCountResult>($"Stok sayımı uygulanamadı: {ex.Message}");
        }
        catch (Exception ex)
        {
            await RollbackIfPresentAsync(transaction, cancellationToken);
            return Result.Failure<StockCountResult>($"Stok sayımı uygulanamadı: {ex.Message}");
        }
    }

    private static string? Validate(ApplyStockCountCommand command)
    {
        if (command.IdempotencyKey == Guid.Empty) return "Sayım idempotency anahtarı zorunludur.";
        if (command.WarehouseId <= 0) return "Geçerli bir depo seçilmelidir.";
        if (command.Lines.Count == 0) return "Düzeltilecek sayım farkı bulunamadı.";
        if (command.Lines.Any(item => item.ProductId <= 0 || item.CountedQuantity < 0))
            return "Sayılan miktar negatif olamaz ve ürün kimlikleri geçerli olmalıdır.";
        if (command.Lines.GroupBy(item => item.ProductId).Any(group => group.Count() > 1))
            return "Aynı ürün sayım listesinde birden fazla kez bulunamaz.";
        if (command.Lines.All(item => item.SystemQuantity == item.CountedQuantity))
            return "Düzeltilecek sayım farkı bulunamadı.";
        return null;
    }

    private static StockCountResult ToResult(StockCountSession session, bool wasAlreadyApplied) => new(
        session.Id,
        session.ReferenceNumber,
        session.ProductCount,
        session.TotalPositiveDifference,
        session.TotalNegativeDifference,
        session.FinancialDifference,
        wasAlreadyApplied);

    private static string NormalizeUser(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Sistem" : value.Trim()[..Math.Min(100, value.Trim().Length)];

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
