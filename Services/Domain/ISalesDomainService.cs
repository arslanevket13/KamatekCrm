using System.Collections.Generic;
using System.Threading.Tasks;
using KamatekCrm.Enums;

namespace KamatekCrm.Services.Domain
{
    /// <summary>
    /// Satış işlemlerini yöneten domain service interface
    /// </summary>
    public interface ISalesDomainService
    {
        /// <summary>
        /// Satış işlemini gerçekleştirir (Transaction içinde)
        /// </summary>
        /// <param name="request">Satış isteği detayları</param>
        /// <returns>Satış sonucu</returns>
        SalesResult ProcessSale(SaleRequest request);

        /// <summary>
        /// Sepet öğelerini stok durumuna göre doğrular
        /// </summary>
        /// <param name="items">Sepet öğeleri</param>
        /// <param name="warehouseId">Depo ID</param>
        /// <param name="allowNegativeStock">Negatif stoğa izin ver</param>
        void ValidateCartItems(IEnumerable<SaleItemRequest> items, int warehouseId, bool allowNegativeStock = true);
    }

    /// <summary>
    /// Satış isteği DTO
    /// </summary>
    public class SaleRequest
    {
        public int WarehouseId { get; set; }
        public string CustomerName { get; set; } = "Perakende Müşteri";
        public PaymentMethod PaymentMethod { get; set; }
        public List<SaleItemRequest> Items { get; set; } = new();
        public string CreatedBy { get; set; } = "Sistem";
    }

    /// <summary>
    /// Satış kalemi isteği DTO
    /// </summary>
    public class SaleItemRequest
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    /// <summary>
    /// Satış sonucu DTO
    /// </summary>
    public class SalesResult
    {
        public bool Success { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public static SalesResult Ok(string orderNumber, decimal totalAmount)
            => new() { Success = true, OrderNumber = orderNumber, TotalAmount = totalAmount };

        public static SalesResult Fail(string error)
            => new() { Success = false, ErrorMessage = error };
    }
}
