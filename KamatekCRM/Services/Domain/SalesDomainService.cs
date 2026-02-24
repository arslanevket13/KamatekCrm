using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Events;
using KamatekCrm.Exceptions;
using KamatekCrm.Shared.Models;
using KamatekCrm.Repositories;

using KamatekCrm.Data;

namespace KamatekCrm.Services.Domain
{
    /// <summary>
    /// Satış işlemlerini yöneten domain service - Thread-safe transactional operations
    /// </summary>
    public class SalesDomainService : ISalesDomainService
    {
        // Thread safety için SemaphoreSlim (eşzamanlı satış işlemlerini sıraya koy)
        private static readonly SemaphoreSlim _salesLock = new(1, 1);
        private readonly IAuthService _authService;

        public SalesDomainService(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Satış işlemini gerçekleştiri (Transaction içinde)
        /// </summary>
        public SalesResult ProcessSale(SaleRequest request)
        {
            if (request.Items.Count == 0)
                return SalesResult.Fail("Sepet boş. Satış yapılamaz.");

            if (request.WarehouseId <= 0)
                return SalesResult.Fail("Geçerli bir depo seçilmedi.");

            // Thread safety: Eşzamanlı satışları sıraya al
            _salesLock.Wait();
            try
            {
                using var unitOfWork = new UnitOfWork(new AppDbContext());
                var context = unitOfWork.Context;

                using var transaction = unitOfWork.BeginTransaction();
                try
                {
                    // 1. Sipariş numarası oluştur
                    var todayOrders = context.SalesOrders.Count(o => o.Date.Date == DateTime.Today);
                    var orderNumber = $"SO-{DateTime.Now:yyyyMMdd}-{(todayOrders + 1):D3}";

                    // 2. Toplamları hesapla
                    var subTotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);
                    var discountTotal = request.Items.Sum(i => i.DiscountAmount);
                    var taxTotal = request.Items.Sum(i =>
                    {
                        var afterDiscount = (i.Quantity * i.UnitPrice) - i.DiscountAmount;
                        return afterDiscount * i.TaxRate / 100m;
                    });
                    var totalAmount = request.Items.Sum(i => i.LineTotal > 0 ? i.LineTotal : (i.Quantity * i.UnitPrice));

                    // 3. SalesOrder oluştur
                    var salesOrder = new SalesOrder
                    {
                        OrderNumber = orderNumber,
                        Date = DateTime.Now,
                        CustomerId = request.CustomerId ?? 0,
                        PaymentMethod = request.PaymentMethod.ToString(),
                        SubTotal = subTotal,
                        DiscountTotal = discountTotal,
                        TaxTotal = taxTotal,
                        TotalAmount = totalAmount,
                        CustomerName = request.CustomerName,
                        Status = SalesOrderStatus.Completed
                    };
                    context.SalesOrders.Add(salesOrder);
                    context.SaveChanges(); // ID almak için

                    // 4. Sipariş kalemleri ve stok işlemleri
                    foreach (var item in request.Items)
                    {
                        // SalesOrderItem
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
                        context.SalesOrderItems.Add(orderItem);

                        // Stok düş
                        var inventory = context.Inventories
                            .FirstOrDefault(i => i.ProductId == item.ProductId && i.WarehouseId == request.WarehouseId);

                        int oldQty = 0;
                        if (inventory != null)
                        {
                            oldQty = inventory.Quantity;
                            inventory.Quantity -= item.Quantity;
                        }
                        else
                        {
                            // Inventory kaydı yoksa yeni oluştur (negatif stok)
                            var newInventory = new Inventory
                            {
                                ProductId = item.ProductId,
                                WarehouseId = request.WarehouseId,
                                Quantity = -item.Quantity
                            };
                            context.Inventories.Add(newInventory);
                        }

                        // StockTransaction kaydet
                        var stockTransaction = new StockTransaction
                        {
                            Date = DateTime.Now,
                            ProductId = item.ProductId,
                            SourceWarehouseId = request.WarehouseId,
                            TargetWarehouseId = null, // Satış çıkışı
                            Quantity = item.Quantity,
                            TransactionType = StockTransactionType.Sale,
                            UnitCost = item.UnitPrice,
                            Description = $"POS Satış - {orderNumber}",
                            ReferenceId = orderNumber
                        };
                        context.StockTransactions.Add(stockTransaction);

                        // Event yayınla
                        EventAggregator.Instance.Publish(new StockUpdatedEvent(
                            item.ProductId,
                            request.WarehouseId,
                            oldQty,
                            inventory?.Quantity ?? -item.Quantity,
                            $"Satış: {orderNumber}"
                        ));
                    }

                    // 5. Kasa kaydı oluştur
                    var cashTransactionType = request.PaymentMethod switch
                    {
                        PaymentMethod.Cash => CashTransactionType.CashIncome,
                        PaymentMethod.CreditCard => CashTransactionType.CardIncome,
                        _ => CashTransactionType.CashIncome
                    };

                    var cashTransaction = new CashTransaction
                    {
                        Date = DateTime.Now,
                        Amount = totalAmount,
                        TransactionType = cashTransactionType,
                        Description = $"POS Satış - {orderNumber}",
                        Category = "Perakende Satış",
                        SalesOrderId = salesOrder.Id,
                        CreatedBy = request.CreatedBy ?? _authService.CurrentUser?.AdSoyad ?? "Sistem",
                        CreatedAt = DateTime.Now
                    };
                    context.CashTransactions.Add(cashTransaction);

                    // 6. Commit
                    unitOfWork.Commit();

                    // 7. Event yayınla
                    EventAggregator.Instance.Publish(new SaleCompletedEvent(
                        orderNumber,
                        totalAmount,
                        request.WarehouseId,
                        request.Items.Count
                    ));

                    // Log
                    _ = AuditService.LogAsync(AuditActionType.Create, "Sale", orderNumber, $"POS Satış tamamlandı: {orderNumber}, Tutar: {totalAmount:C}");

                    // ======================== Müşteri İstatistiklerini Güncelle ========================
                    if (request.CustomerId.HasValue)
                    {
                        var customer = context.Customers.Find(request.CustomerId.Value);
                        if (customer != null)
                        {
                            customer.LastPurchaseDate = DateTime.Now;
                            customer.TotalPurchaseCount++;
                            customer.TotalSpent += totalAmount;
                            
                            // Puan hesapla: Her 100 TL = 1 puan
                            int earnedPoints = (int)(totalAmount / 100);
                            customer.LoyaltyPoints += earnedPoints;
                            
                            context.SaveChanges();
                            
                            _ = AuditService.LogAsync(AuditActionType.Update, "Customer", customer.Id.ToString(), 
                                $"Müşteri istatistikleri güncellendi. Toplam: {customer.TotalSpent:C}, Puan: +{earnedPoints}");
                        }
                    }

                    return SalesResult.Ok(orderNumber, totalAmount);
                }
                catch (Exception ex)
                {
                    unitOfWork.Rollback();
                    _ = AuditService.LogAsync(AuditActionType.Create, "Sale", null, $"Satış hatası: {ex.Message}");
                    return SalesResult.Fail($"Satış işlemi başarısız: {ex.Message}");
                }
            }
            finally
            {
                _salesLock.Release();
            }
        }

