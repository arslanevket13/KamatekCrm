using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Data;
using Microsoft.Extensions.Logging;

namespace KamatekCrm.API.Services
{
    public class SalesDomainService : ISalesDomainService
    {
        private static readonly SemaphoreSlim _salesLock = new(1, 1);
        private readonly AppDbContext _context;
        private readonly ILogger<SalesDomainService> _logger;

        public SalesDomainService(AppDbContext context, ILogger<SalesDomainService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SaleResult> ProcessSaleAsync(SaleRequest request)
        {
            if (request.Items.Count == 0)
                return SaleResult.Fail("Sepet boş. Satış yapılamaz.");

            if (request.WarehouseId <= 0)
                return SaleResult.Fail("Geçerli bir depo seçilmedi.");

            await _salesLock.WaitAsync();
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var todayOrders = await _context.SalesOrders.CountAsync(o => o.Date.Date == DateTime.UtcNow.Date);
                    var orderNumber = $"SO-{DateTime.UtcNow:yyyyMMdd}-{(todayOrders + 1):D3}";

                    var subTotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);
                    var discountTotal = request.Items.Sum(i => i.DiscountAmount);
                    var taxTotal = request.Items.Sum(i =>
                    {
                        var afterDiscount = (i.Quantity * i.UnitPrice) - i.DiscountAmount;
                        return afterDiscount * i.TaxRate / 100m;
                    });
                    var totalAmount = request.Items.Sum(i => i.LineTotal > 0 ? i.LineTotal : (i.Quantity * i.UnitPrice));

                    var salesOrder = new SalesOrder
                    {
                        OrderNumber = orderNumber,
                        Date = DateTime.UtcNow,
                        CustomerId = request.CustomerId ?? 0,
                        PaymentMethod = request.PaymentMethod.ToString(),
                        SubTotal = subTotal,
                        DiscountTotal = discountTotal,
                        TaxTotal = taxTotal,
                        TotalAmount = totalAmount,
                        CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? "Perakende Müşteri" : request.CustomerName,
                        Status = SalesOrderStatus.Completed
                    };
                    _context.SalesOrders.Add(salesOrder);
                    await _context.SaveChangesAsync();

                    foreach (var item in request.Items)
                    {
                        var orderItem = new SalesOrderItem
                        {
                            SalesOrderId = salesOrder.Id,
                            ProductId = item.ProductId,
                            ProductName = item.ProductName,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            DiscountPercent = item.DiscountPercent,
                            DiscountAmount = item.DiscountAmount,
                            TaxRate = item.TaxRate,
                            LineTotal = item.LineTotal
                        };
                        _context.SalesOrderItems.Add(orderItem);

                        var inventory = await _context.Inventories
                            .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.WarehouseId == request.WarehouseId);

                        if (inventory != null)
                        {
                            inventory.Quantity -= item.Quantity;
                        }
                        else
                        {
                            var newInventory = new Inventory
                            {
                                ProductId = item.ProductId,
                                WarehouseId = request.WarehouseId,
                                Quantity = -item.Quantity
                            };
                            _context.Inventories.Add(newInventory);
                        }

                        var stockTransaction = new StockTransaction
                        {
                            Date = DateTime.UtcNow,
                            ProductId = item.ProductId,
                            SourceWarehouseId = request.WarehouseId,
                            TargetWarehouseId = null,
                            Quantity = item.Quantity,
                            TransactionType = StockTransactionType.Sale,
                            UnitCost = item.UnitPrice,
                            Description = $"POS Satış - {orderNumber}",
                            ReferenceId = orderNumber
                        };
                        _context.StockTransactions.Add(stockTransaction);
                    }

                    var cashTransactionType = request.PaymentMethod switch
                    {
                        PaymentMethod.Cash => CashTransactionType.CashIncome,
                        PaymentMethod.CreditCard => CashTransactionType.CardIncome,
                        _ => CashTransactionType.CashIncome
                    };

