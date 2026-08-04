using KamatekCrm.Shared.Models.WorkOrders;

namespace KamatekCrm.Shared.Services
{
    /// <summary>
    /// Keşif Raporu PDF üretimi. Rapor fiyat içermez; keşif verileri
    /// <see cref="DiscoveryReport"/> altında saklanır.
    /// </summary>
    public interface IDiscoveryPdfService
    {
        void GenerateDiscoveryReportPdf(DiscoveryReport report, string filePath);
    }
}
