using KamatekCrm.Shared.Models;

namespace KamatekCrm.Shared.Services
{
    public interface IServiceReportPdfService
    {
        void GenerateServiceForm(ServiceJob job, string filePath);
    }
}
