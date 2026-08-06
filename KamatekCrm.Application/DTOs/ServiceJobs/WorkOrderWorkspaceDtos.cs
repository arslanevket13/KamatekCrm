using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.DTOs.ServiceJobs;

// ───────────────────────────── NextAction / AllowedActions ─────────────────────────────

/// <summary>
/// Sıradaki işlem ve izinli işlemlerin çözümlenmesi için gereken saf (veritabanı bağımsız)
/// olgu kümesi. Okuma servisi bu kaydı EF projeksiyonundan üretir; çözümleyici tamamen
/// birim test edilebilir ve UI tarafından doğrudan türetilmez.
/// </summary>
public sealed record WorkOrderWorkspaceInput(
    int JobId,
    JobStatus JobStatus,
    int? AssignedUserId,
    string? AssignedTechnicianName,
    DateTime? SlaDeadline,
    bool HasDiscoveryReport,
    string? DiscoveryTechnicianName,
    string? DiscoveryTechnicalNotes,
    string? DiscoveryRecommendedSolution,
    int DiscoveryMaterialCount,
    int DiscoveryVisitCount,
    DateTime? DiscoveryAppointmentDate,
    QuotationStatus? QuotationStatus,
    int QuotationRevisionNumber,
    bool HasInstallation,
    DateTime? InstallationDate,
    bool InstallationCompleted,
    bool HasDelivery,
    DateTime? DeliveryDate,
    // Montaj tamamlama ön koşulları: servis (CompleteInstallationAsync) ile birebir aynı kurallar.
    int InstallationMaterialCount = 0,
    decimal InstallationLaborHours = 0m);

/// <summary>Çalışma alanında sunulan tek bir işlem (AllowedActions üyesi).</summary>
public sealed record WorkOrderActionInfo(
    WorkOrderAction Action,
    string Title,
    string Description,
    string PrimaryButtonText,
    bool IsEnabled,
    string DisabledReason,
    WorkOrderSeverity Severity,
    DateTime? DueDate);

/// <summary>
/// Çalışma alanının "sıradaki işlem" paneli. Action null ise ilerletilecek bir işlem
/// yoktur (terminal veya iptal durumu) — panel bilgi amaçlıdır.
/// </summary>
public sealed record WorkOrderNextActionInfo(
    WorkOrderAction? Action,
    string Title,
    string Description,
    string? PrimaryButtonText,
    bool IsEnabled,
    string DisabledReason,
    WorkOrderSeverity Severity,
    DateTime? DueDate);

/// <summary>Çalışma alanının "eksik bilgiler / dikkat" listesindeki tek uyarı.</summary>
public sealed record WorkOrderWarning(
    string Code,
    string Message,
    WorkOrderSeverity Severity);

public static class WorkOrderStageLabels
{
    public static string Map(WorkOrderStage stage) => stage switch
    {
        WorkOrderStage.Pending => "📥 Talep",
        WorkOrderStage.Discovery => "🔍 Keşif",
        WorkOrderStage.Quotation => "📄 Teklif",
        WorkOrderStage.Installation => "🛠️ Montaj",
        WorkOrderStage.Delivery => "🚚 Teslim",
        WorkOrderStage.Closed => "✅ Kapandı",
        WorkOrderStage.Cancelled => "❌ İptal Edildi",
        _ => stage.ToString()
    };
}

// ───────────────────────────── Workspace DTO ─────────────────────────────

/// <summary>
/// İş Emri Çalışma Alanı merkezi projeksiyonu. Aşama, sıradaki işlem, izinli işlemler ve
/// uyarılar application katmanında çözümlenir; UI bu DTO'yu yalnızca görüntüler.
/// Alt kayıt özetleri (keşif/teklif/montaj/teslim) mevcut aggregate DTO'ları yeniden kullanır.
/// </summary>
public sealed record WorkOrderWorkspaceDto(
    int JobId,
    string WorkOrderNumber,
    string CustomerName,
    string CustomerPhone,
    string? CustomerAddress,
    string JobTitle,
    string Description,
    WorkOrderType WorkOrderType,
    JobPriority Priority,
    int? AssignedUserId,
    string? AssignedUserName,
    int? AssignedTechnicianId,
    string? AssignedTechnicianName,
    WorkOrderStage CurrentStage,
    string CurrentStageDisplay,
    JobStatus JobStatus,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    DateTime? TargetDate,
    DateTime? DiscoveryAppointmentDate,
    DateTime? InstallationDate,
    DateTime? SlaDeadline,
    string SlaStatus,
    WorkOrderNextActionInfo NextAction,
    IReadOnlyList<WorkOrderActionInfo> AllowedActions,
    IReadOnlyList<WorkOrderWarning> Warnings,
    DiscoveryReportDto? DiscoverySummary,
    WorkOrderQuotationDto? QuotationSummary,
    InstallationOrderDto? InstallationSummary,
    JobDeliveryDto? DeliverySummary,
    IReadOnlyList<ServiceJobHistoryDto> RecentActivities,
    IReadOnlyList<DiscoveryVisitDto>? Visits = null);
