using System.Threading.Tasks;
using KamatekCrm.Shared.Models;
using KamatekCrm.ApplicationCore.DTOs.Transactions;

namespace KamatekCrm.Services
{
    public interface IThermalReceiptPrintService
    {
        /// <summary>
        /// Prints a thermal receipt (80mm/58mm) for the specified SalesOrder
        /// </summary>
        Task PrintReceiptAsync(SalesOrder salesOrder, string? printerName = null);
        Task PrintSalesReturnReceiptAsync(SalesReturnReceiptDto salesReturn, string? printerName = null);

        /// <summary>
        /// Generates a formatted text receipt representation
        /// </summary>
        string FormatReceiptText(SalesOrder salesOrder);

        /// <summary>
        /// Generates encoded bytes for raw/thermal printer output with Turkish encoding support
        /// </summary>
        byte[] GetReceiptBytes(SalesOrder salesOrder, System.Text.Encoding? encoding = null);

        /// <summary>
        /// Prints a thermal ticket for a ServiceJob (Cihaz Kabul Fişi)
        /// </summary>
        Task PrintServiceJobTicketAsync(ServiceJob serviceJob, string? printerName = null);
    }
}
