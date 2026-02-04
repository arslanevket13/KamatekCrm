using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Events;
using KamatekCrm.Exceptions;
using KamatekCrm.Shared.Models;
using KamatekCrm.Repositories;

namespace KamatekCrm.Services.Domain
{
    /// <summary>
    /// Satış işlemlerini yöneten domain service - Thread-safe transactional operations
    /// </summary>
    public class SalesDomainService : ISalesDomainService
    {
        // Thread safety için SemaphoreSlim (eşzamanlı satış işlemlerini sıraya koy)
        private static readonly SemaphoreSlim _salesLock = new(1, 1);

        /// <summary>
        /// Satış işlemini gerçekleştirir (Transaction içinde)
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
                using var unitOfWork = new UnitOfWork();
                var context = unitOfWork.Context;

                using var transaction = unitOfWork.BeginTransaction();
                try
                {
                    // 1. Sipariş numarası oluştur
                    var todayOrders = context.SalesOrders.Count(o => o.Date.Date == DateTime.Today);
                    var orderNumber = $"SO-{DateTime.Now:yyyyMMdd}-{(todayOrders + 1):D3}";

                    // 2. Toplam tutarı hesapla
                    var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);

                    // 3. SalesOrder oluştur
                    var salesOrder = new SalesOrder
                    {
                        OrderNumber = orderNumber,
                        Date = DateTime.Now,
                        PaymentMethod = request.PaymentMethod,
                        TotalAmount = totalAmount,
                        CustomerName = request.CustomerName
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
                            UnitPrice = item.UnitPrice
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
                        ReferenceNumber = orderNumber,
                        SalesOrderId = salesOrder.Id,
                        CreatedBy = request.CreatedBy,
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

            using var unitOfWork = new UnitOfWork();
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
    }
}
