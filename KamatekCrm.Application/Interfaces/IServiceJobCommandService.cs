using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IServiceJobCommandService
{
    Task<Result<ServiceJobSaveResult>> SaveAsync(
        ServiceJobSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ServiceJobStatusChangeResult>> ChangeStatusAsync(
        int jobId,
        JobStatus requestedStatus,
        string changedBy,
        CancellationToken cancellationToken = default);

    Task<Result<ServiceJobStatusChangeResult>> CompleteAsync(
        int jobId,
        decimal? laborCost,
        decimal? discountAmount,
        string? completionNote,
        string changedBy,
        CancellationToken cancellationToken = default);

    Task<Result<ServiceJobQuoteConversionResult>> ConvertToQuoteAsync(
        int jobId,
        string changedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Keşif kaydını günceller veya oluşturur: teknik rapor, önerilen çözüm, tahmini
    /// işçilik, teknisyen, tahmini malzemeler, ziyaretler ve fotoğraflar tek transaction'da
    /// kaydedilir. Keşif aşamasında (dönüştürmeden önce) çağrılır.
    /// </summary>
    Task<Result<DiscoverySaveResult>> SaveDiscoveryAsync(
        SaveDiscoveryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Keşfi tamamlar: keşif raporunun yeterli olduğunu doğrular (teknik notlar ve en az
    /// bir malzeme veya ziyaret) ve iş emrini DiscoveryCompleted durumuna alır.
    /// Yalnızca keşif aşamalarından geçerlidir.
    /// </summary>
    Task<Result<ServiceJobStatusChangeResult>> CompleteDiscoveryAsync(
        int jobId,
        string changedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Teklif kalemlerini ve şartlarını (malzeme, miktar, birim fiyat, iskonto, KDV,
    /// işçilik, nakliye, açıklama, garanti, teslim süresi, ödeme şartları) günceller.
    /// </summary>
    Task<Result<WorkOrderQuotationResult>> UpdateQuotationAsync(
        UpdateWorkOrderQuotationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Teklifi müşteriye gönderilmiş olarak işaretler (Draft → Sent): gönderim zamanı
    /// saklanır ve iş tarihçesine yazılır. Zaten gönderilmiş teklifte idempotent başarı döner;
    /// kabul/red/iptal edilmiş teklif gönderilemez.
    /// </summary>
    Task<Result<WorkOrderQuotationResult>> SendQuotationAsync(
        int quotationId,
        string changedBy,
        CancellationToken cancellationToken = default);

    Task<Result<WorkOrderQuotationResult>> AcceptQuotationAsync(
        int quotationId,
        string changedBy,
        CancellationToken cancellationToken = default);

    Task<Result<WorkOrderQuotationResult>> RejectQuotationAsync(
        int quotationId,
        string? reason,
        string changedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Teklifin yeni bir revizyonunu oluşturur: başlık ve kalemler kopyalanır,
    /// revizyon Taslak durumunda başlar. Kabul edilmiş/reddedilmiş teklifler
    /// doğrudan düzenlenemez; değişiklik için revizyon oluşturulmalıdır.
    /// </summary>
    Task<Result<QuotationRevisionResult>> CreateRevisionAsync(
        int quotationId,
        string changedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Montajı planlar: iş emri montaj aşamasına alınır, teklif kalemleri
    /// montaj malzemelerine kopyalanır. Yalnızca kabul edilmiş teklifler için çalışır.
    /// </summary>
    Task<Result<ServiceJobStatusChangeResult>> PlanInstallationAsync(
        PlanInstallationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Montaj emrini günceller: başlık bilgileri (teknisyen, tarih, not, işçilik saati),
    /// malzemeler ve görevler diff tabanlı tek transaction'da kaydedilir. ProductId'li
    /// malzemeler stok rezervasyonuyla senkronize edilir (çekme akışı montaj tamamlanınca
    /// stoktan düşer). Montaj planlanmadan önce çağrılamaz.
    /// </summary>
    Task<Result<InstallationSaveResult>> SaveInstallationAsync(
        SaveInstallationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Montajı tamamlar: tamamlanma tarihi, teknisyen, teslim notu, işçilik saati ve müşteri
    /// imzası saklanır; stok tüketimi ve müşteri aktivitesi burada yürütülür. Doğrulama:
    /// montaj emri mevcut olmalı, en az bir malzeme girilmiş olmalı ve işçilik saati &gt; 0 olmalı.
    /// </summary>
    Task<Result<ServiceJobStatusChangeResult>> CompleteInstallationAsync(
        CompleteInstallationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// İşi teslim eder (Paket 7): teslim tarihi, teslim eden, teslim notu, müşteri imzası ve
    /// ödeme bilgileri (durum, yöntem, tahsilat, fatura no) kaydedilir; iş Delivered durumuna
    /// alınır. Doğrulama: durum geçişi (InstallationCompleted → Delivered) ve ödeme tutarlılığı
    /// (kısmi/ödenmiş durumda tahsilat tutarı &gt; 0, ödenmemiş durumda 0 olmalı).
    /// İş zaten teslim edilmişse yalnızca teslim/ödeme kaydı güncellenir.
    /// </summary>
    Task<Result<ServiceJobStatusChangeResult>> CompleteDeliveryAsync(
        CompleteDeliveryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ServiceJobDeleteResult>> DeleteAsync(
        int jobId,
        string changedBy,
        CancellationToken cancellationToken = default);
}
