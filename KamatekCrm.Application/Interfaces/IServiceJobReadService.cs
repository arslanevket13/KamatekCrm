using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IServiceJobReadService
{
    Task<Result<ServiceJobWorkspaceDto>> GetWorkspaceAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ServiceJobRowDto>>> SearchAsync(ServiceJobSearchRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ServiceJobAssetLookupDto>>> GetCustomerAssetsAsync(int customerId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ServiceJobProjectLookupDto>>> GetCustomerProjectsAsync(int customerId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ServiceJobMaterialDto>>> GetMaterialsAsync(int jobId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ServiceJobHistoryDto>>> GetHistoryAsync(int jobId, CancellationToken cancellationToken = default);
    Task<Result<ServiceJobDashboardDto>> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<Result<ServiceJobDocumentDto>> GetDocumentAsync(int jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bir iş emrinin tüm iş akışı verisini (keşif raporu + teklif + montaj emri) döndürür.
    /// PDF üretimi ve teklif düzenleme ekranı bu veriyle çalışır.
    /// </summary>
    Task<Result<WorkOrderWorkflowDto>> GetWorkOrderWorkflowAsync(int jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// İş Emri Çalışma Alanı merkezi projeksiyonunu döndürür: iş künyesi + alt kayıt özetleri
    /// (keşif/teklif/montaj/teslim) + son hareketler + application katmanında çözümlenmiş
    /// CurrentStage, NextAction, AllowedActions ve Warnings. UI bu DTO'yu yalnızca görüntüler.
    /// </summary>
    Task<Result<WorkOrderWorkspaceDto>> GetWorkspaceAsync(int jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Teklif düzenleyicide stoktan ürün eklemek için ürün arar (ad veya SKU ile).
    /// </summary>
    Task<Result<IReadOnlyList<QuotationProductLookupDto>>> SearchProductsAsync(
        string searchText,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bir iş emrinin tüm teklif revizyonlarını en yeni üstte olacak şekilde döndürür.
    /// IsCurrent bayrağı iş emrinin halen bağlı olduğu teklifi işaretler.
    /// </summary>
    Task<Result<IReadOnlyList<QuotationRevisionSummaryDto>>> GetQuotationRevisionsAsync(
        int jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirli bir teklif revizyonunu kalemleriyle birlikte döndürür (geçmiş görüntüleme için).
    /// </summary>
    Task<Result<WorkOrderQuotationDto>> GetQuotationByIdAsync(
        int quotationId,
        CancellationToken cancellationToken = default);
}
