using System.Collections.Generic;
using System.Threading.Tasks;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Services
{
    public interface IDirectSalesService
    {
        Task<SalesOrder> ProcessSaleAsync(
            int? customerId,
            string customerName,
            int warehouseId,
            IEnumerable<PosCartItem> cartItems,
            IEnumerable<PosPaymentEntry> payments,
            string? notes,
            string? currentUserName,
            string idempotencyKey);
    }
}
