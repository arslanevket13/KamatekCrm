using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;

namespace KamatekCrm.Services
{
    public class DirectSalesService : IDirectSalesService
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IApplicationAuthorizationService _authorizationService;

        public DirectSalesService(
            IDbContextFactory<AppDbContext> dbContextFactory,
            IApplicationAuthorizationService authorizationService)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _authorizationService = authorizationService;
        }

        public async Task<SalesOrder> ProcessSaleAsync(
            int? customerId,
            string customerName,
            int warehouseId,
            IEnumerable<PosCartItem> cartItems,
            IEnumerable<PosPaymentEntry> payments,
            string? notes,
            string? currentUserName,
            string idempotencyKey)
        {
            var authorization = _authorizationService.Authorize(ApplicationPermission.ExecuteSales);
            if (authorization.IsFailure)
                throw new UnauthorizedAccessException(authorization.Error);

            if (!Guid.TryParse(idempotencyKey, out _))
                throw new ArgumentException("Geçerli bir satış işlem anahtarı gereklidir.", nameof(idempotencyKey));

            var cartList = cartItems?.ToList() ?? new List<PosCartItem>();
            if (cartList.Count == 0)
                throw new InvalidOperationException("Sepet boş, satış tamamlanamaz.");
            if (warehouseId <= 0)
                throw new InvalidOperationException("Geçerli bir depo seçilmelidir.");
            if (cartList.Any(i => i.ProductId <= 0 || i.Quantity <= 0 || i.UnitPrice < 0))
                throw new InvalidOperationException("Satış kalemlerinde ürün, miktar veya fiyat bilgisi geçersiz.");

            var paymentList = payments?.ToList() ?? new List<PosPaymentEntry>();
            if (paymentList.Count == 0)
                throw new InvalidOperationException("Ödeme yöntemi girilmedi.");
            if (paymentList.Any(p => p.Amount <= 0))
                throw new InvalidOperationException("Ödeme tutarları sıfırdan büyük olmalıdır.");

            var grandTotal = cartList.Sum(i => i.LineTotal);
            if (grandTotal <= 0)
                throw new InvalidOperationException("Satış toplamı sıfırdan büyük olmalıdır.");
            if (paymentList.Sum(p => p.Amount) != grandTotal)
                throw new InvalidOperationException("Ödeme toplamı satış toplamına eşit olmalıdır.");

            using var context = await _dbContextFactory.CreateDbContextAsync();

            var existingSale = await context.SalesOrders
                .Include(s => s.Items)
                .Include(s => s.Payments)
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.IdempotencyKey == idempotencyKey);
            if (existingSale != null)
                return existingSale;

            using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                // 1. Verify Inventory Stock Levels (Atomic Check)
                var productIds = cartList.Select(i => i.ProductId).Distinct().ToList();
                var inventories = await context.Inventories
                    .Where(i => i.WarehouseId == warehouseId && i.ProductId.HasValue && productIds.Contains(i.ProductId.Value))
                    .ToListAsync();

                foreach (var productGroup in cartList.GroupBy(i => i.ProductId))
                {
                    var requestedQuantity = productGroup.Sum(i => i.Quantity);
                    var inv = inventories.FirstOrDefault(i => i.ProductId == productGroup.Key);
                    if (inv == null || inv.Quantity < requestedQuantity)
                    {
                        var available = inv?.Quantity ?? 0;
                        throw new InvalidOperationException($"'{productGroup.First().ProductName}' için yetersiz stok! Mevcut Stok: {available}, İstenen: {requestedQuantity}");
                    }
                }

                // 2. Generate Unique Order Number
                var orderNo = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{DateTime.UtcNow:HHmmss}-{Random.Shared.Next(1000, 9999)}";

                var subTotal = cartList.Sum(i => i.SubTotal);
                var discountTotal = cartList.Sum(i => i.DiscountAmount);
                var taxTotal = cartList.Sum(i => i.TaxAmount);
                var paymentSummary = string.Join(", ", paymentList.Select(p => $"{p.DisplayName}: {p.Amount:N2} ₺"));

                // 3. Build SalesOrder Header
                var salesOrder = new SalesOrder
                {
                    OrderNumber = orderNo,
                    IdempotencyKey = idempotencyKey,
                    CustomerId = customerId,
                    CustomerName = string.IsNullOrWhiteSpace(customerName) ? "Perakende Müşteri" : customerName,
                    Date = DateTime.UtcNow,
                    PaymentMethod = paymentSummary,
                    SubTotal = subTotal,
                    DiscountTotal = discountTotal,
                    TaxTotal = taxTotal,
                    TotalAmount = grandTotal,
                    Status = SalesOrderStatus.Completed,
                    Notes = string.IsNullOrWhiteSpace(notes) ? "POS Perakende Satış" : notes,
                    PrintCount = 0,
                    IsReprinted = false
                };

                // 4. Add SalesOrderItems & Update Inventory
                foreach (var item in cartList)
                {
                    salesOrder.Items.Add(new SalesOrderItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        DiscountPercent = item.DiscountPercent,
                        DiscountAmount = item.DiscountAmount,
                        TaxRate = item.TaxRate,
                        LineTotal = item.LineTotal
                    });

                    var inv = inventories.First(i => i.ProductId == item.ProductId);
                    inv.Quantity -= item.Quantity;
                }

                // 5. Add SalesOrderPayments & Record Cash/Bank Transactions
                decimal paidAmount = 0m;
                foreach (var p in paymentList)
                {
                    salesOrder.Payments.Add(new SalesOrderPayment
                    {
                        PaymentMethod = p.PaymentMethod,
                        Amount = p.Amount,
                        Reference = p.Reference ?? string.Empty
                    });

                    paidAmount += p.Amount;

                    if (p.PaymentMethod != PaymentMethod.OnAccount)
                    {
                        var cashTxType = p.PaymentMethod switch
                        {
                            PaymentMethod.Cash => CashTransactionType.CashIncome,
                            PaymentMethod.CreditCard => CashTransactionType.CardIncome,
                            PaymentMethod.BankTransfer => CashTransactionType.TransferIncome,
                            _ => CashTransactionType.Income
                        };

                        context.CashTransactions.Add(new CashTransaction
                        {
                            TransactionType = cashTxType,
                            Amount = p.Amount,
                            Date = DateTime.UtcNow,
                            Description = $"POS Satış #{orderNo} ({p.DisplayName})",
                            Category = "Perakende Satış",
                            PaymentMethod = p.PaymentMethod,
                            ReferenceNumber = p.Reference ?? string.Empty,
                            SalesOrderId = 0,
                            CustomerId = customerId,
                            CreatedBy = currentUserName ?? "Kasiyer",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                context.SalesOrders.Add(salesOrder);
                await context.SaveChangesAsync();

                // Assign SalesOrderId to CashTransactions
                var createdTransactions = context.CashTransactions.Local.Where(ct => ct.SalesOrderId == 0).ToList();
                foreach (var ct in createdTransactions)
                {
                    ct.SalesOrderId = salesOrder.Id;
                }

                // 6. Customer Ledger & Loyalty Update
                if (customerId.HasValue && customerId.Value > 0)
                {
                    var customer = await context.Customers.FirstOrDefaultAsync(c => c.Id == customerId.Value);
                    if (customer != null)
                    {
                        customer.TotalSpent += grandTotal;
                        customer.TotalPurchaseCount += 1;
                        customer.LastPurchaseDate = DateTime.UtcNow;
                        customer.LoyaltyPoints += (int)(grandTotal / 100m);

                        context.Transactions.Add(new Transaction
                        {
                            CustomerId = customer.Id,
                            Amount = grandTotal,
                            Date = DateTime.UtcNow,
                            Type = TransactionType.Debt,
                            Description = $"POS Satış Siparişi #{orderNo}"
                        });

                        if (paidAmount > 0)
                        {
                            context.Transactions.Add(new Transaction
                            {
                                CustomerId = customer.Id,
                                Amount = paidAmount,
                                Date = DateTime.UtcNow,
                                Type = TransactionType.Payment,
                                Description = $"POS Satış Tahsilatı #{orderNo} ({paymentSummary})"
                            });
                        }
                    }
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return salesOrder;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
