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

public sealed class RetailTransactionService : IRetailTransactionService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IApplicationAuthorizationService _authorization;

    public RetailTransactionService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IApplicationAuthorizationService authorization)
    {
        _dbContextFactory = dbContextFactory;
        _authorization = authorization;
    }

    public async Task<Result<SaleTransactionResult>> CompleteSaleAsync(
        CompleteSaleCommand command,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ExecuteSales);
        if (authorization.IsFailure) return Result.Failure<SaleTransactionResult>(authorization.Error);

        var validation = ValidateSale(command);
        if (validation is not null) return Result.Failure<SaleTransactionResult>(validation);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.SalesOrders.AsNoTracking()
            .SingleOrDefaultAsync(order => order.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (existing is not null)
            return Result.Success(new SaleTransactionResult(existing.Id, existing.OrderNumber, true));

        await using var transaction = await BeginTransactionAsync(context, cancellationToken);
        try
        {
            var warehouse = await context.Warehouses
                .SingleOrDefaultAsync(item => item.Id == command.WarehouseId && item.IsActive, cancellationToken);
            if (warehouse is null || warehouse.IsQuarantine)
                return await FailAsync<SaleTransactionResult>(transaction, "Satış için aktif ve satılabilir bir depo seçilmelidir.", cancellationToken);

            var grouped = command.Items.GroupBy(item => item.ProductId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
            var productIds = grouped.Keys.ToList();
            var inventories = await context.Inventories
                .Where(item => item.WarehouseId == command.WarehouseId && item.ProductId.HasValue && productIds.Contains(item.ProductId.Value))
                .ToDictionaryAsync(item => item.ProductId!.Value, cancellationToken);
            var products = await context.Products.Where(item => productIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

            foreach (var requested in grouped)
            {
                if (!inventories.TryGetValue(requested.Key, out var inventory) || inventory.Quantity < requested.Value)
                    return await FailAsync<SaleTransactionResult>(transaction, $"Ürün #{requested.Key} için yetersiz stok.", cancellationToken);
                if (!products.ContainsKey(requested.Key))
                    return await FailAsync<SaleTransactionResult>(transaction, $"Ürün bulunamadı: #{requested.Key}.", cancellationToken);
            }

            var total = command.Items.Sum(item => item.LineTotal);
            var order = new SalesOrder
            {
                CustomerId = command.CustomerId,
                WarehouseId = command.WarehouseId,
                OrderNumber = CreateNumber("ORD"),
                IdempotencyKey = command.IdempotencyKey,
                Date = DateTime.UtcNow,
                CustomerName = string.IsNullOrWhiteSpace(command.CustomerName) ? "Perakende Müşteri" : command.CustomerName.Trim(),
                PaymentMethod = string.Join(", ", command.Payments.Select(item => $"{item.PaymentMethod}: {item.Amount:N2} ₺")),
                SubTotal = command.Items.Sum(item => item.UnitPrice * item.Quantity),
                DiscountTotal = command.Items.Sum(item => item.DiscountAmount),
                TaxTotal = command.Items.Sum(item => item.LineTotal - ((item.UnitPrice * item.Quantity) - item.DiscountAmount)),
                TotalAmount = total,
                Notes = command.Notes?.Trim() ?? "POS Perakende Satış",
                Status = SalesOrderStatus.Completed
            };
            foreach (var line in command.Items)
            {
                order.Items.Add(new SalesOrderItem
                {
                    ProductId = line.ProductId,
                    ProductName = line.ProductName,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountAmount = line.DiscountAmount,
                    DiscountPercent = line.UnitPrice * line.Quantity == 0 ? 0 : line.DiscountAmount / (line.UnitPrice * line.Quantity) * 100m,
                    TaxRate = line.TaxRate,
                    LineTotal = line.LineTotal
                });
            }
            foreach (var payment in command.Payments)
            {
                order.Payments.Add(new SalesOrderPayment
                {
                    PaymentMethod = payment.PaymentMethod,
                    Amount = payment.Amount,
                    Reference = payment.Reference?.Trim() ?? string.Empty
                });
            }

            context.SalesOrders.Add(order);
            await context.SaveChangesAsync(cancellationToken);

            foreach (var requested in grouped)
            {
                inventories[requested.Key].Quantity -= requested.Value;
                products[requested.Key].TotalStockQuantity = Math.Max(0, products[requested.Key].TotalStockQuantity - requested.Value);
                context.StockTransactions.Add(new StockTransaction
                {
                    Date = DateTime.UtcNow,
                    ProductId = requested.Key,
                    SourceWarehouseId = command.WarehouseId,
                    Quantity = requested.Value,
                    TransactionType = StockTransactionType.Sale,
                    Description = $"POS Satış {order.OrderNumber}",
                    ReferenceId = order.OrderNumber,
                    SalesOrderId = order.Id,
                    UserId = command.CreatedBy
                });
            }

            var externalPaid = 0m;
            foreach (var payment in command.Payments.Where(item => item.PaymentMethod != PaymentMethod.OnAccount))
            {
                externalPaid += payment.Amount;
                context.CashTransactions.Add(new CashTransaction
                {
                    TransactionType = FinancialTransactionPolicy.ToIncomeType(payment.PaymentMethod),
                    PaymentMethod = payment.PaymentMethod,
                    Amount = payment.Amount,
                    Date = DateTime.UtcNow,
                    Description = $"POS Satış {order.OrderNumber}",
                    Category = "Perakende Satış",
                    ReferenceNumber = payment.Reference?.Trim() ?? string.Empty,
                    SalesOrderId = order.Id,
                    CustomerId = command.CustomerId,
                    CreatedBy = command.CreatedBy,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (command.CustomerId is > 0)
            {
                var customer = await context.Customers.FindAsync([command.CustomerId.Value], cancellationToken);
                if (customer is not null)
                {
                    customer.TotalSpent += total;
                    customer.TotalPurchaseCount += 1;
                    customer.LastPurchaseDate = DateTime.UtcNow;
                    customer.LoyaltyPoints += (int)(total / 100m);
                    context.Transactions.Add(new Transaction
                    {
                        CustomerId = customer.Id,
                        SalesOrderId = order.Id,
                        Amount = total,
                        Date = DateTime.UtcNow,
                        Type = TransactionType.Debt,
                        Description = $"POS Satış {order.OrderNumber}"
                    });
                    if (externalPaid > 0)
                    {
                        context.Transactions.Add(new Transaction
                        {
                            CustomerId = customer.Id,
                            SalesOrderId = order.Id,
                            Amount = externalPaid,
                            Date = DateTime.UtcNow,
                            Type = TransactionType.Payment,
                            Description = $"POS Satış Tahsilatı {order.OrderNumber}"
                        });
                    }
                }
            }

            AddAudit(context, "Create", "SalesOrder", order.Id.ToString(), $"Satış tamamlandı: {order.OrderNumber}, {total:N2} ₺", command.CreatedBy);
            await context.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            return Result.Success(new SaleTransactionResult(order.Id, order.OrderNumber, false));
        }
        catch (Exception exception)
        {
            await RollbackAsync(transaction, cancellationToken);
            return Result.Failure<SaleTransactionResult>($"Satış tamamlanamadı: {exception.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<SaleSummaryDto>>> SearchSalesAsync(
        SaleSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ProcessReturns);
        if (authorization.IsFailure) return Result.Failure<IReadOnlyList<SaleSummaryDto>>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var source = context.SalesOrders.AsNoTracking()
            .Where(item => item.Status == SalesOrderStatus.Completed || item.Status == SalesOrderStatus.PartiallyRefunded || item.Status == SalesOrderStatus.Refunded)
            .AsQueryable();
        if (query.StartDate.HasValue) source = source.Where(item => item.Date >= query.StartDate.Value);
        if (query.EndDate.HasValue) source = source.Where(item => item.Date <= query.EndDate.Value);
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var search = query.SearchText.Trim().ToLower();
            source = source.Where(item => item.OrderNumber.ToLower().Contains(search) || item.CustomerName.ToLower().Contains(search));
        }

        var rows = await source.OrderByDescending(item => item.Date).Take(Math.Clamp(query.Take, 1, 250))
            .Select(item => new SaleSummaryDto(item.Id, item.OrderNumber, item.Date, item.CustomerName, item.TotalAmount, item.Status))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<SaleSummaryDto>>(rows);
    }

    public async Task<Result<ReturnableSaleDto>> GetReturnableSaleAsync(int salesOrderId, CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ProcessReturns);
        if (authorization.IsFailure) return Result.Failure<ReturnableSaleDto>(authorization.Error);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await BuildReturnableSaleAsync(context, salesOrderId, cancellationToken);
    }

    public async Task<Result<ReturnTransactionResult>> ReturnSaleAsync(
        ReturnSaleCommand command,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ProcessReturns);
        if (authorization.IsFailure) return Result.Failure<ReturnTransactionResult>(authorization.Error);
        if (!Guid.TryParse(command.IdempotencyKey, out _)) return Result.Failure<ReturnTransactionResult>("Geçerli bir iade işlem anahtarı gereklidir.");
        if (string.IsNullOrWhiteSpace(command.Reason)) return Result.Failure<ReturnTransactionResult>("İade nedeni zorunludur.");
        if (command.Lines.Count == 0 || command.Lines.Any(item => item.Quantity <= 0)) return Result.Failure<ReturnTransactionResult>("En az bir geçerli iade kalemi seçilmelidir.");
        if (command.Lines.GroupBy(item => item.SalesOrderItemId).Any(group => group.Count() > 1)) return Result.Failure<ReturnTransactionResult>("Aynı satış kalemi birden fazla kez gönderilemez.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.SalesReturns.AsNoTracking().SingleOrDefaultAsync(item => item.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (existing is not null) return Result.Success(new ReturnTransactionResult(existing.Id, existing.ReturnNumber, existing.TotalAmount, true));
        await using var transaction = await BeginTransactionAsync(context, cancellationToken);

        try
        {
            var order = await context.SalesOrders.Include(item => item.Items).Include(item => item.Payments)
                .SingleOrDefaultAsync(item => item.Id == command.SalesOrderId, cancellationToken);
            if (order is null) return await FailAsync<ReturnTransactionResult>(transaction, "Satış bulunamadı.", cancellationToken);
            if (order.Status is not (SalesOrderStatus.Completed or SalesOrderStatus.PartiallyRefunded))
                return await FailAsync<ReturnTransactionResult>(transaction, "Bu satış iade edilebilir durumda değil.", cancellationToken);

            var priorItems = await context.SalesReturnItems
                .Where(item => item.SalesReturn.SalesOrderId == order.Id)
                .ToListAsync(cancellationToken);
            var priorPayments = await context.SalesReturnPayments
                .Where(item => item.SalesReturn.SalesOrderId == order.Id)
                .ToListAsync(cancellationToken);
            var orderItems = order.Items.ToDictionary(item => item.Id);
            var salesReturn = new SalesReturn
            {
                SalesOrderId = order.Id,
                ReturnNumber = CreateNumber("SRET"),
                IdempotencyKey = command.IdempotencyKey,
                Date = DateTime.UtcNow,
                Reason = command.Reason.Trim(),
                Notes = command.Notes?.Trim(),
                CreatedBy = command.CreatedBy
            };

            Warehouse? quarantine = null;
            foreach (var input in command.Lines)
            {
                if (!orderItems.TryGetValue(input.SalesOrderItemId, out var original))
                    return await FailAsync<ReturnTransactionResult>(transaction, $"Satış kalemi bulunamadı: #{input.SalesOrderItemId}.", cancellationToken);
                var returnedQuantity = priorItems.Where(item => item.SalesOrderItemId == original.Id).Sum(item => item.Quantity);
                var remainingQuantity = original.Quantity - returnedQuantity;
                if (input.Quantity > remainingQuantity)
                    return await FailAsync<ReturnTransactionResult>(transaction, $"'{original.ProductName}' için kalan iade miktarı {remainingQuantity}.", cancellationToken);

                var previous = priorItems.Where(item => item.SalesOrderItemId == original.Id).ToList();
                var isFinal = input.Quantity == remainingQuantity;
                var originalSubTotal = original.UnitPrice * original.Quantity;
                var originalTax = original.LineTotal - (originalSubTotal - original.DiscountAmount);
                var subTotal = Allocate(originalSubTotal, original.Quantity, input.Quantity, previous.Sum(item => item.SubTotal), isFinal);
                var discount = Allocate(original.DiscountAmount, original.Quantity, input.Quantity, previous.Sum(item => item.DiscountAmount), isFinal);
                var tax = Allocate(originalTax, original.Quantity, input.Quantity, previous.Sum(item => item.TaxAmount), isFinal);
                var lineTotal = subTotal - discount + tax;

                var destinationId = input.RestockWarehouseId;
                if (input.Disposition == ReturnDisposition.Quarantine)
                {
                    quarantine ??= await GetOrCreateQuarantineAsync(context, cancellationToken);
                    destinationId = quarantine.Id;
                }
                var destination = await context.Warehouses.SingleOrDefaultAsync(item => item.Id == destinationId && item.IsActive, cancellationToken);
                if (destination is null) return await FailAsync<ReturnTransactionResult>(transaction, "İade hedef deposu geçersiz.", cancellationToken);
                if (input.Disposition == ReturnDisposition.Restock && destination.IsQuarantine)
                    return await FailAsync<ReturnTransactionResult>(transaction, "Satılabilir iade karantina deposuna yönlendirilemez.", cancellationToken);

                var inventory = await context.Inventories.SingleOrDefaultAsync(item => item.ProductId == original.ProductId && item.WarehouseId == destinationId, cancellationToken);
                if (inventory is null)
                {
                    inventory = new Inventory { ProductId = original.ProductId, WarehouseId = destinationId, Quantity = 0 };
                    context.Inventories.Add(inventory);
                }
                inventory.Quantity += input.Quantity;
                var product = await context.Products.FindAsync([original.ProductId], cancellationToken);
                if (product is not null) product.TotalStockQuantity += input.Quantity;

                var returnItem = new SalesReturnItem
                {
                    SalesOrderItemId = original.Id,
                    ProductId = original.ProductId,
                    DestinationWarehouseId = destinationId,
                    Quantity = input.Quantity,
                    Disposition = input.Disposition,
                    SubTotal = subTotal,
                    DiscountAmount = discount,
                    TaxAmount = tax,
                    LineTotal = lineTotal
                };
                salesReturn.Items.Add(returnItem);
                salesReturn.SubTotal += subTotal;
                salesReturn.DiscountTotal += discount;
                salesReturn.TaxTotal += tax;
                salesReturn.TotalAmount += lineTotal;
            }

            if (command.Refunds.Count == 0 || command.Refunds.Any(item => item.Amount <= 0) ||
                command.Refunds.Sum(item => item.Amount) != salesReturn.TotalAmount)
                return await FailAsync<ReturnTransactionResult>(transaction, "Para iadesi dağılımı iade toplamına eşit olmalıdır.", cancellationToken);

            var originalExternal = order.Payments.Where(item => item.PaymentMethod != PaymentMethod.OnAccount).Sum(item => item.Amount);
            var priorExternal = priorPayments.Where(item => item.PaymentMethod != PaymentMethod.OnAccount).Sum(item => item.Amount);
            var requestedExternal = command.Refunds.Where(item => item.PaymentMethod != PaymentMethod.OnAccount).Sum(item => item.Amount);
            if (requestedExternal > originalExternal - priorExternal)
                return await FailAsync<ReturnTransactionResult>(transaction, "Gerçek para iadesi kalan dış tahsilat tutarını aşamaz.", cancellationToken);

            context.SalesReturns.Add(salesReturn);
            await context.SaveChangesAsync(cancellationToken);

            foreach (var payment in command.Refunds)
            {
                salesReturn.Payments.Add(new SalesReturnPayment
                {
                    PaymentMethod = payment.PaymentMethod,
                    Amount = payment.Amount,
                    Reference = payment.Reference?.Trim() ?? string.Empty
                });
                if (payment.PaymentMethod != PaymentMethod.OnAccount)
                {
                    context.CashTransactions.Add(new CashTransaction
                    {
                        TransactionType = FinancialTransactionPolicy.ToExpenseType(payment.PaymentMethod),
                        PaymentMethod = payment.PaymentMethod,
                        Amount = payment.Amount,
                        Date = DateTime.UtcNow,
                        Description = $"Satış İadesi {salesReturn.ReturnNumber}",
                        Category = "Satış İadesi",
                        ReferenceNumber = payment.Reference?.Trim() ?? string.Empty,
                        SalesOrderId = order.Id,
                        SalesReturnId = salesReturn.Id,
                        CustomerId = order.CustomerId,
                        CreatedBy = command.CreatedBy,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            foreach (var item in salesReturn.Items)
            {
                context.StockTransactions.Add(new StockTransaction
                {
                    Date = DateTime.UtcNow,
                    ProductId = item.ProductId,
                    TargetWarehouseId = item.DestinationWarehouseId,
                    Quantity = item.Quantity,
                    TransactionType = StockTransactionType.ReturnFromCustomer,
                    Description = $"Satış İadesi {salesReturn.ReturnNumber} ({item.Disposition})",
                    ReferenceId = salesReturn.ReturnNumber,
                    SalesOrderId = order.Id,
                    SalesReturnId = salesReturn.Id,
                    UserId = command.CreatedBy
                });
            }

            if (order.CustomerId is > 0)
            {
                var customer = await context.Customers.FindAsync([order.CustomerId.Value], cancellationToken);
                if (customer is not null)
                {
                    customer.TotalSpent = Math.Max(0, customer.TotalSpent - salesReturn.TotalAmount);
                    var previousReturnedTotal = await context.SalesReturns.Where(item => item.SalesOrderId == order.Id && item.Id != salesReturn.Id).SumAsync(item => item.TotalAmount, cancellationToken);
                    var beforePoints = Math.Min((int)(order.TotalAmount / 100m), (int)(previousReturnedTotal / 100m));
                    var afterPoints = Math.Min((int)(order.TotalAmount / 100m), (int)((previousReturnedTotal + salesReturn.TotalAmount) / 100m));
                    customer.LoyaltyPoints = Math.Max(0, customer.LoyaltyPoints - (afterPoints - beforePoints));
                    context.Transactions.Add(new Transaction
                    {
                        CustomerId = customer.Id,
                        SalesOrderId = order.Id,
                        SalesReturnId = salesReturn.Id,
                        Amount = salesReturn.TotalAmount,
                        Date = DateTime.UtcNow,
                        Type = TransactionType.CreditNote,
                        Description = $"Satış İade Alacak Dekontu {salesReturn.ReturnNumber}"
                    });
                    if (requestedExternal > 0)
                    {
                        context.Transactions.Add(new Transaction
                        {
                            CustomerId = customer.Id,
                            SalesOrderId = order.Id,
                            SalesReturnId = salesReturn.Id,
                            Amount = requestedExternal,
                            Date = DateTime.UtcNow,
                            Type = TransactionType.Refund,
                            Description = $"Satış Para İadesi {salesReturn.ReturnNumber}"
                        });
                    }
                }
            }

            var totalReturnedQuantity = priorItems.Sum(item => item.Quantity) + salesReturn.Items.Sum(item => item.Quantity);
            var totalSoldQuantity = order.Items.Sum(item => item.Quantity);
            order.Status = totalReturnedQuantity == totalSoldQuantity ? SalesOrderStatus.Refunded : SalesOrderStatus.PartiallyRefunded;
            if (order.Status == SalesOrderStatus.Refunded && order.CustomerId is > 0)
            {
                var customer = await context.Customers.FindAsync([order.CustomerId.Value], cancellationToken);
                if (customer is not null) customer.TotalPurchaseCount = Math.Max(0, customer.TotalPurchaseCount - 1);
            }

            AddAudit(context, "Create", "SalesReturn", salesReturn.Id.ToString(), $"Satış iadesi: {salesReturn.ReturnNumber}, {salesReturn.TotalAmount:N2} ₺", command.CreatedBy);
            await context.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            return Result.Success(new ReturnTransactionResult(salesReturn.Id, salesReturn.ReturnNumber, salesReturn.TotalAmount, false));
        }
        catch (Exception exception)
        {
            await RollbackAsync(transaction, cancellationToken);
            return Result.Failure<ReturnTransactionResult>($"Satış iadesi tamamlanamadı: {exception.Message}");
        }
    }

    public async Task<Result<LegacyLedgerPreviewDto>> PreviewLegacyLedgerAsync(CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ProcessReturns);
        if (authorization.IsFailure) return Result.Failure<LegacyLedgerPreviewDto>(authorization.Error);
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var orders = await context.SalesOrders.AsNoTracking().Include(item => item.Payments)
            .Where(item => item.CustomerId != null && item.Payments.Any(payment => payment.PaymentMethod == PaymentMethod.OnAccount))
            .ToListAsync(cancellationToken);
        var existingKeys = await context.Transactions.AsNoTracking().Where(item => item.ReconciliationKey != null)
            .Select(item => item.ReconciliationKey!).ToListAsync(cancellationToken);
        var transactions = await context.Transactions.AsNoTracking().Where(item => item.Type == TransactionType.Payment).ToListAsync(cancellationToken);

        var issues = orders.Select(order =>
            {
                var key = $"LEGACY-ONACCOUNT-{order.Id}";
                var amount = order.Payments.Where(item => item.PaymentMethod == PaymentMethod.OnAccount).Sum(item => item.Amount);
                var hasLegacyPayment = transactions.Any(item => item.SalesOrderId == null && item.Description.Contains(order.OrderNumber));
                return new { order, key, amount, hasLegacyPayment };
            })
            .Where(item => item.amount > 0 && item.hasLegacyPayment && !existingKeys.Contains(item.key))
            .Select(item => new LegacyLedgerIssueDto(item.order.Id, item.order.OrderNumber, item.order.CustomerId!.Value, item.order.CustomerName, item.amount, item.key))
            .ToList();
        return Result.Success(new LegacyLedgerPreviewDto(issues, issues.Sum(item => item.OnAccountAmount)));
    }

    public async Task<Result<int>> ApplyLegacyLedgerCorrectionsAsync(
        IReadOnlyCollection<string> reconciliationKeys,
        string appliedBy,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ProcessReturns);
        if (authorization.IsFailure) return Result.Failure<int>(authorization.Error);
        var preview = await PreviewLegacyLedgerAsync(cancellationToken);
        if (preview.IsFailure || preview.Value is null) return Result.Failure<int>(preview.Error);
        var selected = preview.Value.Issues.Where(item => reconciliationKeys.Contains(item.ReconciliationKey)).ToList();
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var issue in selected)
        {
            if (await context.Transactions.AnyAsync(item => item.ReconciliationKey == issue.ReconciliationKey, cancellationToken)) continue;
            context.Transactions.Add(new Transaction
            {
                CustomerId = issue.CustomerId,
                SalesOrderId = issue.SalesOrderId,
                Amount = issue.OnAccountAmount,
                Date = DateTime.UtcNow,
                Type = TransactionType.Refund,
                Description = $"Eski cari satış tahsilat düzeltmesi {issue.OrderNumber}",
                ReconciliationKey = issue.ReconciliationKey
            });
            AddAudit(
                context,
                "Create",
                "LegacyLedgerCorrection",
                issue.SalesOrderId.ToString(),
                $"Eski cari satış için idempotent telafi hareketi eklendi: {issue.OrderNumber}, {issue.OnAccountAmount:N2} ₺",
                string.IsNullOrWhiteSpace(appliedBy) ? "Sistem" : appliedBy.Trim());
        }
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(selected.Count);
    }

    private static string? ValidateSale(CompleteSaleCommand command)
    {
        if (!Guid.TryParse(command.IdempotencyKey, out _)) return "Geçerli bir satış işlem anahtarı gereklidir.";
        if (command.WarehouseId <= 0) return "Geçerli depo seçilmelidir.";
        if (command.Items.Count == 0 || command.Items.Any(item =>
                item.ProductId <= 0 || item.Quantity <= 0 || item.UnitPrice < 0 || item.DiscountAmount < 0 ||
                item.DiscountAmount > item.UnitPrice * item.Quantity || item.TaxRate is < 0 or > 100 || item.LineTotal < 0 ||
                Math.Abs(item.LineTotal - CalculateLineTotal(item.UnitPrice, item.Quantity, item.DiscountAmount, item.TaxRate)) > 0.01m))
            return "Satış kalemlerinin fiyat, indirim veya KDV toplamı geçersiz.";
        if (command.Payments.Count == 0 || command.Payments.Any(item => item.Amount <= 0)) return "Ödeme dağılımı geçersiz.";
        var total = command.Items.Sum(item => item.LineTotal);
        if (total <= 0 || command.Payments.Sum(item => item.Amount) != total) return "Ödeme toplamı satış toplamına eşit olmalıdır.";
        return null;
    }

    private static decimal CalculateLineTotal(decimal unitPrice, int quantity, decimal discount, decimal taxRate)
    {
        var afterDiscount = unitPrice * quantity - discount;
        return Math.Round(afterDiscount + (afterDiscount * taxRate / 100m), 2, MidpointRounding.AwayFromZero);
    }

    private static async Task<Result<ReturnableSaleDto>> BuildReturnableSaleAsync(AppDbContext context, int salesOrderId, CancellationToken cancellationToken)
    {
        var order = await context.SalesOrders.AsNoTracking().Include(item => item.Items).Include(item => item.Payments)
            .SingleOrDefaultAsync(item => item.Id == salesOrderId, cancellationToken);
        if (order is null) return Result.Failure<ReturnableSaleDto>("Satış bulunamadı.");
        var returned = await context.SalesReturnItems.AsNoTracking().Where(item => item.SalesReturn.SalesOrderId == salesOrderId).ToListAsync(cancellationToken);
        var refunded = await context.SalesReturnPayments.AsNoTracking().Where(item => item.SalesReturn.SalesOrderId == salesOrderId && item.PaymentMethod != PaymentMethod.OnAccount).SumAsync(item => item.Amount, cancellationToken);
        var lines = order.Items.Select(item =>
        {
            var returnedQuantity = returned.Where(value => value.SalesOrderItemId == item.Id).Sum(value => value.Quantity);
            var returnedAmount = returned.Where(value => value.SalesOrderItemId == item.Id).Sum(value => value.LineTotal);
            return new ReturnableSaleLineDto(item.Id, item.ProductId, item.ProductName, item.Quantity, returnedQuantity, item.Quantity - returnedQuantity, item.LineTotal - returnedAmount);
        }).ToList();
        var external = order.Payments.Where(item => item.PaymentMethod != PaymentMethod.OnAccount).Sum(item => item.Amount) - refunded;
        return Result.Success(new ReturnableSaleDto(order.Id, order.OrderNumber, order.WarehouseId, Math.Max(0, external), lines));
    }

    private static decimal Allocate(decimal original, int originalQuantity, int requestedQuantity, decimal previouslyAllocated, bool isFinal) =>
        isFinal ? original - previouslyAllocated : Math.Round(original * requestedQuantity / originalQuantity, 2, MidpointRounding.AwayFromZero);

    private static async Task<Warehouse> GetOrCreateQuarantineAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var warehouse = await context.Warehouses.SingleOrDefaultAsync(item => item.IsQuarantine, cancellationToken);
        if (warehouse is not null) return warehouse;
        warehouse = new Warehouse { Name = "İade / Karantina", Type = WarehouseType.Other, IsActive = true, IsQuarantine = true };
        context.Warehouses.Add(warehouse);
        await context.SaveChangesAsync(cancellationToken);
        return warehouse;
    }

    private static string CreateNumber(string prefix) => $"{prefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..Math.Min(48, prefix.Length + 1 + 15 + 1 + 32)];

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
