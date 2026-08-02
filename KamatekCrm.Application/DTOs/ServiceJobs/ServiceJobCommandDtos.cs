using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ApplicationCore.DTOs.ServiceJobs;

public sealed record ServiceJobSaveRequest(
    ServiceJob Job,
    IReadOnlyCollection<ServiceJobItem> Items,
    bool IsEditing,
    string ChangedBy,
    ServiceJobQuickCustomerInput? QuickCustomer = null,
    ServiceJobNewAssetInput? NewAsset = null);

public sealed record ServiceJobQuickCustomerInput(
    string FullName,
    string PhoneNumber);

public sealed record ServiceJobNewAssetInput(
    JobCategory Category,
    string Brand,
    string Model,
    string? SerialNumber,
    string? Location);

public sealed record ServiceJobSaveResult(
    int JobId,
    bool IsStockReserved,
    int ReservationCount,
    int CustomerId,
    int? CustomerAssetId);

public sealed record ServiceJobDeleteResult(int JobId);

public sealed record ServiceJobStatusChangeResult(
    int JobId,
    JobStatus PreviousStatus,
    JobStatus CurrentStatus,
    DateTime? CompletedDate);

public sealed record ServiceJobQuoteConversionResult(
    int JobId,
    int CustomerId,
    JobStatus PreviousStatus,
    JobStatus CurrentStatus);
