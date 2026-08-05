using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.DTOs.ServiceJobs;

// ───────────────────────────── Komut DTO'ları ─────────────────────────────

/// <summary>
/// Teklif düzenleyicide "Stoktan Ekle" ürün seçici için ürün arama sonucu.
/// </summary>
public sealed record QuotationProductLookupDto(
    int Id,
    string ProductName,
    string Sku,
    string Unit,
    decimal SalePrice,
    int StockQuantity);

public sealed record QuotationItemInput(
    int? Id,
    int? ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxPercent,
    int Sequence = 0);

public sealed record QuotationRevisionResult(
    int NewQuotationId,
    int RevisionNumber);

/// <summary>QuotationStatus → Türkçe etiket (editör, workspace ve revizyon geçmişi ortak kullanır).</summary>
public static class QuotationStatusLabels
{
    public static string Map(QuotationStatus status) => status switch
    {
        QuotationStatus.Draft => "📝 Taslak",
        QuotationStatus.Sent => "✉️ Gönderildi",
        QuotationStatus.Accepted => "✅ Kabul Edildi",
        QuotationStatus.Rejected => "❌ Reddedildi",
        QuotationStatus.Cancelled => "🚫 İptal Edildi",
        QuotationStatus.Expired => "⏳ Süresi Doldu",
        _ => status.ToString()
    };
}

/// <summary>
/// Teklif düzenleyicideki "Revizyon Geçmişi" listesi için özet satır.
/// IsCurrent, iş emrinin halen bağlı olduğu (en güncel) teklifi işaretler.
/// </summary>
public sealed record QuotationRevisionSummaryDto(
    int Id,
    int RevisionNumber,
    QuotationStatus Status,
    decimal TotalAmount,
    DateTime IssuedDate,
    DateTime? SentDate,
    DateTime? AcceptedAt,
    DateTime? RejectedAt,
    bool IsCurrent)
{
    public string StatusDisplay => QuotationStatusLabels.Map(Status);
}

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

/// <summary>Montaj malzemesi girişi — ID korumalı diff güncellemesi için kaynak kimliği taşır.</summary>
public sealed record InstallationMaterialInput(
    int? Id,
    int? ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    string? Notes);

/// <summary>Montaj görev listesi girişi — tamamlanma durumuyla birlikte diff güncellenir.</summary>
public sealed record InstallationTaskInput(
    int? Id,
    string Title,
    string? Description,
    bool IsCompleted);

/// <summary>
/// Montaj emrini günceller: başlık bilgileri (teknisyen, tarih, notlar, işçilik saati),
/// malzemeler ve görevler tek transaction'da diff tabanlı kaydedilir. Stok rezervasyonları
/// montaj malzemeleri (ProductId'li) üzerinden senkronize edilir.
/// </summary>
public sealed record SaveInstallationRequest(
    int JobId,
    int? TechnicianId,
    string? TechnicianName,
    DateTime? InstallationDate,
    string? Notes,
    decimal LaborHours,
    IReadOnlyCollection<InstallationMaterialInput> Materials,
    IReadOnlyCollection<InstallationTaskInput> Tasks,
    string ChangedBy);

public sealed record InstallationSaveResult(int InstallationId);

public sealed record CompleteInstallationRequest(
    int JobId,
    string? DeliveryNote,
    string? CompletionTechnician,
    string? CustomerSignature,
    decimal LaborHours = 0m,
    string ChangedBy = "Sistem");

/// <summary>
/// İşi teslim eder (Paket 7). Teslim tarihi, teslim eden, teslim notu, müşteri imzası
/// ve ödeme bilgileri tek transaction'da kaydedilir; iş Delivered durumuna alınır.
/// Doğrulama: durum geçişi (InstallationCompleted → Delivered) ve ödeme tutarlılığı.
/// </summary>
public sealed record CompleteDeliveryRequest(
    int JobId,
    string? DeliveredBy,
    string? DeliveryNote,
    string? CustomerSignature,
    KamatekCrm.Shared.Enums.PaymentStatus PaymentStatus,
    KamatekCrm.Shared.Enums.PaymentMethod PaymentMethod,
    decimal PaidAmount,
    string? InvoiceNumber,
    string ChangedBy);

// ───────────────────────────── Okuma DTO'ları ─────────────────────────────

