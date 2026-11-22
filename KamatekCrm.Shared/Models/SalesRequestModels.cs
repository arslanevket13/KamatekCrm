using System;
using System.Collections.Generic;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
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
    public class SaleResult
    {
        public bool Success { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public static SaleResult Ok(string orderNumber, decimal totalAmount)
            => new() { Success = true, OrderNumber = orderNumber, TotalAmount = totalAmount };

        public static SaleResult Fail(string error)
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
