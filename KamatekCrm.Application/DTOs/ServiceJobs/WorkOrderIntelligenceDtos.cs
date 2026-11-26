using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.DTOs.ServiceJobs;

public sealed class WorkOrderSubTaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class DiscoveryChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public bool IsChecked { get; set; }
    public string? Note { get; set; }
}

public sealed record CustomerPastJobSummaryDto(
    int JobId,
    string Title,
    string CategoriesDisplay,
    JobStatus Status,
    DateTime CreatedDate,
    DateTime? CompletedDate,
    decimal TotalAmount);

public sealed record WorkOrderCustomerMemoryDto(
    int CustomerId,
    int PastJobCount,
    IReadOnlyList<CustomerPastJobSummaryDto> LastJobs,
    decimal OpenBalance,
    bool HasRepeatWarrantyWarning,
    string? RepeatWarrantyWarningMessage,
    int? RepeatWarrantyJobId);

public enum WorkOrderAlertCode
{
    UnassignedTech = 0,
    DiscoveryDoneNoQuote48h = 1,
    QuoteSentNoReply72h = 2,
    InstalledUnpaid = 3,
    SlaAtRisk = 4,
    FollowUpDue = 5,
    StockShortage = 6
}

public sealed record WorkOrderAlertBadgeDto(
    WorkOrderAlertCode Code,
    string Text,
    WorkOrderSeverity Severity);

public sealed record WorkOrderProfitabilityDto(
    int ServiceJobId,
    decimal MaterialCost,
    decimal QuotedTotal,
    decimal LaborFee,
    decimal Discount,
    decimal EstimatedProfit,
    decimal ProfitMarginPercent,
    decimal CollectedAmount,
    decimal RemainingBalance);

public sealed record QuickMaterialKitItemDto(
    int? ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    int AvailableStock,
    bool IsInStock);

public sealed record QuickMaterialKitDto(
    string Key,
    string Name,
    string Category,
    string Description,
    IReadOnlyList<QuickMaterialKitItemDto> Items);

public sealed record SaveSmartWorkOrderFieldsRequest(
    int JobId,
    string? SubTasksJson,
    string? DiscoveryChecklistJson,
    DateTime? NextFollowUpUtc,
    string? FollowUpNote,
    string ChangedBy,
    bool SyncCustomerInteraction = true);
