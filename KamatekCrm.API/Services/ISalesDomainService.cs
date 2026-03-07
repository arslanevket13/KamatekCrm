using System.Collections.Generic;
using System.Threading.Tasks;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.API.Services
{
    public interface ISalesDomainService
    {
        Task<SaleResult> ProcessSaleAsync(SaleRequest request);
        void ValidateCartItems(IEnumerable<SaleItemRequest> items, int warehouseId, bool allowNegativeStock = true);
        List<CustomerPurchaseHistory> GetCustomerPurchaseHistory(int customerId);
        CustomerStatistics GetCustomerStatistics(int customerId);
        DailyPosReport GetDailyPosReport(System.DateTime date);
        bool ReprintReceipt(int transactionId);
    }
}
