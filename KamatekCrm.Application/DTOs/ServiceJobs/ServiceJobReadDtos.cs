using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.DTOs.ServiceJobs;

public sealed record ServiceJobSearchRequest(
    string? SearchText,
    JobStatus? Status,
    DateTime? StartDate,
    DateTime? EndDate,
    int Take = 50);

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
    public string StatusDisplay => Status switch
    {
        JobStatus.Pending => "⏳ Bekliyor",
        JobStatus.InProgress => "🔵 Devam Ediyor",
        JobStatus.WaitingForParts => "📦 Parça Bekleniyor",
        JobStatus.WaitingForApproval => "✋ Onay Bekleniyor",
        JobStatus.Completed => "✅ Tamamlandı",
        JobStatus.Cancelled => "❌ İptal",
        _ => Status.ToString()
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
            if (Status == JobStatus.Cancelled) return "—";
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
