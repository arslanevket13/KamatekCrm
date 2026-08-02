using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Infrastructure.Services;

/// <summary>
/// Servis işi ekranlarının salt-okunur projection sınırıdır. UI katmanına
/// DbContext veya izlenen EF varlığı taşımaz.
/// </summary>
public sealed class ServiceJobReadService : IServiceJobReadService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IApplicationAuthorizationService _authorization;

    public ServiceJobReadService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IApplicationAuthorizationService authorization)
    {
        _dbContextFactory = dbContextFactory;
        _authorization = authorization;
    }

    public async Task<Result<ServiceJobWorkspaceDto>> GetWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var authorization = AuthorizeRead();
        if (authorization.IsFailure) return Result.Failure<ServiceJobWorkspaceDto>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var customerRows = await context.Customers.AsNoTracking()
            .OrderBy(item => item.FullName)
            .Select(item => new
            {
                item.Id, item.FullName, item.City, item.District, item.Neighborhood,
                item.Street, item.BuildingNo, item.ApartmentNo
            })
            .ToListAsync(cancellationToken);
        var customers = customerRows.Select(item => new ServiceJobCustomerLookupDto(
            item.Id,
            item.FullName,
            BuildAddress(item.Neighborhood, item.Street, item.BuildingNo, item.ApartmentNo, item.District, item.City)))
            .ToList();
        var products = await context.Products.AsNoTracking()
            .OrderBy(item => item.ProductName)
            .Select(item => new ServiceJobProductLookupDto(item.Id, item.ProductName, item.SalePrice, item.PurchasePrice))
            .ToListAsync(cancellationToken);
        var technicians = await context.Users.AsNoTracking()
            .Where(item => item.IsActive && (item.Role == "Personel" || item.Role == "Admin" || item.IsTechnician))
            .OrderBy(item => item.Ad).ThenBy(item => item.Soyad)
            .Select(item => new ServiceJobTechnicianLookupDto(
                item.Id,
                (item.Ad + " " + item.Soyad).Trim() == string.Empty
                    ? item.Username
                    : (item.Ad + " " + item.Soyad).Trim()))
            .ToListAsync(cancellationToken);

        return Result.Success(new ServiceJobWorkspaceDto(customers, products, technicians));
    }

    public async Task<Result<IReadOnlyList<ServiceJobRowDto>>> SearchAsync(
        ServiceJobSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = AuthorizeRead();
        if (authorization.IsFailure) return Result.Failure<IReadOnlyList<ServiceJobRowDto>>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.ServiceJobs.AsNoTracking().AsQueryable();
        string searchText = request.SearchText?.Trim().ToLower() ?? string.Empty;
        if (searchText.Length > 0)
        {
            query = query.Where(item => item.Description.ToLower().Contains(searchText) ||
                                        item.Customer.FullName.ToLower().Contains(searchText) ||
                                        item.Customer.PhoneNumber.Contains(searchText));
        }
        if (request.Status.HasValue) query = query.Where(item => item.Status == request.Status.Value);
        if (request.StartDate.HasValue) query = query.Where(item => item.CreatedDate >= request.StartDate.Value);
        if (request.EndDate.HasValue)
        {
            DateTime endExclusive = request.EndDate.Value.Date.AddDays(1);
            query = query.Where(item => item.CreatedDate < endExclusive);
        }

        var rows = await query
            .OrderByDescending(item => item.CreatedDate)
            .Take(Math.Clamp(request.Take, 1, 500))
            .Select(item => new ServiceJobRowDto
            {
                Id = item.Id,
                CustomerId = item.CustomerId,
                CustomerAssetId = item.CustomerAssetId,
                CustomerFullName = item.Customer.FullName,
                CustomerPhone = item.Customer.PhoneNumber,
                Description = item.Description,
                Status = item.Status,
                Priority = item.Priority,
                WorkOrderType = item.WorkOrderType,
                JobCategory = item.JobCategory,
                CategoriesJson = item.CategoriesJson,
                CreatedDate = item.CreatedDate,
                CompletedDate = item.CompletedDate,
                ScheduledDate = item.ScheduledDate,
                AssignedTechnicianId = item.AssignedTechnicianId,
                AssignedTechnician = item.AssignedTechnician,
                LaborCost = item.LaborCost,
                DiscountAmount = item.DiscountAmount,
                TaxAmount = item.TaxAmount,
                TotalAmount = item.TotalAmount,
                EstimatedDuration = item.EstimatedDuration,
                SlaDeadline = item.SlaDeadline,
                TechnicianNotes = item.TechnicianNotes,
                PhotoPathsJson = item.PhotoPathsJson
            })
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ServiceJobRowDto>>(rows);
    }

    public async Task<Result<IReadOnlyList<ServiceJobAssetLookupDto>>> GetCustomerAssetsAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        var authorization = AuthorizeRead();
        if (authorization.IsFailure) return Result.Failure<IReadOnlyList<ServiceJobAssetLookupDto>>(authorization.Error);
        if (customerId <= 0) return Result.Failure<IReadOnlyList<ServiceJobAssetLookupDto>>("Geçerli bir müşteri seçilmelidir.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.CustomerAssets.AsNoTracking()
            .Where(item => item.CustomerId == customerId)
            .OrderBy(item => item.Brand).ThenBy(item => item.Model)
            .Select(item => new ServiceJobAssetLookupDto(
                item.Id, item.Category, item.Brand, item.Model, item.SerialNumber, item.Location))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ServiceJobAssetLookupDto>>(rows);
    }

    public async Task<Result<IReadOnlyList<ServiceJobProjectLookupDto>>> GetCustomerProjectsAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        var authorization = AuthorizeRead();
        if (authorization.IsFailure) return Result.Failure<IReadOnlyList<ServiceJobProjectLookupDto>>(authorization.Error);
        if (customerId <= 0) return Result.Failure<IReadOnlyList<ServiceJobProjectLookupDto>>("Geçerli bir müşteri seçilmelidir.");

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.ServiceProjects.AsNoTracking()
            .Where(item => item.CustomerId == customerId)
            .OrderByDescending(item => item.CreatedDate)
            .Select(item => new ServiceJobProjectLookupDto(item.Id, item.Name == string.Empty ? item.Title : item.Name))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ServiceJobProjectLookupDto>>(rows);
    }

    public async Task<Result<IReadOnlyList<ServiceJobMaterialDto>>> GetMaterialsAsync(
        int jobId,
        CancellationToken cancellationToken = default)
    {
        var authorization = AuthorizeRead();
        if (authorization.IsFailure) return Result.Failure<IReadOnlyList<ServiceJobMaterialDto>>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.ServiceJobItems.AsNoTracking()
            .Where(item => item.ServiceJobId == jobId && item.ProductId.HasValue)
            .OrderBy(item => item.Id)
            .Select(item => new ServiceJobMaterialDto(
                item.Id,
                item.ProductId!.Value,
                item.Product != null ? item.Product.ProductName : $"Ürün #{item.ProductId}",
                item.QuantityUsed,
                item.UnitPrice,
                item.UnitCost))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(rows);
    }

    public async Task<Result<IReadOnlyList<ServiceJobHistoryDto>>> GetHistoryAsync(
        int jobId,
        CancellationToken cancellationToken = default)
    {
        var authorization = AuthorizeRead();
        if (authorization.IsFailure) return Result.Failure<IReadOnlyList<ServiceJobHistoryDto>>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.ServiceJobHistories.AsNoTracking()
            .Where(item => item.ServiceJobId == jobId)
            .OrderByDescending(item => item.Date)
            .Select(item => new ServiceJobHistoryDto(
                item.Id, item.Date, item.JobStatusChange, item.TechnicianNote,
                item.Action, item.Notes, item.UserId))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ServiceJobHistoryDto>>(rows);
    }

    public async Task<Result<ServiceJobDashboardDto>> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var authorization = AuthorizeRead();
        if (authorization.IsFailure) return Result.Failure<ServiceJobDashboardDto>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        DateTime now = DateTime.UtcNow;
        DateTime today = now.Date;
        DateTime tomorrow = today.AddDays(1);
        int total = await context.ServiceJobs.AsNoTracking().CountAsync(cancellationToken);
        int pending = await context.ServiceJobs.AsNoTracking().CountAsync(item => item.Status == JobStatus.Pending, cancellationToken);
        int inProgress = await context.ServiceJobs.AsNoTracking().CountAsync(item => item.Status == JobStatus.InProgress, cancellationToken);
        int completed = await context.ServiceJobs.AsNoTracking().CountAsync(item => item.Status == JobStatus.Completed, cancellationToken);
        int breached = await context.ServiceJobs.AsNoTracking().CountAsync(
            item => item.SlaDeadline < now && item.Status != JobStatus.Completed && item.Status != JobStatus.Cancelled,
            cancellationToken);
        int todayCreated = await context.ServiceJobs.AsNoTracking().CountAsync(
            item => item.CreatedDate >= today && item.CreatedDate < tomorrow,
            cancellationToken);
        var completionRows = await context.ServiceJobs.AsNoTracking()
            .Where(item => item.CompletedDate.HasValue)
            .Select(item => new { item.CreatedDate, item.CompletedDate })
            .ToListAsync(cancellationToken);
        double averageHours = completionRows.Count == 0
            ? 0
            : completionRows.Average(item => (item.CompletedDate!.Value - item.CreatedDate).TotalHours);

        return Result.Success(new ServiceJobDashboardDto(
            total, pending, inProgress, completed, breached, todayCreated, averageHours));
    }

    public async Task<Result<ServiceJobDocumentDto>> GetDocumentAsync(
        int jobId,
        CancellationToken cancellationToken = default)
    {
        var authorization = AuthorizeRead();
        if (authorization.IsFailure) return Result.Failure<ServiceJobDocumentDto>(authorization.Error);

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.ServiceJobs.AsNoTracking()
            .Where(item => item.Id == jobId)
            .Select(item => new
            {
                item.Id, item.WorkOrderType, item.Description, item.DiscoveryTechnicalNotes,
                item.TechnicianNotes, item.AssignedTechnician, item.Priority, item.ScheduledDate,
                item.CustomerId, item.Customer.FullName, item.Customer.CompanyName,
                item.Customer.PhoneNumber, item.Customer.City, item.Customer.District,
                item.Customer.Neighborhood, item.Customer.Street, item.Customer.BuildingNo,
                item.Customer.ApartmentNo
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return Result.Failure<ServiceJobDocumentDto>("İş kaydı bulunamadı.");

        return Result.Success(new ServiceJobDocumentDto(
            row.Id, row.WorkOrderType, row.Description, row.DiscoveryTechnicalNotes,
            row.TechnicianNotes, row.AssignedTechnician, row.Priority, row.ScheduledDate,
            row.CustomerId, row.FullName, row.CompanyName ?? string.Empty, row.PhoneNumber,
            BuildAddress(row.Neighborhood, row.Street, row.BuildingNo, row.ApartmentNo, row.District, row.City)));
    }

    private Result AuthorizeRead()
    {
        var serviceAuthorization = _authorization.Authorize(ApplicationPermission.ManageServiceJobs);
        return serviceAuthorization.IsFailure
            ? serviceAuthorization
            : _authorization.Authorize(ApplicationPermission.ViewCustomerContactData);
    }

    private static string BuildAddress(
        string? neighborhood,
        string? street,
        string? buildingNo,
        string? apartmentNo,
        string? district,
        string? city)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(neighborhood)) parts.Add(neighborhood);
        if (!string.IsNullOrWhiteSpace(street)) parts.Add(street);
        if (!string.IsNullOrWhiteSpace(buildingNo)) parts.Add($"No: {buildingNo}");
        if (!string.IsNullOrWhiteSpace(apartmentNo)) parts.Add($"D: {apartmentNo}");
        if (!string.IsNullOrWhiteSpace(district)) parts.Add(district);
        if (!string.IsNullOrWhiteSpace(city)) parts.Add(city);
        return string.Join(", ", parts);
    }
}
