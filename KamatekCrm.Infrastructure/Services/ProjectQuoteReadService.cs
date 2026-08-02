using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ProjectQuotes;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Infrastructure.Services;

public sealed class ProjectQuoteReadService : IProjectQuoteReadService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IApplicationAuthorizationService _authorization;

    public ProjectQuoteReadService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IApplicationAuthorizationService authorization)
    {
        _dbContextFactory = dbContextFactory;
        _authorization = authorization;
    }

    public async Task<Result<ProjectQuoteWorkspaceDto>> GetWorkspaceAsync(
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ManageQuotes);
        if (authorization.IsFailure)
            return Result.Failure<ProjectQuoteWorkspaceDto>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var customers = await context.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.FullName)
            .Select(customer => new ProjectQuoteCustomerDto(
                customer.Id, customer.CustomerCode, customer.FullName, customer.PhoneNumber,
                customer.Email, customer.City, customer.District, customer.Neighborhood,
                customer.Street, customer.BuildingNo, customer.ApartmentNo))
            .ToListAsync(cancellationToken);

        var products = await context.Products
            .AsNoTracking()
            .OrderBy(product => product.ProductCategoryType)
            .ThenBy(product => product.ProductName)
            .Select(product => new ProjectQuoteProductDto(
                product.Id, product.ProductName, product.SKU, product.ProductCategoryType,
                product.PurchasePrice, product.SalePrice, product.ImagePath))
            .ToListAsync(cancellationToken);

        return Result.Success(new ProjectQuoteWorkspaceDto(customers, products));
    }

    public async Task<Result<ProjectQuoteDetailDto>> GetAsync(
        int projectId,
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ManageQuotes);
        if (authorization.IsFailure)
            return Result.Failure<ProjectQuoteDetailDto>(authorization.Error);
        if (projectId <= 0)
            return Result.Failure<ProjectQuoteDetailDto>("Geçerli bir proje seçilmelidir.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var project = await context.ServiceProjects
            .AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new ProjectQuoteDetailDto(
                item.Id, item.Title, item.CustomerId, item.ProjectCode, item.ProjectScopeJson,
                item.TotalBudget, item.TotalCost, item.TotalProfit, item.DiscountPercent,
                item.CreatedDate, item.PipelineStage, item.Status, item.TotalUnitCount,
                item.SurveyNotes, item.QuoteItemsJson, item.QuoteNumber, item.QuoteStatus,
                item.RevisionNumber, item.SentDate, item.ValidUntil, item.ApprovedDate,
                item.RejectedDate, item.RejectionReason, item.KdvRate, item.Notes,
                item.PaymentTerms, item.RevisionsJson))
            .SingleOrDefaultAsync(cancellationToken);

        return project is null
            ? Result.Failure<ProjectQuoteDetailDto>("Proje teklifi bulunamadı.")
            : Result.Success(project);
    }

    public async Task<Result<IReadOnlyList<ProjectQuoteListItemDto>>> GetListAsync(
        CancellationToken cancellationToken = default)
    {
        var authorization = _authorization.Authorize(ApplicationPermission.ManageQuotes);
        if (authorization.IsFailure)
            return Result.Failure<IReadOnlyList<ProjectQuoteListItemDto>>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var quotes = await context.ServiceProjects
            .AsNoTracking()
            .OrderByDescending(project => project.CreatedDate)
            .Select(project => new ProjectQuoteListItemDto(
                project.Id, project.Title, project.CustomerId,
                project.Customer != null ? project.Customer.FullName : "Müşteri Atanmamış",
                project.ProjectCode, project.QuoteNumber, project.TotalBudget, project.TotalCost,
                project.TotalProfit, project.DiscountPercent, project.KdvRate, project.QuoteStatus,
                project.RevisionNumber, project.CreatedDate, project.SentDate, project.ValidUntil))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ProjectQuoteListItemDto>>(quotes);
    }

    public async Task<Result<ProjectQuoteExportDto>> GetExportAsync(
        int projectId,
        CancellationToken cancellationToken = default)
    {
        var detailResult = await GetAsync(projectId, cancellationToken);
        if (detailResult.IsFailure || detailResult.Value is null)
            return Result.Failure<ProjectQuoteExportDto>(detailResult.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var customer = detailResult.Value.CustomerId.HasValue
            ? await context.Customers.AsNoTracking()
                .Where(item => item.Id == detailResult.Value.CustomerId.Value)
                .Select(item => new ProjectQuoteCustomerDto(
                    item.Id, item.CustomerCode, item.FullName, item.PhoneNumber, item.Email,
                    item.City, item.District, item.Neighborhood, item.Street,
                    item.BuildingNo, item.ApartmentNo))
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return Result.Success(new ProjectQuoteExportDto(detailResult.Value, customer));
    }
}
