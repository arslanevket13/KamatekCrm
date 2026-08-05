using KamatekCrm.Shared.Enums;
// Satır DTO'sundaki QuotationStatus özelliği enum tip adıyla çakıştığı için alias kullanılır.
using QuotationStatusEnum = KamatekCrm.Shared.Enums.QuotationStatus;

namespace KamatekCrm.ApplicationCore.DTOs.ServiceJobs;

public sealed record ServiceJobSearchRequest(
    string? SearchText,
    JobStatus? Status,
    DateTime? StartDate,
    DateTime? EndDate,
    int Take = 50,
    bool IsSlaBreachedOnly = false);

public sealed record ChangeServiceJobStatusCommandParameter(
    ServiceJobRowDto? Job,
    JobStatus Status);

public sealed record ServiceJobCustomerLookupDto(
    int Id,
    string FullName,
    string FullAddress);

public sealed record ServiceJobProductLookupDto(
    int Id,
    string ProductName,
    decimal SalePrice,
    decimal PurchasePrice);

public sealed record ServiceJobTechnicianLookupDto(
    int Id,
    string FullName);

public sealed record ServiceJobAssetLookupDto(
    int Id,
    JobCategory Category,
    string Brand,
    string Model,
    string? SerialNumber,
    string? Location)
{
    public string FullName => $"{Brand} {Model}".Trim();
}

public sealed record ServiceJobProjectLookupDto(int Id, string Name);

public sealed record ServiceJobWorkspaceDto(
    IReadOnlyList<ServiceJobCustomerLookupDto> Customers,
    IReadOnlyList<ServiceJobProductLookupDto> Products,
    IReadOnlyList<ServiceJobTechnicianLookupDto> Technicians);

