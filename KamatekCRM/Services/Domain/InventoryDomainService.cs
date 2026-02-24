using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Events;
using KamatekCrm.Exceptions;
using KamatekCrm.Shared.Models;
using KamatekCrm.Repositories;

using KamatekCrm.Data;
using Microsoft.EntityFrameworkCore;

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
                using var unitOfWork = new UnitOfWork(new AppDbContext());
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
            using var unitOfWork = new UnitOfWork(new AppDbContext());
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
                using var unitOfWork = new UnitOfWork(new AppDbContext());
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
                using var unitOfWork = new UnitOfWork(new AppDbContext());
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

        // ======================== ASYNC METODLAR ========================

        public async Task<TransferResult> TransferStockAsync(TransferRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Quantity <= 0)
                return TransferResult.Fail("Transfer miktarı sıfırdan büyük olmalıdır.");

            if (request.SourceWarehouseId == request.TargetWarehouseId)
                return TransferResult.Fail("Kaynak ve hedef depo aynı olamaz.");

            await _stockLock.WaitAsync(cancellationToken);
            try
            {
                using var unitOfWork = new UnitOfWork(new AppDbContext());
                var context = unitOfWork.Context;

                using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
                try
                {
                    var sourceInventory = await context.Inventories
                        .FirstOrDefaultAsync(i => i.ProductId == request.ProductId && i.WarehouseId == request.SourceWarehouseId, cancellationToken);

                    if (sourceInventory == null || sourceInventory.Quantity < request.Quantity)
                    {
                        var available = sourceInventory?.Quantity ?? 0;
                        return TransferResult.Fail($"Kaynak depoda yeterli stok yok. Mevcut: {available}, İstenen: {request.Quantity}");
                    }

                    int oldSourceQty = sourceInventory.Quantity;
                    sourceInventory.Quantity -= request.Quantity;

                    var targetInventory = await context.Inventories
                        .FirstOrDefaultAsync(i => i.ProductId == request.ProductId && i.WarehouseId == request.TargetWarehouseId, cancellationToken);

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

                    await unitOfWork.CommitAsync(cancellationToken);

                    EventAggregator.Instance.Publish(new StockUpdatedEvent(request.ProductId, request.SourceWarehouseId, oldSourceQty, sourceInventory.Quantity, "Transfer (çıkış)"));
                    EventAggregator.Instance.Publish(new StockUpdatedEvent(request.ProductId, request.TargetWarehouseId, oldTargetQty, targetInventory.Quantity, "Transfer (giriş)"));

                    return TransferResult.Ok(stockTransaction.Id);
                }
                catch (Exception ex)
                {
                    await unitOfWork.RollbackAsync(cancellationToken);
                    return TransferResult.Fail($"Transfer başarısız: {ex.Message}");
                }
            }
            finally
            {
                _stockLock.Release();
            }
        }

        public async Task<int> GetAvailableStockAsync(int productId, int warehouseId, CancellationToken cancellationToken = default)
        {
            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            var inventory = await context.Inventories
                .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId, cancellationToken);

            return inventory?.Quantity ?? 0;
        }

        public async Task AdjustStockAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default)
        {
            await _stockLock.WaitAsync(cancellationToken);
            try
            {
                using var unitOfWork = new UnitOfWork(new AppDbContext());
                var context = unitOfWork.Context;

                var inventory = await context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == request.ProductId && i.WarehouseId == request.WarehouseId, cancellationToken);

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

                var transactionType = request.QuantityChange > 0 ? StockTransactionType.AdjustmentPlus : StockTransactionType.AdjustmentMinus;

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

                await unitOfWork.CommitAsync(cancellationToken);

                EventAggregator.Instance.Publish(new StockUpdatedEvent(request.ProductId, request.WarehouseId, oldQty, inventory.Quantity, request.Reason));
            }
            finally
            {
                _stockLock.Release();
            }
        }

        public async Task AddStockAsync(int productId, int warehouseId, int quantity, decimal unitCost, string referenceId, CancellationToken cancellationToken = default)
        {
            await _stockLock.WaitAsync(cancellationToken);
            try
            {
                using var unitOfWork = new UnitOfWork(new AppDbContext());
                var context = unitOfWork.Context;

                var inventory = await context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId, cancellationToken);

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

                    decimal currentTotalValue = oldQty * oldCost;
                    decimal newTotalValue = quantity * unitCost;
                    int totalQty = oldQty + quantity;

                    if (totalQty > 0)
                    {
                        inventory.AverageCost = (currentTotalValue + newTotalValue) / totalQty;
                    }

                    inventory.Quantity += quantity;
                }

                var stockTransaction = new StockTransaction
                {
                    Date = DateTime.Now,
                    ProductId = productId,
                    TargetWarehouseId = warehouseId,
                    Quantity = quantity,
                    TransactionType = StockTransactionType.Purchase,
                    UnitCost = unitCost,
                    Description = $"Satın Alma - {referenceId}",
                    ReferenceId = referenceId,
                    UserId = _authService.CurrentUser?.AdSoyad ?? "Sistem"
                };
                context.StockTransactions.Add(stockTransaction);

                await unitOfWork.CommitAsync(cancellationToken);

                EventAggregator.Instance.Publish(new StockUpdatedEvent(productId, warehouseId, oldQty, inventory.Quantity, $"Stok Girişi - WAC: {inventory.AverageCost:C2}"));
            }
            finally
            {
                _stockLock.Release();
            }
        }

        // ======================== STOK DEĞERLEME ========================

        public async Task<decimal> GetTotalInventoryValueAsync(int? warehouseId = null, CancellationToken cancellationToken = default)
        {
            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            var query = context.Inventories.AsQueryable();

            if (warehouseId.HasValue)
            {
                query = query.Where(i => i.WarehouseId == warehouseId.Value);
            }

            var inventories = await query.ToListAsync(cancellationToken);

            return inventories.Sum(i => i.Quantity * i.AverageCost);
        }

        public async Task<IEnumerable<LowStockProduct>> GetLowStockProductsAsync(int? warehouseId = null, CancellationToken cancellationToken = default)
        {
            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            var query = from i in context.Inventories
                        join p in context.Products on i.ProductId equals p.Id
                        join w in context.Warehouses on i.WarehouseId equals w.Id
                        where i.Quantity < p.MinStockLevel
                        where !p.IsDeleted
                        where i.Quantity > 0
                        select new LowStockProduct
                        {
                            ProductId = p.Id,
                            ProductName = p.ProductName,
                            WarehouseId = i.WarehouseId,
                            WarehouseName = w.Name,
                            CurrentStock = i.Quantity,
                            MinStockLevel = p.MinStockLevel
                        };

            if (warehouseId.HasValue)
            {
                query = query.Where(x => x.WarehouseId == warehouseId.Value);
            }

            return await query.ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<StockTransaction>> GetStockTransactionsAsync(StockTransactionQuery query, CancellationToken cancellationToken = default)
        {
            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            var q = context.StockTransactions.AsQueryable();

            if (query.ProductId.HasValue)
                q = q.Where(t => t.ProductId == query.ProductId.Value);

            if (query.WarehouseId.HasValue)
                q = q.Where(t => t.SourceWarehouseId == query.WarehouseId.Value || t.TargetWarehouseId == query.WarehouseId.Value);

            if (query.TransactionType.HasValue)
                q = q.Where(t => t.TransactionType == query.TransactionType.Value);

            if (query.StartDate.HasValue)
                q = q.Where(t => t.Date >= query.StartDate.Value);

            if (query.EndDate.HasValue)
                q = q.Where(t => t.Date <= query.EndDate.Value);

            return await q
                .OrderByDescending(t => t.Date)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
        }

        // ======================== STOK REZERVASYON ========================

        public async Task<ReservationResult> ReserveStockAsync(StockReservationRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Quantity <= 0)
                return ReservationResult.Fail("Rezervasyon miktarı sıfırdan büyük olmalıdır.");

            await _stockLock.WaitAsync(cancellationToken);
            try
            {
                using var unitOfWork = new UnitOfWork(new AppDbContext());
                var context = unitOfWork.Context;

                var availableStock = await GetAvailableStockAsync(request.ProductId, request.WarehouseId, cancellationToken);
                var reservedStock = await GetReservedQuantityAsync(request.ProductId, request.WarehouseId, cancellationToken);
                var netAvailable = availableStock - reservedStock;

                if (netAvailable < request.Quantity)
                {
                    return ReservationResult.Fail($"Yeterli stok yok. Mevcut: {netAvailable}, İstenen: {request.Quantity}");
                }

                var reservation = new StockReservation
                {
                    ProductId = request.ProductId,
                    WarehouseId = request.WarehouseId,
                    Quantity = request.Quantity,
                    ReferenceType = request.ReferenceType,
                    ReferenceId = request.ReferenceId,
                    ExpiresAt = request.ExpiresAt,
                    ReservedBy = _authService.CurrentUser?.AdSoyad ?? "Sistem",
                    IsActive = true
                };

                context.StockReservations.Add(reservation);
                await unitOfWork.CommitAsync(cancellationToken);

                return ReservationResult.Ok(reservation.Id);
            }
            finally
            {
                _stockLock.Release();
            }
        }

        public async Task<bool> CancelReservationAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            var reservation = await context.StockReservations.FindAsync(new object[] { reservationId }, cancellationToken);
            if (reservation == null || !reservation.IsActive)
                return false;

            reservation.IsActive = false;
            await unitOfWork.CommitAsync(cancellationToken);

            return true;
        }

        public async Task<int> GetReservedQuantityAsync(int productId, int warehouseId, CancellationToken cancellationToken = default)
        {
            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            return await context.StockReservations
                .Where(r => r.ProductId == productId && r.WarehouseId == warehouseId && r.IsActive)
                .Where(r => !r.ExpiresAt.HasValue || r.ExpiresAt > DateTime.Now)
                .SumAsync(r => r.Quantity, cancellationToken);
        }

        // ======================== STOK GÖRSELLERİ ========================

        public async Task<InventoryImage> AddInventoryImageAsync(InventoryImageRequest request, CancellationToken cancellationToken = default)
        {
            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            var image = new InventoryImage
            {
                ProductId = request.ProductId,
                WarehouseId = request.WarehouseId,
                ImagePath = request.ImagePath,
                ThumbnailPath = request.ThumbnailPath ?? request.ImagePath,
                Description = request.Description ?? string.Empty,
                UploadedBy = request.UploadedBy,
                UploadedAt = DateTime.Now
            };

            context.InventoryImages.Add(image);
            await unitOfWork.CommitAsync(cancellationToken);

            return image;
        }

        public async Task<IEnumerable<InventoryImage>> GetInventoryImagesAsync(int productId, int warehouseId, CancellationToken cancellationToken = default)
        {
            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            return await context.InventoryImages
                .Where(i => i.ProductId == productId && i.WarehouseId == warehouseId)
                .OrderByDescending(i => i.UploadedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> DeleteInventoryImageAsync(int imageId, CancellationToken cancellationToken = default)
        {
            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            var image = await context.InventoryImages.FindAsync(new object[] { imageId }, cancellationToken);
            if (image == null)
                return false;

            context.InventoryImages.Remove(image);
            await unitOfWork.CommitAsync(cancellationToken);

            return true;
        }

        // ======================== BATCH OPERATIONS ========================

        public async Task<BatchOperationResult> AddStockBatchAsync(IEnumerable<BatchStockEntry> entries, CancellationToken cancellationToken = default)
        {
            var result = new BatchOperationResult { Success = true };

            await _stockLock.WaitAsync(cancellationToken);
            try
            {
                using var unitOfWork = new UnitOfWork(new AppDbContext());
                var context = unitOfWork.Context;

                foreach (var entry in entries)
                {
                    try
                    {
                        var inventory = await context.Inventories
                            .FirstOrDefaultAsync(i => i.ProductId == entry.ProductId && i.WarehouseId == 1, cancellationToken);

                        if (inventory == null)
                        {
                            inventory = new Inventory
                            {
                                ProductId = entry.ProductId,
                                WarehouseId = 1,
                                Quantity = entry.Quantity,
                                AverageCost = entry.UnitCost
                            };
                            context.Inventories.Add(inventory);
                        }
                        else
                        {
                            decimal currentValue = inventory.Quantity * inventory.AverageCost;
                            decimal newValue = entry.Quantity * entry.UnitCost;
                            int totalQty = inventory.Quantity + entry.Quantity;

                            if (totalQty > 0)
                                inventory.AverageCost = (currentValue + newValue) / totalQty;

                            inventory.Quantity += entry.Quantity;
                        }

                        var transaction = new StockTransaction
                        {
                            Date = DateTime.Now,
                            ProductId = entry.ProductId,
                            TargetWarehouseId = 1,
                            Quantity = entry.Quantity,
                            TransactionType = StockTransactionType.Purchase,
                            UnitCost = entry.UnitCost,
                            ReferenceId = entry.ReferenceId,
                            UserId = _authService.CurrentUser?.AdSoyad ?? "Sistem"
                        };
                        context.StockTransactions.Add(transaction);

                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"Ürün #{entry.ProductId}: {ex.Message}");
                    }
                }

                await unitOfWork.CommitAsync(cancellationToken);
            }
            finally
            {
                _stockLock.Release();
            }

            return result;
        }

        // ======================== SON KULLANMA TARİHİ ========================

        public async Task<IEnumerable<ExpiringStock>> GetExpiringStockAsync(int daysThreshold = 30, int? warehouseId = null, CancellationToken cancellationToken = default)
        {
            using var unitOfWork = new UnitOfWork(new AppDbContext());
            var context = unitOfWork.Context;

            var thresholdDate = DateTime.Now.AddDays(daysThreshold);

            var query = from ps in context.ProductSerials
                        join p in context.Products on ps.ProductId equals p.Id
                        join i in context.Inventories on p.Id equals i.ProductId
                        join w in context.Warehouses on i.WarehouseId equals w.Id
                        where ps.ExpiryDate.HasValue
                        where ps.ExpiryDate <= thresholdDate
                        where i.Quantity > 0
                        select new ExpiringStock
                        {
                            ProductId = p.Id,
                            ProductName = p.ProductName,
                            SerialNumber = ps.SerialNumber,
                            ExpiryDate = ps.ExpiryDate!.Value,
                            DaysUntilExpiry = (ps.ExpiryDate.Value - DateTime.Now).Days,
                            Quantity = i.Quantity,
                            WarehouseName = w.Name
                        };

            if (warehouseId.HasValue)
                query = query.Where(x => x.Quantity > 0);

            return await query
                .OrderBy(x => x.ExpiryDate)
                .Take(100)
                .ToListAsync(cancellationToken);
        }
    }
}
