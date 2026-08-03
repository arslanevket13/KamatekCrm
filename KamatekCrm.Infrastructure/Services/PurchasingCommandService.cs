using System.Data;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Transactions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KamatekCrm.Infrastructure.Services;

public sealed class PurchasingCommandService : IPurchasingCommandService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IApplicationAuthorizationService _authorization;

    public PurchasingCommandService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IApplicationAuthorizationService authorization)
    {
        _dbContextFactory = dbContextFactory;
        _authorization = authorization;
    }

    public async Task<Result<PurchaseCommandResult>> CreatePurchaseAsync(
        CreatePurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        var auth = _authorization.Authorize(ApplicationPermission.ApprovePurchase);
        if (auth.IsFailure) return Result.Failure<PurchaseCommandResult>(auth.Error);
        if (!Guid.TryParse(command.IdempotencyKey, out _)) return Result.Failure<PurchaseCommandResult>("Geçerli işlem anahtarı gereklidir.");
        if (command.SupplierId <= 0 || command.Lines.Count == 0 || command.Lines.Any(item =>
                string.IsNullOrWhiteSpace(item.ProductName) || item.Quantity <= 0 || item.UnitPrice < 0 || item.DiscountAmount < 0 ||
                item.DiscountAmount > item.UnitPrice * item.Quantity || item.TaxRate is < 0 or > 100 ||
                Math.Abs(item.LineTotal - CalculateLineTotal(item.UnitPrice, item.Quantity, item.DiscountAmount, item.TaxRate)) > 0.01m))
            return Result.Failure<PurchaseCommandResult>("Tedarikçi ve satın alma kalemleri geçerli olmalıdır.");
        if (command.ReceiveImmediately && command.WarehouseId is not > 0)
            return Result.Failure<PurchaseCommandResult>("Teslim alma için depo seçilmelidir.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(item => item.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (existing is not null) return Result.Success(new PurchaseCommandResult(existing.Id, existing.TotalAmount, true));
        await using var transaction = await BeginTransactionAsync(context, cancellationToken);
        try
        {
            if (!await context.Suppliers.AnyAsync(item => item.Id == command.SupplierId && item.IsActive, cancellationToken))
                return await FailAsync<PurchaseCommandResult>(transaction, "Aktif tedarikçi bulunamadı.", cancellationToken);
            var productIds = command.Lines.Where(item => item.ProductId is > 0).Select(item => item.ProductId!.Value).Distinct().ToList();
            if (await context.Products.CountAsync(item => productIds.Contains(item.Id), cancellationToken) != productIds.Count)
                return await FailAsync<PurchaseCommandResult>(transaction, "Satın alma kalemlerinden biri sistemde bulunamadı.", cancellationToken);

            var resolvedLines = new List<(PurchaseLineInput Input, int ProductId)>();
            foreach (var line in command.Lines)
            {
                if (line.ProductId is > 0)
                {
                    resolvedLines.Add((line, line.ProductId.Value));
                    continue;
                }

                var product = new Product
                {
                    ProductName = line.ProductName.Trim(),
                    SKU = string.IsNullOrWhiteSpace(line.Sku) ? $"SKU-{Guid.NewGuid():N}"[..16] : line.Sku.Trim(),
                    Barcode = line.Barcode?.Trim() ?? string.Empty,
                    Unit = string.IsNullOrWhiteSpace(line.Unit) ? "Adet" : line.Unit.Trim(),
                    VatRate = (int)line.TaxRate,
                    PurchasePrice = line.UnitPrice,
                    TotalStockQuantity = 0,
                    AverageCost = 0,
                    ProductCategoryType = ProductCategoryType.Other
                };
                context.Products.Add(product);
                await context.SaveChangesAsync(cancellationToken);
                resolvedLines.Add((line, product.Id));
            }

            var order = new PurchaseOrder
            {
                SupplierId = command.SupplierId,
                InvoiceNumber = string.IsNullOrWhiteSpace(command.InvoiceNumber) ? CreateNumber("INV") : command.InvoiceNumber.Trim(),
                OrderDate = EnsureUtc(command.OrderDate),
                Date = DateTime.UtcNow,
                Status = PurchaseStatus.Pending,
                Notes = command.Notes?.Trim() ?? string.Empty,
                IdempotencyKey = command.IdempotencyKey,
                TotalAmount = command.Lines.Sum(item => item.LineTotal)
            };
            foreach (var resolved in resolvedLines)
            {
                var line = resolved.Input;
                order.Items.Add(new PurchaseOrderItem
                {
                    ProductId = resolved.ProductId,
                    ProductName = line.ProductName,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountAmount = line.DiscountAmount,
                    TaxRate = line.TaxRate,
                    SubTotal = line.UnitPrice * line.Quantity,
                    TaxAmount = line.LineTotal - ((line.UnitPrice * line.Quantity) - line.DiscountAmount),
                    LineTotal = line.LineTotal
                });
            }
            context.PurchaseOrders.Add(order);
            await context.SaveChangesAsync(cancellationToken);

            if (command.ReceiveImmediately)
            {
                var settlements = command.Settlements ?? Array.Empty<PaymentAllocationInput>();
                var receiptError = await ApplyReceiptAsync(context, order, command.WarehouseId!.Value, settlements, command.CreatedBy, command.IdempotencyKey, cancellationToken);
                if (receiptError is not null) return await FailAsync<PurchaseCommandResult>(transaction, receiptError, cancellationToken);
            }

            AddAudit(context, "Create", "PurchaseOrder", order.Id.ToString(), command.ReceiveImmediately ? "Satın alma oluşturuldu ve teslim alındı." : "Satın alma oluşturuldu.", command.CreatedBy);
            await context.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            return Result.Success(new PurchaseCommandResult(order.Id, order.TotalAmount, false));
        }
        catch (Exception exception)
        {
            await RollbackAsync(transaction, cancellationToken);
            return Result.Failure<PurchaseCommandResult>($"Satın alma oluşturulamadı: {exception.Message}");
        }
    }

    public async Task<Result<PurchaseCommandResult>> ReceivePurchaseAsync(
        ReceivePurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        var auth = _authorization.Authorize(ApplicationPermission.ApprovePurchase);
        if (auth.IsFailure) return Result.Failure<PurchaseCommandResult>(auth.Error);
        if (!Guid.TryParse(command.IdempotencyKey, out _)) return Result.Failure<PurchaseCommandResult>("Geçerli teslim işlem anahtarı gereklidir.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(item => item.ReceiptIdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (existing is not null) return Result.Success(new PurchaseCommandResult(existing.Id, existing.TotalAmount, true));
        await using var transaction = await BeginTransactionAsync(context, cancellationToken);
        try
        {
            var order = await context.PurchaseOrders.Include(item => item.Items).Include(item => item.Payments)
                .SingleOrDefaultAsync(item => item.Id == command.PurchaseOrderId, cancellationToken);
            if (order is null) return await FailAsync<PurchaseCommandResult>(transaction, "Satın alma bulunamadı.", cancellationToken);
            var error = await ApplyReceiptAsync(context, order, command.WarehouseId, command.Settlements, command.CreatedBy, command.IdempotencyKey, cancellationToken);
            if (error is not null) return await FailAsync<PurchaseCommandResult>(transaction, error, cancellationToken);
            AddAudit(context, "Update", "PurchaseOrder", order.Id.ToString(), "Satın alma teslim alındı.", command.CreatedBy);
            await context.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            return Result.Success(new PurchaseCommandResult(order.Id, order.TotalAmount, false));
        }
        catch (Exception exception)
        {
            await RollbackAsync(transaction, cancellationToken);
            return Result.Failure<PurchaseCommandResult>($"Teslim alma tamamlanamadı: {exception.Message}");
        }
    }

    public async Task<Result> CancelPurchaseAsync(CancelPurchaseCommand command, CancellationToken cancellationToken = default)
    {
        var auth = _authorization.Authorize(ApplicationPermission.ProcessReturns);
        if (auth.IsFailure) return Result.Failure(auth.Error);
        if (string.IsNullOrWhiteSpace(command.Reason)) return Result.Failure("İptal nedeni zorunludur.");
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var order = await context.PurchaseOrders.FindAsync([command.PurchaseOrderId], cancellationToken);
        if (order is null) return Result.Failure("Satın alma bulunamadı.");
        if (order.Status is not (PurchaseStatus.Pending or PurchaseStatus.Ordered))
            return Result.Failure("Yalnız bekleyen veya sipariş edilmiş kayıt iptal edilebilir; teslim alınan kayıt için iade oluşturun.");
        order.Status = PurchaseStatus.Cancelled;
        order.Notes = string.Join(Environment.NewLine, new[] { order.Notes, $"İptal: {command.Reason.Trim()} ({command.CancelledBy})" }.Where(item => !string.IsNullOrWhiteSpace(item)));
        AddAudit(context, "Update", "PurchaseOrder", order.Id.ToString(), $"Satın alma iptal edildi: {command.Reason.Trim()}", command.CancelledBy);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ReturnablePurchaseDto>> GetReturnablePurchaseAsync(int purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var auth = _authorization.Authorize(ApplicationPermission.ProcessReturns);
        if (auth.IsFailure) return Result.Failure<ReturnablePurchaseDto>(auth.Error);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var order = await context.PurchaseOrders.AsNoTracking().Include(item => item.Items).Include(item => item.Payments)
            .SingleOrDefaultAsync(item => item.Id == purchaseOrderId, cancellationToken);
        if (order is null) return Result.Failure<ReturnablePurchaseDto>("Satın alma bulunamadı.");
        if (order.Status is not (PurchaseStatus.Received or PurchaseStatus.Completed or PurchaseStatus.PartiallyReturned)) return Result.Failure<ReturnablePurchaseDto>("Satın alma iade edilebilir durumda değil.");
        var returned = await context.PurchaseReturnItems.AsNoTracking().Where(item => item.PurchaseReturn.PurchaseOrderId == purchaseOrderId).ToListAsync(cancellationToken);
        var returns = await context.PurchaseReturns.AsNoTracking().Where(item => item.PurchaseOrderId == purchaseOrderId).ToListAsync(cancellationToken);
        var lines = order.Items.Where(item => item.ProductId.HasValue).Select(item =>
        {
            var returnedQty = returned.Where(value => value.PurchaseOrderItemId == item.Id).Sum(value => value.Quantity);
            var returnedAmount = returned.Where(value => value.PurchaseOrderItemId == item.Id).Sum(value => value.LineTotal);
            return new ReturnablePurchaseLineDto(item.Id, item.ProductId!.Value, item.ProductName, item.Quantity, returnedQty, item.Quantity - returnedQty, item.LineTotal - returnedAmount);
        }).ToList();
        var externalOriginal = order.Payments.Where(item => item.PaymentMethod != PaymentMethod.OnAccount).Sum(item => item.Amount);
        var externalReturned = returns.Where(item => item.SettlementMethod != PaymentMethod.OnAccount).Sum(item => item.TotalAmount);
        return Result.Success(new ReturnablePurchaseDto(order.Id, order.InvoiceNumber, order.WarehouseId, Math.Max(0, externalOriginal - externalReturned), order.Payments.Count == 0, lines));
    }

    public async Task<Result<ReturnTransactionResult>> ReturnPurchaseAsync(ReturnPurchaseCommand command, CancellationToken cancellationToken = default)
    {
        var auth = _authorization.Authorize(ApplicationPermission.ProcessReturns);
        if (auth.IsFailure) return Result.Failure<ReturnTransactionResult>(auth.Error);
        if (!Guid.TryParse(command.IdempotencyKey, out _)) return Result.Failure<ReturnTransactionResult>("Geçerli iade işlem anahtarı gereklidir.");
        if (string.IsNullOrWhiteSpace(command.Reason) || command.Lines.Count == 0 || command.Lines.Any(item => item.Quantity <= 0) ||
            command.Lines.GroupBy(item => item.PurchaseOrderItemId).Any(group => group.Count() > 1))
            return Result.Failure<ReturnTransactionResult>("İade nedeni ve geçerli kalemler zorunludur.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.PurchaseReturns.AsNoTracking().SingleOrDefaultAsync(item => item.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (existing is not null) return Result.Success(new ReturnTransactionResult(existing.Id, existing.ReturnNumber, existing.TotalAmount, true));
        await using var transaction = await BeginTransactionAsync(context, cancellationToken);
        try
        {
            var order = await context.PurchaseOrders.Include(item => item.Items).Include(item => item.Payments).Include(item => item.Supplier)
                .SingleOrDefaultAsync(item => item.Id == command.PurchaseOrderId, cancellationToken);
            if (order is null) return await FailAsync<ReturnTransactionResult>(transaction, "Satın alma bulunamadı.", cancellationToken);
            if (order.Status is not (PurchaseStatus.Received or PurchaseStatus.Completed or PurchaseStatus.PartiallyReturned))
                return await FailAsync<ReturnTransactionResult>(transaction, "Satın alma iade edilebilir durumda değil.", cancellationToken);
            if (order.Payments.Count == 0 && !command.LegacySettlementOverride)
                return await FailAsync<ReturnTransactionResult>(transaction, "Eski satın alma için ödeme yöntemi yönetici onayıyla belirtilmelidir.", cancellationToken);

            var priorItems = await context.PurchaseReturnItems.Where(item => item.PurchaseReturn.PurchaseOrderId == order.Id).ToListAsync(cancellationToken);
            var priorReturns = await context.PurchaseReturns.Where(item => item.PurchaseOrderId == order.Id).ToListAsync(cancellationToken);
            var originals = order.Items.ToDictionary(item => item.Id);
            var purchaseReturn = new PurchaseReturn
            {
                PurchaseOrderId = order.Id,
                ReturnNumber = CreateNumber("PRET"),
                IdempotencyKey = command.IdempotencyKey,
                Date = DateTime.UtcNow,
                Reason = command.Reason.Trim(),
                Notes = command.Notes?.Trim(),
                SettlementMethod = command.SettlementMethod,
                SettlementReference = command.SettlementReference?.Trim() ?? string.Empty,
                LegacySettlementOverride = command.LegacySettlementOverride,
                CreatedBy = command.CreatedBy
            };

            foreach (var input in command.Lines)
            {
                if (!originals.TryGetValue(input.PurchaseOrderItemId, out var original) || original.ProductId is null)
                    return await FailAsync<ReturnTransactionResult>(transaction, "Satın alma kalemi bulunamadı.", cancellationToken);
                var previous = priorItems.Where(item => item.PurchaseOrderItemId == original.Id).ToList();
                var returnedQuantity = previous.Sum(item => item.Quantity);
                var remaining = original.Quantity - returnedQuantity;
                if (input.Quantity > remaining) return await FailAsync<ReturnTransactionResult>(transaction, $"'{original.ProductName}' için kalan iade miktarı {remaining}.", cancellationToken);
                var inventory = await context.Inventories.SingleOrDefaultAsync(item => item.ProductId == original.ProductId && item.WarehouseId == input.SourceWarehouseId, cancellationToken);
                if (inventory is null || inventory.Quantity < input.Quantity)
                    return await FailAsync<ReturnTransactionResult>(transaction, $"'{original.ProductName}' için kaynak depoda yeterli stok yok.", cancellationToken);
                var isFinal = input.Quantity == remaining;
                var amount = isFinal ? original.LineTotal - previous.Sum(item => item.LineTotal) : Math.Round(original.LineTotal * input.Quantity / original.Quantity, 2, MidpointRounding.AwayFromZero);
                inventory.Quantity -= input.Quantity;
                if (inventory.Quantity == 0) inventory.AverageCost = 0;
                else inventory.AverageCost = Math.Max(0, ((inventory.Quantity + input.Quantity) * inventory.AverageCost - input.Quantity * original.UnitPrice) / inventory.Quantity);
                var product = await context.Products.FindAsync([original.ProductId.Value], cancellationToken);
                if (product is not null)
                {
                    var oldTotalQuantity = product.TotalStockQuantity;
                    var newTotalQuantity = Math.Max(0, oldTotalQuantity - input.Quantity);
                    product.AverageCost = newTotalQuantity == 0
                        ? 0
                        : Math.Max(0, ((oldTotalQuantity * product.AverageCost) - (input.Quantity * original.UnitPrice)) / newTotalQuantity);
                    product.TotalStockQuantity = newTotalQuantity;
                }
                purchaseReturn.Items.Add(new PurchaseReturnItem
                {
                    PurchaseOrderItemId = original.Id,
                    ProductId = original.ProductId.Value,
                    SourceWarehouseId = input.SourceWarehouseId,
                    Quantity = input.Quantity,
                    UnitCost = original.UnitPrice,
                    LineTotal = amount
                });
                purchaseReturn.TotalAmount += amount;
            }

            var knownExternal = order.Payments.Where(item => item.PaymentMethod != PaymentMethod.OnAccount).Sum(item => item.Amount);
            var returnedExternal = priorReturns.Where(item => item.SettlementMethod != PaymentMethod.OnAccount).Sum(item => item.TotalAmount);
            if (command.SettlementMethod != PaymentMethod.OnAccount && order.Payments.Count > 0 && purchaseReturn.TotalAmount > knownExternal - returnedExternal)
                return await FailAsync<ReturnTransactionResult>(transaction, "Dış ödeme iadesi kalan ödenmiş tutarı aşamaz; kalan kısmı cari mahsup edin.", cancellationToken);

            context.PurchaseReturns.Add(purchaseReturn);
            await context.SaveChangesAsync(cancellationToken);
            foreach (var item in purchaseReturn.Items)
            {
                context.StockTransactions.Add(new StockTransaction
                {
                    Date = DateTime.UtcNow,
                    ProductId = item.ProductId,
                    SourceWarehouseId = item.SourceWarehouseId,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost,
                    TransactionType = StockTransactionType.ReturnToSupplier,
                    Description = $"Tedarikçi İadesi {purchaseReturn.ReturnNumber}",
                    ReferenceId = purchaseReturn.ReturnNumber,
                    PurchaseOrderId = order.Id,
                    PurchaseReturnId = purchaseReturn.Id,
                    UserId = command.CreatedBy
                });
            }

            if (command.SettlementMethod == PaymentMethod.OnAccount)
            {
                order.Supplier.Balance -= purchaseReturn.TotalAmount;
            }
            else
            {
                context.CashTransactions.Add(new CashTransaction
                {
                    TransactionType = FinancialTransactionPolicy.ToIncomeType(command.SettlementMethod),
                    PaymentMethod = command.SettlementMethod,
                    Amount = purchaseReturn.TotalAmount,
                    Date = DateTime.UtcNow,
                    Description = $"Tedarikçi İadesi {purchaseReturn.ReturnNumber}",
                    Category = "Satın Alma İadesi",
                    ReferenceNumber = purchaseReturn.SettlementReference,
                    PurchaseReturnId = purchaseReturn.Id,
                    CreatedBy = command.CreatedBy,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var allReturned = priorItems.Sum(item => item.Quantity) + purchaseReturn.Items.Sum(item => item.Quantity) == order.Items.Sum(item => item.Quantity);
            order.Status = allReturned ? PurchaseStatus.Returned : PurchaseStatus.PartiallyReturned;
            AddAudit(context, "Create", "PurchaseReturn", purchaseReturn.Id.ToString(), $"Tedarikçi iadesi: {purchaseReturn.ReturnNumber}, {purchaseReturn.TotalAmount:N2} ₺", command.CreatedBy);
            await context.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            return Result.Success(new ReturnTransactionResult(purchaseReturn.Id, purchaseReturn.ReturnNumber, purchaseReturn.TotalAmount, false));
        }
        catch (Exception exception)
        {
            await RollbackAsync(transaction, cancellationToken);
            return Result.Failure<ReturnTransactionResult>($"Tedarikçi iadesi tamamlanamadı: {exception.Message}");
        }
    }

    private static async Task<string?> ApplyReceiptAsync(
        AppDbContext context,
        PurchaseOrder order,
        int warehouseId,
        IReadOnlyCollection<PaymentAllocationInput> settlements,
        string createdBy,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (order.Status is PurchaseStatus.Received or PurchaseStatus.Completed or PurchaseStatus.PartiallyReturned or PurchaseStatus.Returned) return "Bu satın alma daha önce teslim alınmış.";
        if (order.Status == PurchaseStatus.Cancelled) return "İptal edilmiş satın alma teslim alınamaz.";
        if (order.Items.Count == 0) return "Satın alma kalemi bulunamadı.";
        if (settlements.Count == 0 || settlements.Any(item => item.Amount <= 0) || settlements.Sum(item => item.Amount) != order.TotalAmount)
            return "Ödeme dağılımı satın alma toplamına eşit olmalıdır.";
        var warehouse = await context.Warehouses.SingleOrDefaultAsync(item => item.Id == warehouseId && item.IsActive && !item.IsQuarantine, cancellationToken);
        if (warehouse is null) return "Geçerli teslim deposu bulunamadı.";

        var supplier = await context.Suppliers.FindAsync([order.SupplierId], cancellationToken);
        if (supplier is null) return "Tedarikçi bulunamadı.";
        foreach (var item in order.Items)
        {
            if (item.ProductId is null) return $"'{item.ProductName}' için ürün bağlantısı eksik.";
            var inventory = await context.Inventories.SingleOrDefaultAsync(value => value.ProductId == item.ProductId && value.WarehouseId == warehouseId, cancellationToken);
            if (inventory is null)
            {
                inventory = new Inventory { ProductId = item.ProductId, WarehouseId = warehouseId, Quantity = 0, AverageCost = 0 };
                context.Inventories.Add(inventory);
            }
            var oldQuantity = inventory.Quantity;
            inventory.Quantity += item.Quantity;
            inventory.AverageCost = inventory.Quantity == 0 ? 0 : ((oldQuantity * inventory.AverageCost) + (item.Quantity * item.UnitPrice)) / inventory.Quantity;
            var product = await context.Products.FindAsync([item.ProductId.Value], cancellationToken);
            if (product is null) return $"Ürün bulunamadı: #{item.ProductId}.";
            var oldTotalQuantity = product.TotalStockQuantity;
            product.TotalStockQuantity += item.Quantity;
            product.AverageCost = product.TotalStockQuantity == 0
                ? 0
                : ((oldTotalQuantity * product.AverageCost) + (item.Quantity * item.UnitPrice)) / product.TotalStockQuantity;
            product.PurchasePrice = item.UnitPrice;
            context.StockTransactions.Add(new StockTransaction
            {
                Date = DateTime.UtcNow,
                ProductId = item.ProductId,
                TargetWarehouseId = warehouseId,
                Quantity = item.Quantity,
                UnitCost = item.UnitPrice,
                TransactionType = StockTransactionType.Purchase,
                Description = $"Satın Alma {order.InvoiceNumber}",
                ReferenceId = order.InvoiceNumber,
                PurchaseOrderId = order.Id,
                UserId = createdBy
            });
        }

        foreach (var settlement in settlements)
        {
            order.Payments.Add(new PurchaseOrderPayment { PaymentMethod = settlement.PaymentMethod, Amount = settlement.Amount, Reference = settlement.Reference?.Trim() ?? string.Empty });
            if (settlement.PaymentMethod == PaymentMethod.OnAccount) supplier.Balance += settlement.Amount;
            else
            {
                context.CashTransactions.Add(new CashTransaction
                {
                    TransactionType = FinancialTransactionPolicy.ToExpenseType(settlement.PaymentMethod),
                    PaymentMethod = settlement.PaymentMethod,
                    Amount = settlement.Amount,
                    Date = DateTime.UtcNow,
                    Description = $"Satın Alma {order.InvoiceNumber}",
                    Category = "Satın Alma",
                    ReferenceNumber = settlement.Reference?.Trim() ?? string.Empty,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        order.WarehouseId = warehouseId;
        order.Status = PurchaseStatus.Received;
        order.ReceiptIdempotencyKey = idempotencyKey;
        return null;
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static decimal CalculateLineTotal(decimal unitPrice, int quantity, decimal discount, decimal taxRate)
    {
        var afterDiscount = unitPrice * quantity - discount;
        return Math.Round(afterDiscount + (afterDiscount * taxRate / 100m), 2, MidpointRounding.AwayFromZero);
    }

    private static string CreateNumber(string prefix) => $"{prefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..48];
    private static void AddAudit(AppDbContext context, string action, string entity, string recordId, string description, string username) =>
        context.ActivityLogs.Add(new ActivityLog { Action = action, ActionType = action, EntityName = entity, RecordId = recordId, ReferenceId = recordId, Description = description, Username = username, Timestamp = DateTime.UtcNow, UserAgent = "WPF Client" });
    private static async Task<IDbContextTransaction?> BeginTransactionAsync(AppDbContext context, CancellationToken cancellationToken) =>
        context.Database.IsRelational()
            ? await context.Database.CreateExecutionStrategy().ExecuteAsync(async () => await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
            : null;
    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) => transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;
    private static Task RollbackAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) => transaction?.RollbackAsync(cancellationToken) ?? Task.CompletedTask;
    private static async Task<Result<T>> FailAsync<T>(IDbContextTransaction? transaction, string error, CancellationToken cancellationToken)
    {
        await RollbackAsync(transaction, cancellationToken);
        return Result.Failure<T>(error);
    }
}