public sealed record DiscoveryMaterialDto(
    int Id,
    int? ProductId,
    string ProductName,
    int Quantity,
    string? Notes);

public sealed record DiscoveryVisitDto(
    int Id,
    DateTime VisitDate,
    string? TechnicianName,
    string? Notes,
    IReadOnlyList<string> PhotoPaths);

public sealed record DiscoveryVisitInput(
    int? Id,
    DateTime VisitDate,
    string? TechnicianName,
    string? Notes,
    IReadOnlyList<string> PhotoPaths);

public sealed record DiscoveryMaterialInput(
    int? Id,
    int? ProductId,
    string ProductName,
    int Quantity,
    string? Notes);

public sealed record SaveDiscoveryRequest(
    int JobId,
    string? TechnicalNotes,
    string? RecommendedSolution,
    double EstimatedLaborHours,
    string? TechnicianName,
    IReadOnlyList<string> PhotoPaths,
    IReadOnlyCollection<DiscoveryMaterialInput> Materials,
    IReadOnlyCollection<DiscoveryVisitInput> Visits,
    string ChangedBy);

public sealed record DiscoverySaveResult(int ReportId);

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
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxPercent,
    decimal LineTotal,
    int Sequence = 0);

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
    IReadOnlyList<QuotationItemDto> Items,
    int RevisionNumber = 0,
    int? ParentQuotationId = null);

public sealed record InstallationMaterialDto(
    int Id,
    int? ProductId,
    string ProductName,
    decimal Quantity,
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
    decimal LaborHours,
    DateTime? CompletedAt,
    string? CompletionTechnician,
    string? DeliveryNote,
    string? CustomerSignature,
    IReadOnlyList<InstallationMaterialDto> Materials,
    IReadOnlyList<InstallationTaskDto> Tasks);

/// <summary>
/// Bir iş emrinin tüm iş akışı verisi (keşif + teklif + montaj + teslim). PDF üretimi
/// ve teklif düzenleme ekranı bu aggregate üzerinden çalışır.
/// </summary>
public sealed record WorkOrderWorkflowDto(
    int JobId,
    JobStatus JobStatus,
    DiscoveryReportDto? Discovery,
    WorkOrderQuotationDto? Quotation,
    InstallationOrderDto? Installation,
    IReadOnlyList<DiscoveryVisitDto>? Visits = null,
    JobDeliveryDto? Delivery = null);

public sealed record JobDeliveryDto(
    int Id,
    int ServiceJobId,
    DateTime DeliveryDate,
    string? DeliveredBy,
    string? DeliveryNote,
    string? CustomerSignature,
    KamatekCrm.Shared.Enums.PaymentStatus PaymentStatus,
    KamatekCrm.Shared.Enums.PaymentMethod PaymentMethod,
    decimal PaidAmount,
    string? InvoiceNumber);

/// <summary>PaymentStatus → Türkçe etiket (editör ve workspace ortak kullanır).</summary>
public static class PaymentStatusLabels
{
    public static string Map(KamatekCrm.Shared.Enums.PaymentStatus status) => status switch
    {
        KamatekCrm.Shared.Enums.PaymentStatus.Unpaid => "Tahsilat Bekleniyor",
        KamatekCrm.Shared.Enums.PaymentStatus.Partial => "Kısmi Ödendi",
        KamatekCrm.Shared.Enums.PaymentStatus.Paid => "Ödendi",
        _ => status.ToString()
    };
}

/// <summary>PaymentMethod → Türkçe etiket (editör ve workspace ortak kullanır).</summary>
public static class PaymentMethodLabels
{
    public static string Map(KamatekCrm.Shared.Enums.PaymentMethod method) => method switch
    {
        KamatekCrm.Shared.Enums.PaymentMethod.Cash => "Nakit",
        KamatekCrm.Shared.Enums.PaymentMethod.CreditCard => "Kredi Kartı",
        KamatekCrm.Shared.Enums.PaymentMethod.BankTransfer => "Havale/EFT",
        KamatekCrm.Shared.Enums.PaymentMethod.MobilePayment => "Mobil Ödeme",
        KamatekCrm.Shared.Enums.PaymentMethod.Check => "Çek",
        KamatekCrm.Shared.Enums.PaymentMethod.OnAccount => "Cari Hesap",
        _ => method.ToString()
    };
}