                    var cashTransaction = new CashTransaction
                    {
                        Date = DateTime.UtcNow,
                        Amount = totalAmount,
                        TransactionType = cashTransactionType,
                        Description = $"POS Satış - {orderNumber}",
                        Category = "Perakende Satış",
                        SalesOrderId = salesOrder.Id,
                        CreatedBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? "Sistem" : request.CreatedBy,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.CashTransactions.Add(cashTransaction);

                    await _context.SaveChangesAsync();

                    if (request.CustomerId.HasValue && request.CustomerId.Value > 0)
                    {
                        var customer = await _context.Customers.FindAsync(request.CustomerId.Value);
                        if (customer != null)
                        {
                            customer.LastPurchaseDate = DateTime.UtcNow;
                            customer.TotalPurchaseCount++;
                            customer.TotalSpent += totalAmount;
                            
                            int earnedPoints = (int)(totalAmount / 100);
                            customer.LoyaltyPoints += earnedPoints;
                            
                            await _context.SaveChangesAsync();
                        }
                    }

                    await transaction.CommitAsync();

                    _logger.LogInformation($"Satış tamamlandı: {orderNumber}, Tutar: {totalAmount:C}");
                    return SaleResult.Ok(orderNumber, totalAmount);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Satış işlemi başarısız.");
                    return SaleResult.Fail($"Satış işlemi başarısız: {ex.Message}");
                }
            }
            finally
            {
                _salesLock.Release();
            }
        }

        public void ValidateCartItems(IEnumerable<SaleItemRequest> items, int warehouseId, bool allowNegativeStock = true)
        {
            if (allowNegativeStock) return;

            foreach (var item in items)
            {
                var inventory = _context.Inventories
                    .FirstOrDefault(i => i.ProductId == item.ProductId && i.WarehouseId == warehouseId);

                var available = inventory?.Quantity ?? 0;
                if (item.Quantity > available)
                {
                    throw new Exception($"Yetersiz Stok: {item.ProductName} ({available} adet var, {item.Quantity} isteniyor)");
                }
            }
        }

        public List<CustomerPurchaseHistory> GetCustomerPurchaseHistory(int customerId)
        {
            return _context.SalesOrders
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.Date)
                .Take(50)
                .Select(o => new CustomerPurchaseHistory
                {
                    TransactionId = o.Id,
                    Date = o.Date,
                    OrderNumber = o.OrderNumber,
                    TotalAmount = o.TotalAmount,
                    PaymentMethod = o.PaymentMethod,
                    ItemCount = o.Items.Count
                })
                .ToList();
        }

        public CustomerStatistics GetCustomerStatistics(int customerId)
        {
            var customer = _context.Customers.Find(customerId);
            if (customer == null) return new CustomerStatistics();

            var daysSinceLastPurchase = customer.LastPurchaseDate.HasValue 
                ? (DateTime.UtcNow - customer.LastPurchaseDate.Value).Days 
                : -1;

            return new CustomerStatistics
            {
                TotalPurchases = customer.TotalPurchaseCount,
                TotalSpent = customer.TotalSpent,
                AveragePurchase = customer.TotalPurchaseCount > 0 
                    ? customer.TotalSpent / customer.TotalPurchaseCount 
                    : 0,
                LastPurchaseDate = customer.LastPurchaseDate,
                LoyaltyPoints = customer.LoyaltyPoints,
                LoyaltyLevel = customer.LoyaltyLevel,
                DaysSinceLastPurchase = daysSinceLastPurchase
            };
        }

        public DailyPosReport GetDailyPosReport(DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            var orders = _context.SalesOrders
                .Where(o => o.Date >= startOfDay && o.Date < endOfDay)
                .ToList();

            var report = new DailyPosReport
            {
                Date = date,
                TotalTransactions = orders.Count,
                TotalRevenue = orders.Sum(o => o.TotalAmount),
                TotalDiscount = orders.Sum(o => o.DiscountTotal),
                TotalCash = orders.Where(o => o.PaymentMethod == "Cash").Sum(o => o.TotalAmount),
                TotalCard = orders.Where(o => o.PaymentMethod == "CreditCard").Sum(o => o.TotalAmount),
                WalkInCustomerCount = orders.Count(o => o.CustomerId == 0),
                RegisteredCustomerCount = orders.Count(o => o.CustomerId > 0)
            };

            var topProducts = _context.SalesOrderItems
                .Where(i => i.SalesOrder.Date >= startOfDay && i.SalesOrder.Date < endOfDay)
                .GroupBy(i => i.ProductName)
                .Select(g => new TopSellingProduct
                {
                    ProductName = g.Key,
                    Quantity = g.Sum(i => i.Quantity),
                    Revenue = g.Sum(i => i.LineTotal)
                })
                .OrderByDescending(p => p.Revenue)
                .Take(10)
                .ToList();

            report.TopProducts = topProducts;
            report.TotalItemsSold = topProducts.Sum(p => p.Quantity);

            return report;
        }

        public bool ReprintReceipt(int transactionId)
        {
            var order = _context.SalesOrders.Find(transactionId);
            if (order == null) return false;

            order.PrintCount++;
            order.IsReprinted = true;
            _context.SaveChanges();

            return true;
        }
    }
}