public sealed class ServiceJobRowDto
{
    public int Id { get; init; }
    public int CustomerId { get; init; }
    public int? CustomerAssetId { get; init; }
    public string CustomerFullName { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public JobStatus Status { get; set; }
    public int? DiscoveryReportId { get; init; }
    public int? QuotationId { get; init; }
    public QuotationStatus? QuotationStatus { get; init; }
    public int? InstallationOrderId { get; init; }
    public bool IsInstallationCompleted { get; init; }

    // ── Sağ tık menüsü aktiflik kuralları (UI ipucu; kesin doğrulama servis katmanındadır) ──
    public bool CanConvertToQuote =>
        QuotationId is null &&
        Status is JobStatus.DiscoveryRequest or JobStatus.Pending
            or JobStatus.PendingDiscovery or JobStatus.DiscoveryCompleted;
    public string ConvertToQuoteDisabledReason => QuotationId is not null
        ? "Bu iş emri zaten teklife dönüştürülmüş."
        : "Teklife dönüştürme yalnızca keşif aşamasındaki işler için geçerlidir.";

    public bool CanEditQuote => QuotationId is not null && QuotationStatus is QuotationStatusEnum.Draft or QuotationStatusEnum.Sent;
    public string EditQuoteDisabledReason => QuotationId is null
        ? "Bu iş emri için teklif oluşturulmamış."
        : "Bu durumdaki teklif düzenlenemez; yalnızca taslak veya gönderilmiş teklifler düzenlenir.";

    public bool CanAcceptQuote => QuotationId is not null && QuotationStatus is QuotationStatusEnum.Draft or QuotationStatusEnum.Sent;
    public string AcceptQuoteDisabledReason => QuotationId is null
        ? "Bu iş emri için teklif oluşturulmamış."
        : "Bu durumdaki teklif kabul edilemez.";

    public bool CanRejectQuote => CanAcceptQuote;
    public string RejectQuoteDisabledReason => AcceptQuoteDisabledReason;

    public bool CanSetInstallationPlanned =>
        QuotationStatus == QuotationStatusEnum.Accepted && InstallationOrderId is null;
    public string SetInstallationPlannedDisabledReason => QuotationStatus != QuotationStatusEnum.Accepted
        ? "Montaj planlamak için teklifin önce 'Kabul Edildi' durumunda olması gerekir."
        : "Bu iş için montaj zaten planlanmış.";

    public bool CanSetInstallationCompleted =>
        InstallationOrderId is not null && !IsInstallationCompleted;
    public string SetInstallationCompletedDisabledReason => InstallationOrderId is null
        ? "Montaj tamamlamak için işin önce 'Montaj Yapılacak' aşamasında olması gerekir."
        : "Bu işin montajı zaten tamamlanmış.";

    public bool CanCancelJob =>
        Status is not (JobStatus.Completed or JobStatus.InstallationCompleted or JobStatus.Delivered or JobStatus.Cancelled);
    public string CancelJobDisabledReason => "Bu durumdaki iş iptal edilemez.";

    public bool CanDeleteJob =>
        Status is not (JobStatus.Completed or JobStatus.InstallationCompleted or JobStatus.Delivered);
    public string DeleteJobDisabledReason => "Tamamlanmış veya stok tüketilmiş iş silinemez.";

    public string StatusDisplay => MapStatusDisplay(Status);

    /// <summary>JobStatus → Türkçe durum etiketi (liste satırı, sağ tık menüsü ve workspace rozeti ortak kullanır).</summary>
    public static string MapStatusDisplay(JobStatus status) => status switch
    {
        JobStatus.DiscoveryRequest => "🔍 Keşif Talebi",
        JobStatus.ConvertedToQuote => "📄 Teklife Dönüştürüldü",
        JobStatus.InstallationPlanned => "🛠️ Montaj Yapılacak",
        JobStatus.InstallationCompleted => "✅ Montaj Tamamlandı",
        JobStatus.Delivered => "🚚 Teslim Edildi",
        JobStatus.Pending => "⏳ Bekliyor",
        JobStatus.InProgress => "🔵 Devam Ediyor",
        JobStatus.WaitingForParts => "📦 Parça Bekleniyor",
        JobStatus.WaitingForApproval => "✋ Onay Bekleniyor",
        JobStatus.Completed => "✅ Tamamlandı",
        JobStatus.Cancelled => "❌ İptal Edildi",
        _ => status.ToString()
    };
    public JobPriority Priority { get; init; }
    public string PriorityDisplay => Priority switch
    {
        JobPriority.Low => "🟢 Düşük",
        JobPriority.Normal => "🔵 Normal",
        JobPriority.Urgent => "🟠 Acil",
        JobPriority.Critical => "🔴 Kritik",
        _ => Priority.ToString()
    };
    public WorkOrderType WorkOrderType { get; init; }
    public string WorkOrderTypeDisplay => WorkOrderType switch
    {
        WorkOrderType.Discovery => "🔍 Keşif",
        WorkOrderType.Repair => "🔧 Tamir",
        WorkOrderType.Installation => "🏗️ Kurulum",
        WorkOrderType.Maintenance => "🛠️ Bakım",
        WorkOrderType.Inspection => "📋 Kontrol",
        WorkOrderType.Replacement => "🔄 Değişim",
        _ => WorkOrderType.ToString()
    };
    public string SlaStatusDisplay
    {
        get
        {
            if (Status == JobStatus.Completed) return "Completed";
            if (Status is JobStatus.Cancelled or JobStatus.Delivered) return "—";
            if (!SlaDeadline.HasValue) return "SLA Yok";

            var remaining = SlaDeadline.Value - DateTime.UtcNow;
            if (remaining.TotalSeconds < 0)
            {
                var overdueHours = Math.Abs((int)remaining.TotalHours);
                if (overdueHours < 24)
                    return $"{overdueHours}s gecikti";
                var overdueDays = Math.Abs((int)remaining.TotalDays);
                return $"{overdueDays}d gecikti";
            }
            if (remaining.TotalHours <= 2)
            {
                return $"{(int)remaining.TotalMinutes} dk kaldı";
            }
            if (remaining.TotalHours <= 24)
            {
                return $"{(int)remaining.TotalHours} sa kaldı";
            }
            return $"{SlaDeadline.Value:dd.MM.yyyy}";
        }
    }
    public JobCategory JobCategory { get; init; }
    public string CategoriesJson { get; init; } = "[]";
    public DateTime CreatedDate { get; init; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? ScheduledDate { get; init; }
    public int? AssignedTechnicianId { get; init; }
    public string? AssignedTechnician { get; init; }
    public decimal LaborCost { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public int? EstimatedDuration { get; init; }
    public DateTime? SlaDeadline { get; init; }
    public string? TechnicianNotes { get; init; }
    public string? PhotoPathsJson { get; init; }
    public IReadOnlyList<string> PhotoPathsList => ParsePhotoPaths(PhotoPathsJson);

    private static IReadOnlyList<string> ParsePhotoPaths(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(value) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }
}

public sealed class ServiceJobMaterialDto
{
    public ServiceJobMaterialDto(
        int id,
        int productId,
        string productName,
        int quantityUsed,
        decimal unitPrice,
        decimal unitCost)
    {
        Id = id;
        ProductId = productId;
        ProductName = productName;
        QuantityUsed = quantityUsed;
        UnitPrice = unitPrice;
        UnitCost = unitCost;
    }

    public int Id { get; }
    public int ProductId { get; }
    public string ProductName { get; }
    public int QuantityUsed { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
}

public sealed record ServiceJobHistoryDto(
    int Id,
    DateTime Date,
    JobStatus? JobStatusChange,
    string TechnicianNote,
    string Action,
    string? Notes,
    string? UserId);

public sealed record ServiceJobDashboardDto(
    int TotalJobCount,
    int PendingCount,
    int InProgressCount,
    int CompletedCount,
    int SlaBreachedCount,
    int TodayCreatedCount,
    double AvgCompletionHours);

public sealed record ServiceJobDocumentDto(
    int Id,
    WorkOrderType WorkOrderType,
    string Description,
    string? DiscoveryTechnicalNotes,
    string? TechnicianNotes,
    string? AssignedTechnician,
    JobPriority Priority,
    DateTime? ScheduledDate,
    int CustomerId,
    string CustomerName,
    string CustomerCompanyName,
    string CustomerPhone,
    string CustomerAddress);
