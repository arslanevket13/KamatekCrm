using KamatekCrm.Shared.Models.WorkOrders;

namespace KamatekCrm.Shared.Services
{
    /// <summary>
    /// Fiyat Teklifi PDF üretimi. Keşif raporundan bağımsız; teklif kalemleri,
    /// iskonto, KDV, işçilik, nakliye ve şartlar <see cref="WorkOrderQuotation"/> altındadır.
    /// </summary>
    public interface IQuotationPdfService
    {
        void GenerateWorkOrderQuotationPdf(WorkOrderQuotation quotation, string filePath);
    }
}
