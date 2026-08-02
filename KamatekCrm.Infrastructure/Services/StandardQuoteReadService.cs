using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Quotes;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Infrastructure.Services;

public sealed class StandardQuoteReadService : IStandardQuoteReadService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IApplicationAuthorizationService _authorization;

    public StandardQuoteReadService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IApplicationAuthorizationService authorization)
    {
        _dbContextFactory = dbContextFactory;
        _authorization = authorization;
    }

    public async Task<Result<StandardQuoteWorkspaceDto>> GetWorkspaceAsync(
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ManageQuotes);
        if (authorization.IsFailure) return Result.Failure<StandardQuoteWorkspaceDto>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var customers = await context.Customers.AsNoTracking()
            .OrderBy(customer => customer.FullName)
            .Select(customer => new StandardQuoteCustomerDto(
                customer.Id, customer.CustomerCode, customer.FullName, customer.PhoneNumber,
                customer.Email, customer.City, customer.District, customer.Neighborhood,
                customer.Street, customer.BuildingNo, customer.ApartmentNo))
            .ToListAsync(cancellationToken);
        return Result.Success(new StandardQuoteWorkspaceDto(customers));
    }

    public async Task<Result<IReadOnlyList<StandardQuoteProductDto>>> SearchProductsAsync(
        string searchText,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ManageQuotes);
        if (authorization.IsFailure)
            return Result.Failure<IReadOnlyList<StandardQuoteProductDto>>(authorization.Error);
        var term = searchText.Trim();
        if (term.Length < 2)
            return Result.Success<IReadOnlyList<StandardQuoteProductDto>>([]);
        take = Math.Clamp(take, 1, 50);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalized = term.ToLower();
        var products = await context.Products.AsNoTracking()
            .Where(product => product.ProductName.ToLower().Contains(normalized) ||
                              product.SKU.ToLower().Contains(normalized))
            .OrderBy(product => product.ProductName)
            .Take(take)
            .Select(product => new StandardQuoteProductDto(
                product.Id, product.ProductName, product.SKU, product.Unit,
                product.SalePrice, product.PurchasePrice, product.VatRate,
                product.TotalStockQuantity, product.ImagePath))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<StandardQuoteProductDto>>(products);
    }

    public async Task<Result<StandardQuoteDocumentDto>> GetDocumentAsync(
        int quoteId,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ManageQuotes);
        if (authorization.IsFailure) return Result.Failure<StandardQuoteDocumentDto>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var quote = await context.Quotes.AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == quoteId, cancellationToken);
        return quote is null
            ? Result.Failure<StandardQuoteDocumentDto>("Standart teklif bulunamadı.")
            : Result.Success(new StandardQuoteDocumentDto(quote, quote.Lines.ToList()));
    }
}
