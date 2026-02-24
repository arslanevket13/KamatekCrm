using System.Collections.Generic;
using System.Threading.Tasks;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

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

        /// <summary>
        /// Müşterinin alışveriş geçmişini getirir
        /// </summary>
        List<CustomerPurchaseHistory> GetCustomerPurchaseHistory(int customerId);

        /// <summary>
        /// Müşteri istatistiklerini getirir
        /// </summary>
        CustomerStatistics GetCustomerStatistics(int customerId);

        /// <summary>
        /// Günlük POS raporu getirir
        /// </summary>
        DailyPosReport GetDailyPosReport(DateTime date);

        /// <summary>
        /// Fiş tekrar yazdır
        /// </summary>
        bool ReprintReceipt(int transactionId);
    }

    /// <summary>
    /// Satış isteği DTO
    /// </summary>
    public class SaleRequest
    {
        public int WarehouseId { get; set; }
        public int? CustomerId { get; set; }
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
        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public int TaxRate { get; set; }
        public decimal LineTotal { get; set; }
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

    /// <summary>
    /// Müşteri alışveriş geçmişi DTO
    /// </summary>
    public class CustomerPurchaseHistory
    {
        public int TransactionId { get; set; }
        public DateTime Date { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public int ItemCount { get; set; }
    }

    /// <summary>
    /// Müşteri istatistikleri DTO
    /// </summary>
    public class CustomerStatistics
    {
        public int TotalPurchases { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal AveragePurchase { get; set; }
        public DateTime? LastPurchaseDate { get; set; }
        public int LoyaltyPoints { get; set; }
        public string LoyaltyLevel { get; set; } = "Bronze";
        public int DaysSinceLastPurchase { get; set; }
    }

    /// <summary>
    /// Günlük POS raporu DTO
    /// </summary>
    public class DailyPosReport
    {
        public DateTime Date { get; set; }
        public int TotalTransactions { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCash { get; set; }
        public decimal TotalCard { get; set; }
        public decimal TotalDiscount { get; set; }
        public int TotalItemsSold { get; set; }
        public int WalkInCustomerCount { get; set; }
        public int RegisteredCustomerCount { get; set; }
        public List<TopSellingProduct> TopProducts { get; set; } = new();
    }

    /// <summary>
    /// En çok satan ürün DTO
    /// </summary>
    public class TopSellingProduct
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
    }
}
