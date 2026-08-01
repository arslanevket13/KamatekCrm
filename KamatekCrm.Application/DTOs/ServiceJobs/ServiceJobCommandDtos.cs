using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ApplicationCore.DTOs.ServiceJobs;

public sealed record ServiceJobSaveRequest(
    ServiceJob Job,
    IReadOnlyCollection<ServiceJobItem> Items,
    bool IsEditing,
    string ChangedBy);

public sealed record ServiceJobSaveResult(
    int JobId,
    bool IsStockReserved,
    int ReservationCount);

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
