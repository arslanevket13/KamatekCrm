using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IWorkOrderIntelligenceService
{
    /// <summary>
    /// Müşterinin geçmiş iş emirlerini, açık bakiyesini ve aynı kategoride
    /// son 90 gün içinde servis/montaj yapılmışsa tekrar/garanti uyarısını döndürür.
    /// </summary>
    Task<Result<WorkOrderCustomerMemoryDto>> GetCustomerMemoryAsync(
        int customerId,
        IEnumerable<string> categories,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// İş emri için akıllı radar uyarı rozetlerini (Teknisyen atanmadı, 48s teklif bekliyor,
    /// 72s yanıt yok, Tahsilat bekliyor, SLA riskli, Takip zamanı geldi) değerlendirir.
    /// </summary>
    IReadOnlyList<WorkOrderAlertBadgeDto> EvaluateJobAlerts(ServiceJob job, DateTime? utcNow = null);

    /// <summary>
    /// Satır projeksiyonu (Kanban/Liste kartları) için akıllı radar uyarı rozetlerini değerlendirir.
    /// </summary>
    IReadOnlyList<WorkOrderAlertBadgeDto> EvaluateJobAlerts(ServiceJobRowDto row, DateTime? utcNow = null);

    /// <summary>
    /// İş emrinin canlı malzeme maliyeti, teklif tutarı, tahmini kâr, kâr marjı ve tahsilat özetini hesaplar.
    /// </summary>
    Task<Result<WorkOrderProfitabilityDto>> CalculateProfitabilityAsync(
        int serviceJobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verilen ürün ID'leri için rezervasyon düşülmüş canlı kullanılabilir stok haritasını döndürür.
    /// </summary>
    Task<Result<IReadOnlyDictionary<int, int>>> GetStockAvailabilityMapAsync(
        IEnumerable<int> productIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kategoriye özel standart keşif kontrol listesi şablonunu döndürür.
    /// </summary>
    IReadOnlyList<DiscoveryChecklistItem> GetCategoryDiscoveryTemplates(string category);

    /// <summary>
    /// Mevcut stokla eşleştirilmiş hazır hızlı malzeme paketlerini döndürür.
    /// </summary>
    Task<Result<IReadOnlyList<QuickMaterialKitDto>>> GetQuickMaterialKitsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// İş emri için paylaşılabilir temiz Türkçe WhatsApp durum/randevu mesajı üretir.
    /// </summary>
    string BuildWhatsAppSummaryText(ServiceJob job);

    /// <summary>
    /// Telefon numarası ve mesajdan tek tıkla açılabilir wa.me bağlantısı üretir.
    /// </summary>
    string BuildWhatsAppUrl(string? phoneNumber, string message);
}
