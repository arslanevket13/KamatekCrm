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
    /// Teklif kalemlerini ve şartlarını (malzeme, miktar, birim fiyat, iskonto, KDV,
    /// işçilik, nakliye, açıklama, garanti, teslim süresi, ödeme şartları) günceller.
    /// </summary>
    Task<Result<WorkOrderQuotationResult>> UpdateQuotationAsync(
        UpdateWorkOrderQuotationRequest request,
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
    /// Montajı planlar: iş emri montaj aşamasına alınır, teklif kalemleri
    /// montaj malzemelerine kopyalanır. Yalnızca kabul edilmiş teklifler için çalışır.
    /// </summary>
    Task<Result<ServiceJobStatusChangeResult>> PlanInstallationAsync(
        PlanInstallationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Montajı tamamlar: tamamlanma tarihi, teknisyen, teslim notu ve müşteri imzası
    /// saklanır; stok tüketimi ve müşteri aktivitesi burada yürütülür.
    /// </summary>
    Task<Result<ServiceJobStatusChangeResult>> CompleteInstallationAsync(
        CompleteInstallationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ServiceJobDeleteResult>> DeleteAsync(
        int jobId,
        string changedBy,
        CancellationToken cancellationToken = default);
}
