using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Customers;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Infrastructure.Services;

/// <summary>
/// Müşteri listeleme için SQL seviyesinde filtreleme, sayım ve sayfalama uygulayan read servisi.
/// </summary>
public sealed class CustomerReadService : ICustomerReadService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public CustomerReadService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public async Task<Result<CustomerPagedQueryResult>> GetPagedAsync(
        string? searchText,
        CustomerType? typeFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var baseQuery = context.Customers.AsNoTracking().Where(c => !c.IsDeleted);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                string term = searchText.Trim().ToLower();
                string normalizedDigits = CustomerValidationHelper.NormalizePhoneNumber(searchText);
                string digitsOnly = new string(searchText.Where(char.IsDigit).ToArray());

                baseQuery = baseQuery.Where(c =>
                    c.FullName.ToLower().Contains(term) ||
                    c.CustomerCode.ToLower().Contains(term) ||
                    c.PhoneNumber.Contains(term) ||
                    (c.Email != null && c.Email.ToLower().Contains(term)) ||
                    c.City.ToLower().Contains(term) ||
                    (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)) ||
                    (digitsOnly.Length >= 4 &&
                     (c.PhoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace("+", "").Contains(digitsOnly) ||
                      (!string.IsNullOrEmpty(normalizedDigits) &&
                       c.PhoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace("+", "").Contains(normalizedDigits)))));
            }

            // Tip kırılım sayılarını SQL GroupBy ile tek sorguda hesapla (belleğe tam liste çekmeden)
            var typeCounts = await baseQuery
                .GroupBy(c => c.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            int individualCount = typeCounts.FirstOrDefault(x => x.Type == CustomerType.Individual)?.Count ?? 0;
            int corporateCount = typeCounts.FirstOrDefault(x => x.Type == CustomerType.Corporate)?.Count ?? 0;
            int walkInCount = typeCounts.FirstOrDefault(x => x.Type == CustomerType.WalkIn)?.Count ?? 0;
            int allMatchesCount = typeCounts.Sum(x => x.Count);

            var filteredQuery = typeFilter.HasValue
                ? baseQuery.Where(c => c.Type == typeFilter.Value)
                : baseQuery;

            int filteredCount = await filteredQuery.CountAsync(cancellationToken);

            int safePageSize = Math.Clamp(pageSize, 1, 200);
            int safePage = Math.Max(1, page);

            var items = await filteredQuery
                .OrderByDescending(c => c.Id)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(c => new CustomerListItemDto
                {
                    Id = c.Id,
                    CustomerCode = c.CustomerCode,
                    FullName = c.FullName,
                    CompanyName = c.CompanyName,
                    PhoneNumber = c.PhoneNumber,
                    Email = c.Email,
                    City = c.City,
                    District = c.District,
                    Type = c.Type,
                    Segment = c.Segment,
                    TotalSpent = c.TotalSpent,
                    CreatedDate = c.CreatedDate
                })
                .ToListAsync(cancellationToken);

            return Result.Success(new CustomerPagedQueryResult(
                items,
                filteredCount,
                allMatchesCount,
                individualCount,
                corporateCount,
                walkInCount));
        }
        catch (Exception ex)
        {
            return Result.Failure<CustomerPagedQueryResult>($"Müşteriler yüklenirken hata oluştu: {ex.Message}");
        }
    }
}
