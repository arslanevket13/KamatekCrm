using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.DTOs.ServiceJobs;

// ───────────────────────────── Komut DTO'ları ─────────────────────────────

public sealed record QuotationItemInput(
    int? Id,
    int? ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxPercent);

public sealed record UpdateWorkOrderQuotationRequest(
    int QuotationId,
    string? Description,
    string? Warranty,
    string? DeliveryTime,
    string? PaymentTerms,
    decimal LaborCost,
    decimal ShippingCost,
    decimal DiscountAmount,
    decimal TaxRate,
    IReadOnlyCollection<QuotationItemInput> Items);

public sealed record WorkOrderQuotationResult(
    int QuotationId,
    QuotationStatus Status,
    decimal TotalAmount);

public sealed record PlanInstallationRequest(
    int JobId,
    int? TechnicianId,
    string? TechnicianName,
    DateTime? InstallationDate,
    string? Notes,
    string ChangedBy);

public sealed record CompleteInstallationRequest(
    int JobId,
    string? DeliveryNote,
    string? CompletionTechnician,
    string? CustomerSignature,
    string ChangedBy);

// ───────────────────────────── Okuma DTO'ları ─────────────────────────────

public sealed record DiscoveryMaterialDto(
    int Id,
    int? ProductId,
    string ProductName,
    int Quantity,
    string? Notes);

public sealed record DiscoveryReportDto(
    int Id,
    int ServiceJobId,
    string? TechnicalNotes,
    string? RecommendedSolution,
    IReadOnlyList<string> PhotoPaths,
    double EstimatedLaborHours,
    string? TechnicianName,
    IReadOnlyList<DiscoveryMaterialDto> Materials);

public sealed record QuotationItemDto(
    int Id,
    int? ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxPercent,
    decimal LineTotal);

public sealed record WorkOrderQuotationDto(
    int Id,
    int ServiceJobId,
    string QuotationNumber,
    QuotationStatus Status,
    DateTime IssuedDate,
    DateTime? ValidUntil,
    string? Description,
    string? Warranty,
    string? DeliveryTime,
    string? PaymentTerms,
    decimal LaborCost,
    decimal ShippingCost,
    decimal DiscountAmount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal TotalAmount,
    DateTime? SentDate,
    DateTime? AcceptedAt,
    DateTime? RejectedAt,
    string? RejectionReason,
    IReadOnlyList<QuotationItemDto> Items);

public sealed record InstallationMaterialDto(
    int Id,
    int? ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    string? Notes);

public sealed record InstallationTaskDto(
    int Id,
    string Title,
    string? Description,
    bool IsCompleted,
    DateTime? CompletedAt);

public sealed record InstallationOrderDto(
    int Id,
    int ServiceJobId,
    int? QuotationId,
    int? TechnicianId,
    string? TechnicianName,
    DateTime? InstallationDate,
    string? Notes,
    DateTime? CompletedAt,
    string? CompletionTechnician,
    string? DeliveryNote,
    string? CustomerSignature,
    IReadOnlyList<InstallationMaterialDto> Materials,
    IReadOnlyList<InstallationTaskDto> Tasks);

/// <summary>
/// Bir iş emrinin tüm iş akışı verisi (keşif + teklif + montaj). PDF üretimi
/// ve teklif düzenleme ekranı bu aggregate üzerinden çalışır.
/// </summary>
public sealed record WorkOrderWorkflowDto(
    int JobId,
    JobStatus JobStatus,
    DiscoveryReportDto? Discovery,
    WorkOrderQuotationDto? Quotation,
    InstallationOrderDto? Installation);
