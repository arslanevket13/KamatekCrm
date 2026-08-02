using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Transactions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Infrastructure.Services;

public sealed class TransactionReadService : ITransactionReadService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IApplicationAuthorizationService _authorization;

    public TransactionReadService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IApplicationAuthorizationService authorization)
    {
        _dbContextFactory = dbContextFactory;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<WarehouseLookupDto>>> GetActiveWarehousesAsync(
        bool includeQuarantine,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ProcessReturns);
        if (authorization.IsFailure) return Result.Failure<IReadOnlyList<WarehouseLookupDto>>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.Warehouses.AsNoTracking().Where(item => item.IsActive);
        if (!includeQuarantine) query = query.Where(item => !item.IsQuarantine);
        var rows = await query.OrderBy(item => item.Name)
            .Select(item => new WarehouseLookupDto(item.Id, item.Name, item.IsQuarantine))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<WarehouseLookupDto>>(rows);
    }

    public async Task<Result<IReadOnlyList<PurchaseHistoryDto>>> GetPurchaseHistoryAsync(
        int take = 150,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ProcessReturns);
        if (authorization.IsFailure) return Result.Failure<IReadOnlyList<PurchaseHistoryDto>>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.PurchaseOrders.AsNoTracking()
            .OrderByDescending(item => item.OrderDate)
            .Take(Math.Clamp(take, 1, 500))
            .Select(item => new PurchaseHistoryDto(
                item.Id,
                item.InvoiceNumber,
                item.OrderDate,
                item.Supplier.CompanyName,
                item.TotalAmount,
                item.Status))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<PurchaseHistoryDto>>(rows);
    }

    public async Task<Result<SalesReturnReceiptDto>> GetSalesReturnReceiptAsync(
        int salesReturnId,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ProcessReturns);
        if (authorization.IsFailure) return Result.Failure<SalesReturnReceiptDto>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.SalesReturns.AsNoTracking()
            .Include(item => item.Items).ThenInclude(item => item.SalesOrderItem)
            .Include(item => item.Payments)
            .Include(item => item.SalesOrder)
            .SingleOrDefaultAsync(item => item.Id == salesReturnId, cancellationToken);
        if (entity is null) return Result.Failure<SalesReturnReceiptDto>("İade fişi kaydı bulunamadı.");

        return Result.Success(new SalesReturnReceiptDto(
            entity.Id,
            entity.ReturnNumber,
            entity.SalesOrder.OrderNumber,
            entity.Date,
            entity.Reason,
            entity.TotalAmount,
            entity.Items.Select(item => new SalesReturnReceiptLineDto(item.SalesOrderItem.ProductName, item.Quantity, item.LineTotal)).ToList(),
            entity.Payments.Select(item => new SalesReturnReceiptPaymentDto(item.PaymentMethod, item.Amount, item.Reference)).ToList()));
    }

    public async Task<Result<PurchasingWorkspaceDto>> GetPurchasingWorkspaceAsync(
        int historyTake = 50,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var products = await context.Products.AsNoTracking().OrderBy(item => item.ProductName)
            .Select(item => new PurchaseProductLookupDto(item.Id, item.ProductName, item.SKU, item.Barcode, item.Unit, item.PurchasePrice, item.VatRate))
            .ToListAsync(cancellationToken);
        var suppliers = await context.Suppliers.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.CompanyName)
            .Select(item => new SupplierLookupDto(item.Id, item.CompanyName))
            .ToListAsync(cancellationToken);
        var warehouses = await context.Warehouses.AsNoTracking().Where(item => item.IsActive && !item.IsQuarantine).OrderBy(item => item.Name)
            .Select(item => new WarehouseLookupDto(item.Id, item.Name, item.IsQuarantine))
            .ToListAsync(cancellationToken);
        var orders = await context.PurchaseOrders.AsNoTracking().OrderByDescending(item => item.OrderDate)
            .Take(Math.Clamp(historyTake, 0, 500))
            .Select(item => new PurchaseHistoryDto(item.Id, item.InvoiceNumber, item.OrderDate, item.Supplier.CompanyName, item.TotalAmount, item.Status))
            .ToListAsync(cancellationToken);
        return Result.Success(new PurchasingWorkspaceDto(products, suppliers, warehouses, orders));
    }

    public async Task<Result<IReadOnlyList<PurchaseProductLookupDto>>> SearchPurchaseProductsAsync(
        string searchText,
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Result.Success<IReadOnlyList<PurchaseProductLookupDto>>(Array.Empty<PurchaseProductLookupDto>());

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var search = searchText.Trim().ToLower();
        var rows = await context.Products.AsNoTracking()
            .Where(item => item.ProductName.ToLower().Contains(search) ||
                           item.SKU.ToLower().Contains(search) ||
                           item.Barcode.ToLower().Contains(search))
            .OrderBy(item => item.ProductName)
            .Take(Math.Clamp(take, 1, 50))
            .Select(item => new PurchaseProductLookupDto(item.Id, item.ProductName, item.SKU, item.Barcode, item.Unit, item.PurchasePrice, item.VatRate))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<PurchaseProductLookupDto>>(rows);
    }
}
