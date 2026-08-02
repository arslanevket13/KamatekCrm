using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ApplicationCore.DTOs.ProjectQuotes;

public sealed record ProjectQuoteCustomerDto(
    int Id,
    string CustomerCode,
    string FullName,
    string PhoneNumber,
    string? Email,
    string City,
    string? District,
    string? Neighborhood,
    string? Street,
    string? BuildingNo,
    string? ApartmentNo);

public sealed record ProjectQuoteProductDto(
    int Id,
    string ProductName,
    string? Sku,
    ProductCategoryType Category,
    decimal PurchasePrice,
    decimal SalePrice,
    string? ImagePath);

public sealed record ProjectQuoteWorkspaceDto(
    IReadOnlyList<ProjectQuoteCustomerDto> Customers,
    IReadOnlyList<ProjectQuoteProductDto> Products);

public sealed record ProjectQuoteListItemDto(
    int Id,
    string Title,
    int? CustomerId,
    string CustomerName,
    string ProjectCode,
    string? QuoteNumber,
    decimal TotalBudget,
    decimal TotalCost,
    decimal TotalProfit,
    decimal DiscountPercent,
    decimal KdvRate,
    QuoteStatus QuoteStatus,
    int RevisionNumber,
    DateTime CreatedDate,
    DateTime? SentDate,
    DateTime? ValidUntil);

public sealed record ProjectQuoteExportDto(
    ProjectQuoteDetailDto Quote,
    ProjectQuoteCustomerDto? Customer);

public sealed record ProjectQuoteDetailDto(
    int Id,
    string Title,
    int? CustomerId,
    string ProjectCode,
    string ProjectScopeJson,
    decimal TotalBudget,
    decimal TotalCost,
    decimal TotalProfit,
    decimal DiscountPercent,
    DateTime CreatedDate,
    PipelineStage PipelineStage,
    ProjectStatus Status,
    int TotalUnitCount,
    string SurveyNotes,
    string QuoteItemsJson,
    string? QuoteNumber,
    QuoteStatus QuoteStatus,
    int RevisionNumber,
    DateTime? SentDate,
    DateTime? ValidUntil,
    DateTime? ApprovedDate,
    DateTime? RejectedDate,
    string? RejectionReason,
    decimal KdvRate,
    string? Notes,
    string? PaymentTerms,
    string? RevisionsJson);

public sealed record ProjectQuotePricingResult(
    decimal GrossRevenue,
    decimal TotalCost,
    decimal DiscountAmount,
    decimal NetRevenue,
    decimal VatAmount,
    decimal GrandTotal,
    decimal TotalProfit,
    decimal MarginPercent,
    int IncludedLineCount,
    int TotalQuantity);

public sealed record SaveProjectQuoteCommand(
    Guid IdempotencyKey,
    int? ProjectId,
    int ExpectedRevisionNumber,
    int CustomerId,
    string Title,
    string ProjectScopeJson,
    decimal DiscountPercent,
    decimal KdvRate);

public sealed record ProjectQuoteSaveResult(
    int ProjectId,
    string ProjectCode,
    string QuoteNumber,
    int RevisionNumber,
    QuoteStatus Status,
    ProjectQuotePricingResult Pricing,
    bool WasAlreadyApplied,
    bool WasNoOp);

public sealed record ChangeProjectQuoteStatusCommand(
    Guid IdempotencyKey,
    int ProjectId,
    int ExpectedRevisionNumber,
    QuoteStatus ExpectedStatus,
    QuoteStatus TargetStatus,
    string? Reason = null,
    int ValidityDays = 30);

public sealed record DuplicateProjectQuoteCommand(
    Guid IdempotencyKey,
    int SourceProjectId,
    int ExpectedRevisionNumber);

public sealed record DeleteProjectQuoteCommand(
    Guid IdempotencyKey,
    int ProjectId,
    int ExpectedRevisionNumber,
    QuoteStatus ExpectedStatus);

public sealed record ConvertApprovedQuoteToWorkOrderCommand(
    Guid IdempotencyKey,
    int ProjectId,
    int ExpectedRevisionNumber,
    QuoteStatus ExpectedStatus);

public sealed record ProjectQuoteOperationResult(
    int ProjectId,
    QuoteStatus Status,
    int RevisionNumber,
    bool WasAlreadyApplied);

public sealed record ProjectQuoteDuplicateResult(
    int ProjectId,
    string ProjectCode,
    string QuoteNumber,
    bool WasAlreadyApplied);

public sealed record ProjectQuoteWorkOrderResult(
    int ProjectId,
    int WorkOrderId,
    bool WasAlreadyApplied);

public sealed record ExpireProjectQuotesResult(int ExpiredCount);
