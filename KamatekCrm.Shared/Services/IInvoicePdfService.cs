using KamatekCrm.Shared.Models;

namespace KamatekCrm.Shared.Services
{
    public interface IInvoicePdfService
    {
        void GenerateInvoice(SalesOrder order, string filePath);
    }
}
