using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.Interfaces;

/// <summary>
/// İş emri çalışma alanının iş akışı kararlarını (aşama, sıradaki işlem, izinli işlemler,
/// uyarılar) tek yerde üretir. UI bu kararları kendi başına türetmez; application
/// katmanından gelen <see cref="WorkOrderWorkspaceDto"/> üzerinden görüntüler.
/// </summary>
public interface IWorkOrderNextActionResolver
{
    /// <summary>İşin güncel ana aşamasını (Talep → Keşif → Teklif → Montaj → Teslim → Kapandı) çözer.</summary>
    WorkOrderStage ResolveStage(WorkOrderWorkspaceInput input);

    /// <summary>Kullanıcının yapması gereken tek önerilen işlemi üretir.</summary>
    WorkOrderNextActionInfo ResolveNextAction(WorkOrderWorkspaceInput input);

    /// <summary>Mevcut durumda sunulabilecek işlemlerin tam listesini (aktiflik + gerekçe ile) üretir.</summary>
    IReadOnlyList<WorkOrderActionInfo> ResolveAllowedActions(WorkOrderWorkspaceInput input);

    /// <summary>Eksik bilgi ve dikkat uyarılarını üretir (boş durumlar için kontrol listesi).</summary>
    IReadOnlyList<WorkOrderWarning> ResolveWarnings(WorkOrderWorkspaceInput input);
}
