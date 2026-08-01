using KamatekCrm.Shared.Models;

namespace KamatekCrm.Shared.Services
{
    public interface IPurchaseOrderPdfService
    {
        void GeneratePurchaseOrder(PurchaseInvoice invoice, string filePath);
    }
}