        /// <summary>
        /// Sepet öğelerini stok durumuna göre doğrular
        /// </summary>
        public void ValidateCartItems(IEnumerable<SaleItemRequest> items, int warehouseId, bool allowNegativeStock = true)
        {
            if (allowNegativeStock) return; // Negatif stok izinliyse validasyon yok

            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            foreach (var item in items)
            {
                var inventory = context.Inventories
                    .FirstOrDefault(i => i.ProductId == item.ProductId && i.WarehouseId == warehouseId);

                var available = inventory?.Quantity ?? 0;

                if (item.Quantity > available)
                {
                    throw new InsufficientStockException(
                        item.ProductId,
                        item.ProductName,
                        item.Quantity,
                        available,
                        warehouseId
                    );
                }
            }
        }

        /// <summary>
        /// Müşterinin alışveriş geçmişini getirir
        /// </summary>
        public List<CustomerPurchaseHistory> GetCustomerPurchaseHistory(int customerId)
        {
            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            var purchases = context.SalesOrders
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

            return purchases;
        }

        /// <summary>
        /// Müşteri istatistiklerini getirir
        /// </summary>
        public CustomerStatistics GetCustomerStatistics(int customerId)
        {
            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            var customer = context.Customers.Find(customerId);
            if (customer == null)
            {
                return new CustomerStatistics();
            }

            var daysSinceLastPurchase = customer.LastPurchaseDate.HasValue 
                ? (DateTime.Now - customer.LastPurchaseDate.Value).Days 
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

        /// <summary>
        /// Günlük POS raporu getirir
        /// </summary>
        public DailyPosReport GetDailyPosReport(DateTime date)
        {
            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            var orders = context.SalesOrders
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

            // Top products
            var topProducts = context.SalesOrderItems
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

        /// <summary>
        /// Fiş tekrar yazdır
        /// </summary>
        public bool ReprintReceipt(int transactionId)
        {
            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            var order = context.SalesOrders.Find(transactionId);
            if (order == null) return false;

            order.PrintCount++;
            order.IsReprinted = true;
            context.SaveChanges();

            return true;
        }
    }
}
