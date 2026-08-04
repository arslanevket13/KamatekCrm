using KamatekCrm.Shared.Models.WorkOrders;

namespace KamatekCrm.Shared.Services
{
    /// <summary>
    /// Montaj belgesi PDF üretimi: Montaj İş Emri ve Montaj Tamamlama Formu.
    /// </summary>
    public interface IInstallationPdfService
    {
        void GenerateInstallationOrderPdf(InstallationOrder order, string filePath);

        void GenerateInstallationCompletionFormPdf(InstallationOrder order, string filePath);
    }
}
