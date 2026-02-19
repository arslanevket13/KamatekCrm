using System;
using System.Linq;
using System.Threading;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Events;
using KamatekCrm.Repositories;

namespace KamatekCrm.Services.Domain
{
    /// <summary>
    /// Satın alma domain servisi — Stok artışı, WAC hesaplama, Cari hesap (Borç Senedi)
    /// </summary>
    public interface IPurchasingDomainService
    {
        PurchaseResult CompletePurchaseOrder(PurchaseCompletionRequest request);
    }

    public class PurchasingDomainService : IPurchasingDomainService
    {
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private readonly IAuthService _authService;

        public PurchasingDomainService(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Satın alma siparişini tamamla:
        /// 1) PurchaseOrder status → Completed
        /// 2) Her kalem için stok artır (Inventory + StockTransaction)
        /// 3) WAC (Moving Average Cost) yeniden hesapla
        /// 4) Tedarikçi borç kaydı (CashTransaction → CashExpense)
        /// </summary>
        public PurchaseResult CompletePurchaseOrder(PurchaseCompletionRequest request)
        {
            if (request.PurchaseOrderId <= 0)
                return PurchaseResult.Fail("Geçersiz sipariş ID.");

            _lock.Wait();
            try
            {
                using var unitOfWork = new UnitOfWork(new AppDbContext());
                var context = unitOfWork.Context;

                using var transaction = unitOfWork.BeginTransaction();
                try
                {
                    // 1. PurchaseOrder'ı yükle
                    var po = context.PurchaseOrders
                        .FirstOrDefault(p => p.Id == request.PurchaseOrderId);

                    if (po == null)
                        return PurchaseResult.Fail("Satın alma siparişi bulunamadı.");

                    if (po.Status == PurchaseStatus.Received)
                        return PurchaseResult.Fail("Bu sipariş zaten tamamlanmış.");

                    // Kalemleri yükle
                    var items = context.PurchaseOrderItems
                        .Where(i => i.PurchaseOrderId == po.Id)
                        .ToList();

                    if (items.Count == 0)
                        return PurchaseResult.Fail("Sipariş kalemleri bulunamadı.");

                    decimal totalAmount = 0;

                    // 2. Her kalem için stok işlemi
                    foreach (var item in items)
                    {
                        // Stok artış
                        var inventory = context.Inventories
                            .FirstOrDefault(i => i.ProductId == item.ProductId && i.WarehouseId == request.WarehouseId);

                        int oldQty = 0;
                        if (inventory != null)
                        {
                            oldQty = inventory.Quantity;
                            inventory.Quantity += item.Quantity;
                        }
                        else
                        {
                            inventory = new Inventory
                            {
                                ProductId = item.ProductId,
                                WarehouseId = request.WarehouseId,
                                Quantity = item.Quantity
                            };
                            context.Inventories.Add(inventory);
                        }

                        // WAC (Moving Average Cost) hesapla
                        // newAvgCost = ((existingQty * existingAvgCost) + (newQty * newUnitCost)) / (existingQty + newQty)
                        var existingAvgCost = inventory.AverageCost;
                        var existingQty = oldQty;
                        var newQty = item.Quantity;
                        var newUnitCost = item.UnitPrice;

                        if (existingQty + newQty > 0)
                        {
                            inventory.AverageCost = ((existingQty * existingAvgCost) + (newQty * newUnitCost))
                                                    / (existingQty + newQty);
                        }

                        // Ürün toplam stok güncelle
                        var product = context.Products.Find(item.ProductId);
                        if (product != null)
                        {
                            product.TotalStockQuantity += item.Quantity;
                            product.PurchasePrice = newUnitCost; // Son alış fiyatı
                        }

                        // StockTransaction kaydet
                        var stockTx = new StockTransaction
                        {
                            Date = DateTime.UtcNow,
                            ProductId = item.ProductId,
                            TargetWarehouseId = request.WarehouseId,
                            SourceWarehouseId = null, // Dışarıdan giriş
                            Quantity = item.Quantity,
                            TransactionType = StockTransactionType.Purchase,
                            UnitCost = newUnitCost,
                            Description = $"Satın Alma - PO#{po.Id}",
                            ReferenceId = $"PO-{po.Id}"
                        };
                        context.StockTransactions.Add(stockTx);

                        // Kalem toplamı
                        var lineTotal = item.Quantity * item.UnitPrice;
                        // İndirim uygula (flat amount)
                        if (item.DiscountAmount > 0)
                            lineTotal -= item.DiscountAmount;
                        // KDV uygula
                        if (item.TaxRate > 0)
                            lineTotal += lineTotal * item.TaxRate / 100m;

                        totalAmount += lineTotal;

                        // Event
                        EventAggregator.Instance.Publish(new StockUpdatedEvent(
                            item.ProductId,
                            request.WarehouseId,
                            oldQty,
                            inventory.Quantity,
                            $"Satın Alma: PO#{po.Id}"
                        ));
                    }

                    // 3. PO güncelle
                    po.Status = PurchaseStatus.Received;
                    po.TotalAmount = totalAmount;

                    // 4. Kasa kaydı (Borç/Gider)
                    var cashTx = new CashTransaction
                    {
                        Date = DateTime.UtcNow,
                        Amount = totalAmount,
                        TransactionType = CashTransactionType.CashExpense,
                        PaymentMethod = PaymentMethod.BankTransfer,
                        Description = $"Satın Alma Faturası - PO#{po.Id} - {po.Supplier?.CompanyName ?? "Tedarikçi"}",
                        Category = "Satın Alma",
                        ReferenceNumber = $"PO-{po.Id}",
                        CreatedBy = request.CreatedBy ?? _authService.CurrentUser?.AdSoyad ?? "Sistem",
                        CreatedAt = DateTime.UtcNow
                    };
                    context.CashTransactions.Add(cashTx);

                    // 5. Commit
                    unitOfWork.Commit();

                    // Audit log
                    _ = AuditService.LogAsync(AuditActionType.Create, "Purchase",
                        $"PO-{po.Id}",
                        $"Satın alma tamamlandı. Tutar: {totalAmount:C}, Kalem: {items.Count}");

                    return PurchaseResult.Ok(po.Id, totalAmount);
                }
                catch (Exception ex)
                {
                    unitOfWork.Rollback();
                    _ = AuditService.LogAsync(AuditActionType.Create, "Purchase", null,
                        $"Satın alma hatası: {ex.Message}");
                    return PurchaseResult.Fail($"Satın alma hatası: {ex.Message}");
                }
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    // ================= DTOs =================

    public class PurchaseCompletionRequest
    {
        public int PurchaseOrderId { get; set; }
        public int WarehouseId { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class PurchaseResult
    {
        public bool Success { get; set; }
        public int PurchaseOrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public static PurchaseResult Ok(int poId, decimal total)
            => new() { Success = true, PurchaseOrderId = poId, TotalAmount = total };

        public static PurchaseResult Fail(string error)
            => new() { Success = false, ErrorMessage = error };
    }
}
