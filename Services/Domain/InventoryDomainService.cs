using System;
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
    /// Stok işlemlerini yöneten domain service - Thread-safe transactional operations
    /// </summary>
    public class InventoryDomainService : IInventoryDomainService
    {
        // Thread safety için SemaphoreSlim
        private static readonly SemaphoreSlim _stockLock = new(1, 1);
        private readonly IAuthService _authService;

        public InventoryDomainService(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Depolar arası stok transferi gerçekleştirir
        /// </summary>
        public TransferResult TransferStock(TransferRequest request)
        {
            if (request.Quantity <= 0)
                return TransferResult.Fail("Transfer miktarı sıfırdan büyük olmalıdır.");

            if (request.SourceWarehouseId == request.TargetWarehouseId)
                return TransferResult.Fail("Kaynak ve hedef depo aynı olamaz.");

            _stockLock.Wait();
            try
            {
                using var unitOfWork = new UnitOfWork();
                var context = unitOfWork.Context;

                using var transaction = unitOfWork.BeginTransaction();
                try
                {
                    // 1. Kaynak depo stok kontrolü
                    var sourceInventory = context.Inventories
                        .FirstOrDefault(i => i.ProductId == request.ProductId && i.WarehouseId == request.SourceWarehouseId);

                    if (sourceInventory == null || sourceInventory.Quantity < request.Quantity)
                    {
                        var available = sourceInventory?.Quantity ?? 0;
                        return TransferResult.Fail($"Kaynak depoda yeterli stok yok. Mevcut: {available}, İstenen: {request.Quantity}");
                    }

                    int oldSourceQty = sourceInventory.Quantity;
                    sourceInventory.Quantity -= request.Quantity;

                    // 2. Hedef depo stok güncelleme
                    var targetInventory = context.Inventories
                        .FirstOrDefault(i => i.ProductId == request.ProductId && i.WarehouseId == request.TargetWarehouseId);

                    int oldTargetQty = 0;
                    if (targetInventory == null)
                    {
                        targetInventory = new Inventory
                        {
                            ProductId = request.ProductId,
                            WarehouseId = request.TargetWarehouseId,
                            Quantity = request.Quantity
                        };
                        context.Inventories.Add(targetInventory);
                    }
                    else
                    {
                        oldTargetQty = targetInventory.Quantity;
                        targetInventory.Quantity += request.Quantity;
                    }

                    // 3. Stok hareketi kaydı
                    var stockTransaction = new StockTransaction
                    {
                        Date = DateTime.Now,
                        ProductId = request.ProductId,
                        SourceWarehouseId = request.SourceWarehouseId,
                        TargetWarehouseId = request.TargetWarehouseId,
                        Quantity = request.Quantity,
                        TransactionType = StockTransactionType.Transfer,
                        Description = request.Description ?? "Depolar arası transfer"
                    };
                    context.StockTransactions.Add(stockTransaction);

                    // 4. Commit
                    unitOfWork.Commit();

                    // 5. Event yayınla
                    EventAggregator.Instance.Publish(new StockUpdatedEvent(
                        request.ProductId,
                        request.SourceWarehouseId,
                        oldSourceQty,
                        sourceInventory.Quantity,
                        "Depo transferi (çıkış)"
                    ));

                    EventAggregator.Instance.Publish(new StockUpdatedEvent(
                        request.ProductId,
                        request.TargetWarehouseId,
                        oldTargetQty,
                        targetInventory.Quantity,
                        "Depo transferi (giriş)"
                    ));

                    // Log
                    _ = AuditService.LogAsync(AuditActionType.Create, "StockTransfer", stockTransaction.Id.ToString(),
                        $"Transfer: Ürün #{request.ProductId}, {request.Quantity} adet, Depo {request.SourceWarehouseId} → {request.TargetWarehouseId}");

                    return TransferResult.Ok(stockTransaction.Id);
                }
                catch (Exception ex)
                {
                    unitOfWork.Rollback();
                    _ = AuditService.LogAsync(AuditActionType.Create, "StockTransfer", null, $"Transfer hatası: {ex.Message}");
                    return TransferResult.Fail($"Transfer işlemi başarısız: {ex.Message}");
                }
            }
            finally
            {
                _stockLock.Release();
            }
        }

        /// <summary>
        /// Belirli bir ürünün belirli bir depodaki stok miktarını döndürür
        /// </summary>
        public int GetAvailableStock(int productId, int warehouseId)
        {
            using var unitOfWork = new UnitOfWork();
            var context = unitOfWork.Context;

            var inventory = context.Inventories
                .FirstOrDefault(i => i.ProductId == productId && i.WarehouseId == warehouseId);

            return inventory?.Quantity ?? 0;
        }

        /// <summary>
        /// Stok miktarını günceller ve StockTransaction kaydı oluşturur
        /// </summary>
        public void AdjustStock(StockAdjustmentRequest request)
        {
            _stockLock.Wait();
            try
            {
                using var unitOfWork = new UnitOfWork();
                var context = unitOfWork.Context;

                using var transaction = unitOfWork.BeginTransaction();
                try
                {
                    var inventory = context.Inventories
                        .FirstOrDefault(i => i.ProductId == request.ProductId && i.WarehouseId == request.WarehouseId);

                    int oldQty = 0;
                    if (inventory == null)
                    {
                        inventory = new Inventory
                        {
                            ProductId = request.ProductId,
                            WarehouseId = request.WarehouseId,
                            Quantity = request.QuantityChange
                        };
                        context.Inventories.Add(inventory);
                    }
                    else
                    {
                        oldQty = inventory.Quantity;
                        inventory.Quantity += request.QuantityChange;
                    }

                    // Stok hareketi kaydı
                    var transactionType = request.QuantityChange > 0 
                        ? StockTransactionType.AdjustmentPlus 
                        : StockTransactionType.AdjustmentMinus;

                    var stockTransaction = new StockTransaction
                    {
                        Date = DateTime.Now,
                        ProductId = request.ProductId,
                        SourceWarehouseId = request.WarehouseId,
                        Quantity = Math.Abs(request.QuantityChange),
                        TransactionType = transactionType,
                        Description = request.Reason,
                        ReferenceId = request.ReferenceId ?? string.Empty
                    };
                    context.StockTransactions.Add(stockTransaction);

                    unitOfWork.Commit();

                    // Event yayınla
                    EventAggregator.Instance.Publish(new StockUpdatedEvent(
                        request.ProductId,
                        request.WarehouseId,
                        oldQty,
                        inventory.Quantity,
                        request.Reason
                    ));
                }
                catch
                {
                    unitOfWork.Rollback();
                    throw;
                }
            }
            finally
            {
                _stockLock.Release();
            }
        }
        /// <summary>
        /// Depoya yeni stok girişi yapar ve Ortalama Maliyet (WAC) hesaplar
        /// </summary>
        public void AddStock(int productId, int warehouseId, int quantity, decimal unitCost, string referenceId)
        {
             _stockLock.Wait();
            try
            {
                using var unitOfWork = new UnitOfWork();
                var context = unitOfWork.Context;

                using var transaction = unitOfWork.BeginTransaction();
                try
                {
                    var inventory = context.Inventories
                        .FirstOrDefault(i => i.ProductId == productId && i.WarehouseId == warehouseId);

                    int oldQty = 0;
                    decimal oldCost = 0;

                    if (inventory == null)
                    {
                        inventory = new Inventory
                        {
                            ProductId = productId,
                            WarehouseId = warehouseId,
                            Quantity = quantity,
                            AverageCost = unitCost
                        };
                        context.Inventories.Add(inventory);
                    }
                    else
                    {
                        oldQty = inventory.Quantity;
                        oldCost = inventory.AverageCost;

                        // Weighted Average Cost (WAC) Calculation
                        // (OldQty * OldCost) + (NewQty * NewCost) / (OldQty + NewQty)
                        decimal currentTotalValue = oldQty * oldCost;
                        decimal newTotalValue = quantity * unitCost;
                        int totalQty = oldQty + quantity;

                        if (totalQty > 0)
                        {
                            inventory.AverageCost = (currentTotalValue + newTotalValue) / totalQty;
                        }
                        
                        inventory.Quantity += quantity;
                    }

                    // Stok hareketi kaydı
                    var stockTransaction = new StockTransaction
                    {
                        Date = DateTime.Now,
                        ProductId = productId,
                        TargetWarehouseId = warehouseId,
                        Quantity = quantity,
                        TransactionType = StockTransactionType.Purchase,
                        UnitCost = unitCost,
                        Description = $"Satın Alma (WAC Güncellendi) - {referenceId}",
                        ReferenceId = referenceId,
                        UserId = _authService.CurrentUser?.AdSoyad ?? "Sistem"
                    };
                    context.StockTransactions.Add(stockTransaction);

                    unitOfWork.Commit();

                    // Event yayınla
                    EventAggregator.Instance.Publish(new StockUpdatedEvent(
                        productId,
                        warehouseId,
                        oldQty,
                        inventory.Quantity,
                        $"Stok Girişi (Satın Alma) - Yeni Maliyet: {inventory.AverageCost:C2}"
                    ));
                    
                    // Log
                    _ = AuditService.LogAsync(AuditActionType.Update, "Inventory", referenceId, 
                        $"Stok Girdi: Ürün #{productId}, {quantity} adet @ {unitCost:C2}. Yeni WAC: {inventory.AverageCost:C2}");
                }
                catch (Exception ex)
                {
                    unitOfWork.Rollback();
                    throw new Exception($"Stok ekleme hatası: {ex.Message}");
                }
            }
            finally
            {
                _stockLock.Release();
            }
        }
    }
}
