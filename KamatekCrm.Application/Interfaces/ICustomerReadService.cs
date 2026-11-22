using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Customers;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.Interfaces;

public sealed record CustomerPagedQueryResult(
    IReadOnlyList<CustomerListItemDto> Items,
    int FilteredCount,
    int TotalCustomers,
    int IndividualCount,
    int CorporateCount,
    int WalkInCount);

/// <summary>
/// Müşteri listeleme ekranı için SQL seviyesinde sayfalanmış salt-okunur sorgu servisi.
/// </summary>
public interface ICustomerReadService
{
    Task<Result<CustomerPagedQueryResult>> GetPagedAsync(
        string? searchText,
        CustomerType? typeFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
