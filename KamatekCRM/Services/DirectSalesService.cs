using KamatekCrm.ApplicationCore.DTOs.Transactions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Services;

/// <summary>
/// Mevcut POS ViewModel sözleşmesini yeni Application işlem servisine uyarlayan geçiş adaptörü.
/// İş kuralı ve transaction yönetimi bu sınıfta bulunmaz.
/// </summary>
public sealed class DirectSalesService : IDirectSalesService
{
    private readonly IRetailTransactionService _transactions;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public DirectSalesService(
        IRetailTransactionService transactions,
        IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _transactions = transactions;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<SalesOrder> ProcessSaleAsync(
        int? customerId,
        string customerName,
        int warehouseId,
        IEnumerable<PosCartItem> cartItems,
        IEnumerable<PosPaymentEntry> payments,
        string? notes,
        string? currentUserName,
        string idempotencyKey)
    {
        var command = new CompleteSaleCommand(
            customerId,
            customerName,
            warehouseId,
            cartItems.Select(item => new TransactionLineInput(
                item.ProductId,
                item.ProductName,
                item.Quantity,
                item.UnitPrice,
                item.DiscountAmount,
                item.TaxRate,
                item.LineTotal)).ToList(),
            payments.Select(item => new PaymentAllocationInput(item.PaymentMethod, item.Amount, item.Reference)).ToList(),
            notes,
            currentUserName ?? "Kasiyer",
            idempotencyKey);

        var result = await _transactions.CompleteSaleAsync(command);
        if (result.IsFailure || result.Value is null)
        {
            if (result.Error.Contains("yetki", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException(result.Error);
            throw new InvalidOperationException(result.Error);
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.SalesOrders.AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.Payments)
            .SingleAsync(order => order.Id == result.Value.SalesOrderId);
    }
}
