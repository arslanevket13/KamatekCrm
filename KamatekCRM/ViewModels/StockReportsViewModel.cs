using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Stok Hareketleri Raporlama ViewModel
    /// </summary>
    public partial class StockReportsViewModel : ObservableObject
    {
        private AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        
        // Filtreler
        [ObservableProperty] private DateTime? _startDate;
        [ObservableProperty] private DateTime? _endDate;
        [ObservableProperty] private StockTransactionType? _selectedTransactionType;
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private Warehouse? _selectedWarehouse;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isLoading;

        // Koleksiyonlar
        public ObservableCollection<StockTransactionReportItem> Transactions { get; set; }
        public ObservableCollection<CriticalStockReportItem> CriticalStockItems { get; set; } = new();
        public ObservableCollection<DeadStockReportItem> DeadStockItems { get; set; } = new();
        public ObservableCollection<Warehouse> Warehouses { get; set; }
        public ObservableCollection<Product> Products { get; set; }
        public ObservableCollection<TransactionTypeItem> TransactionTypes { get; set; }

        #region Summary Properties

        [ObservableProperty] private int _totalIn;
        [ObservableProperty] private int _totalOut;
        [ObservableProperty] private int _transactionCount;

        public int NetChange => TotalIn - TotalOut;

        [ObservableProperty] private int _criticalStockCount;
        [ObservableProperty] private int _deadStockCount;
        [ObservableProperty] private decimal _deadStockTotalValue;
        [ObservableProperty] private decimal _totalInventoryValuation;

        #endregion

        public StockReportsViewModel(IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
            _context = _dbContextFactory.CreateDbContext();
            
            Transactions = new ObservableCollection<StockTransactionReportItem>();
            Warehouses = new ObservableCollection<Warehouse>(_context.Warehouses.AsNoTracking().ToList());
            Products = new ObservableCollection<Product>(_context.Products.AsNoTracking().OrderBy(p => p.ProductName).ToList());
            
            TransactionTypes = new ObservableCollection<TransactionTypeItem>
            {
                new() { Type = null, DisplayName = "Tümü" },
                new() { Type = StockTransactionType.OpeningStock, DisplayName = "Açılış / Devir Stoğu" },
                new() { Type = StockTransactionType.Purchase, DisplayName = "Satınalma" },
                new() { Type = StockTransactionType.Sale, DisplayName = "Satış" },
                new() { Type = StockTransactionType.ServiceUsage, DisplayName = "Servis Kullanımı" },
                new() { Type = StockTransactionType.Transfer, DisplayName = "Transfer" },
                new() { Type = StockTransactionType.AdjustmentPlus, DisplayName = "Sayım / Düzeltme Fazlası" },
                new() { Type = StockTransactionType.AdjustmentMinus, DisplayName = "Sayım / Düzeltme Eksiği" },
                new() { Type = StockTransactionType.ReturnToSupplier, DisplayName = "Tedarikçiye İade" },
                new() { Type = StockTransactionType.ReturnFromCustomer, DisplayName = "Müşteriden İade" }
            };

            // Varsayılan tarih aralığı: Son 30 gün
            StartDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc).AddDays(-30);
            EndDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

            // İlk yükleme
            Search();
            _ = RefreshStockAnalysisAsync();
        }

        /// <summary>
        /// Filtrelere göre stok hareketlerini sorgular
        /// </summary>
        [RelayCommand]
        private void Search()
        {
            IsLoading = true;
            Transactions.Clear();

            try
            {
                var query = _context.StockTransactions.AsNoTracking()
                    .Include(t => t.Product)
                    .Include(t => t.SourceWarehouse)
                    .Include(t => t.TargetWarehouse)
                    .AsQueryable();

                // Tarih filtreleri
                if (StartDate.HasValue)
                    query = query.Where(t => t.Date >= StartDate.Value.Date);
                
                if (EndDate.HasValue)
                    query = query.Where(t => t.Date <= EndDate.Value.Date.AddDays(1).AddSeconds(-1));

                // İşlem tipi filtresi
                if (SelectedTransactionType.HasValue)
                    query = query.Where(t => t.TransactionType == SelectedTransactionType.Value);

                // Ürün filtresi
                if (SelectedProduct != null)
                    query = query.Where(t => t.ProductId == SelectedProduct.Id);

                // Depo filtresi (kaynak veya hedef)
                if (SelectedWarehouse != null)
                    query = query.Where(t => t.SourceWarehouseId == SelectedWarehouse.Id || 
                                             t.TargetWarehouseId == SelectedWarehouse.Id);

                // Sonuçları getir ve dönüştür
                var results = query
                    .OrderByDescending(t => t.Date)
                    .ThenByDescending(t => t.Id)
                    .Take(500)
                    .ToList();

                foreach (var tx in results)
                {
                    Transactions.Add(new StockTransactionReportItem
                    {
                        Id = tx.Id,
                        Date = tx.Date,
                        TransactionType = tx.TransactionType,
                        TransactionTypeName = GetTransactionTypeName(tx.TransactionType),
                        ProductCode = tx.Product?.SKU ?? $"P-{tx.ProductId:D4}",
                        ProductName = tx.Product?.ProductName ?? "Bilinmeyen Ürün",
                        SourceWarehouseName = tx.SourceWarehouse?.Name ?? "-",
                        TargetWarehouseName = tx.TargetWarehouse?.Name ?? "-",
                        Quantity = tx.Quantity,
                        UnitCost = tx.UnitCost,
                        ReferenceId = tx.ReferenceId ?? "-",
                        Description = tx.Description ?? "",
                        IsIncoming = IsIncomingTransaction(tx.TransactionType)
                    });
                }

                // Özet hesaplamaları
                CalculateSummaries(results);

                TransactionCount = Transactions.Count;
                StatusMessage = $"{TransactionCount} kayıt bulundu.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Hata: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Kritik stoklar, önerilen sipariş miktarları ve son 90 gün hareketsiz (dead) stok analizini hesaplar
        /// </summary>
        [RelayCommand]
        public async Task RefreshStockAnalysisAsync()
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();

                var products = await context.Products.AsNoTracking()
                    .Include(p => p.Inventories)
                    .ToListAsync();

                foreach (var p in products)
                {
                    p.TotalStockQuantity = p.Inventories?.Sum(i => i.Quantity) ?? 0;
                }

                // 1. Kritik Stok Analizi
                CriticalStockItems.Clear();
                var criticals = products
                    .Where(p => p.MinStockLevel > 0 && p.TotalStockQuantity <= p.MinStockLevel)
                    .OrderBy(p => p.TotalStockQuantity)
                    .ToList();

                foreach (var p in criticals)
                {
                    int suggestedQty = Math.Max(1, (p.MinStockLevel * 2) - Math.Max(0, p.TotalStockQuantity));
                    CriticalStockItems.Add(new CriticalStockReportItem
                    {
                        ProductId = p.Id,
                        ProductCode = p.SKU,
                        ProductName = p.ProductName,
                        CurrentStock = p.TotalStockQuantity,
                        MinStockLevel = p.MinStockLevel,
                        SuggestedOrderQuantity = suggestedQty,
                        UnitCost = p.PurchasePrice,
                        EstimatedCost = suggestedQty * p.PurchasePrice
                    });
                }
                CriticalStockCount = CriticalStockItems.Count;

                // 2. Hareketsiz (Dead) Stok Analizi (Son 90 gün)
                DeadStockItems.Clear();
                var cutoffDate = DateTime.UtcNow.AddDays(-90);

                var outgoingTxTypes = new[]
                {
                    StockTransactionType.Sale,
                    StockTransactionType.ServiceUsage,
                    StockTransactionType.AdjustmentMinus,
                    StockTransactionType.ReturnToSupplier
                };

                var activeProductIds = await context.StockTransactions.AsNoTracking()
                    .Where(t => t.Date >= cutoffDate && outgoingTxTypes.Contains(t.TransactionType) && t.ProductId.HasValue)
                    .Select(t => t.ProductId!.Value)
                    .Distinct()
                    .ToListAsync();

                var activeSet = new HashSet<int>(activeProductIds);

                var deadCandidates = products
                    .Where(p => p.TotalStockQuantity > 0 && !activeSet.Contains(p.Id))
                    .ToList();

                decimal deadVal = 0m;
                foreach (var p in deadCandidates)
                {
                    var lastTxDate = await context.StockTransactions.AsNoTracking()
                        .Where(t => t.ProductId == p.Id && outgoingTxTypes.Contains(t.TransactionType))
                        .OrderByDescending(t => t.Date)
                        .Select(t => (DateTime?)t.Date)
                        .FirstOrDefaultAsync();

                    int daysWithoutMovement = lastTxDate.HasValue
                        ? (int)(DateTime.UtcNow - lastTxDate.Value).TotalDays
                        : 90;

                    decimal itemValue = Math.Max(0, p.TotalStockQuantity) * p.PurchasePrice;
                    deadVal += itemValue;

                    DeadStockItems.Add(new DeadStockReportItem
                    {
                        ProductId = p.Id,
                        ProductCode = p.SKU,
                        ProductName = p.ProductName,
                        CurrentStock = p.TotalStockQuantity,
                        UnitCost = p.PurchasePrice,
                        TotalValue = itemValue,
                        LastOutgoingDate = lastTxDate,
                        DaysWithoutMovement = Math.Max(90, daysWithoutMovement)
                    });
                }

                DeadStockCount = DeadStockItems.Count;
                DeadStockTotalValue = deadVal;

                // 3. Negatif stok korumalı toplam envanter maliyet değerlemesi
                TotalInventoryValuation = products.Sum(p => Math.Max(0, p.TotalStockQuantity) * p.PurchasePrice);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Stok analiz hatası: {ex.Message}";
            }
        }

        /// <summary>
        /// Özet değerleri hesaplar
        /// </summary>
        private void CalculateSummaries(List<StockTransaction> transactions)
        {
            TotalIn = 0;
            TotalOut = 0;

            foreach (var tx in transactions)
            {
                if (IsIncomingTransaction(tx.TransactionType))
                    TotalIn += tx.Quantity;
                else
                    TotalOut += tx.Quantity;
            }

            OnPropertyChanged(nameof(NetChange));
        }

        /// <summary>
        /// İşlemin giriş mi çıkış mı olduğunu belirler
        /// </summary>
        private bool IsIncomingTransaction(StockTransactionType type)
        {
            return type switch
            {
                StockTransactionType.OpeningStock => true,
                StockTransactionType.Purchase => true,
                StockTransactionType.AdjustmentPlus => true,
                StockTransactionType.ReturnFromCustomer => true,
                StockTransactionType.Transfer => true,
                _ => false
            };
        }

        /// <summary>
        /// İşlem tipinin Türkçe karşılığını döner
        /// </summary>
        private string GetTransactionTypeName(StockTransactionType type)
        {
            return type switch
            {
                StockTransactionType.OpeningStock => "Açılış / Devir Stoğu",
                StockTransactionType.Purchase => "Satınalma",
                StockTransactionType.Sale => "Satış",
                StockTransactionType.ServiceUsage => "Servis Kullanımı",
                StockTransactionType.Transfer => "Transfer",
                StockTransactionType.AdjustmentPlus => "Sayım / Düzeltme Fazlası",
                StockTransactionType.AdjustmentMinus => "Sayım / Düzeltme Eksiği",
                StockTransactionType.ReturnToSupplier => "Tedarikçiye İade",
                StockTransactionType.ReturnFromCustomer => "Müşteriden İade",
                _ => type.ToString()
            };
        }

        /// <summary>
        /// Filtreleri temizler
        /// </summary>
        [RelayCommand]
        private void ClearFilters()
        {
            StartDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc).AddDays(-30);
            EndDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            SelectedTransactionType = null;
            SelectedProduct = null;
            SelectedWarehouse = null;
            Search();
        }

        private bool CanExport() => Transactions != null && Transactions.Any();

        /// <summary>
        /// Sonuçları clipboard'a kopyalar (basit export)
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExport))]
        private void Export()
        {
            try
            {
                var lines = new List<string>
                {
                    "Tarih\tİşlem Tipi\tÜrün Kodu\tÜrün Adı\tKaynak\tHedef\tMiktar\tReferans"
                };

                foreach (var tx in Transactions)
                {
                    lines.Add($"{tx.Date:dd.MM.yyyy}\t{tx.TransactionTypeName}\t{tx.ProductCode}\t{tx.ProductName}\t{tx.SourceWarehouseName}\t{tx.TargetWarehouseName}\t{tx.Quantity}\t{tx.ReferenceId}");
                }

                System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, lines));
                StatusMessage = "Veriler panoya kopyalandı!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export hatası: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Kritik stok seviyesi ve önerilen sipariş kalemi
    /// </summary>
    public class CriticalStockReportItem
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int MinStockLevel { get; set; }
        public int SuggestedOrderQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal EstimatedCost { get; set; }
    }

    /// <summary>
    /// Hareketsiz (Dead) stok analiz kalemi
    /// </summary>
    public class DeadStockReportItem
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalValue { get; set; }
        public DateTime? LastOutgoingDate { get; set; }
        public int DaysWithoutMovement { get; set; }
    }

    /// <summary>
    /// İşlem tipi dropdown için wrapper sınıfı
    /// </summary>
    public class TransactionTypeItem
    {
        public StockTransactionType? Type { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Rapor görünümü için stok hareketi wrapper sınıfı
    /// </summary>
    public class StockTransactionReportItem
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public StockTransactionType TransactionType { get; set; }
        public string TransactionTypeName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string SourceWarehouseName { get; set; } = "-";
        public string TargetWarehouseName { get; set; } = "-";
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public string ReferenceId { get; set; } = "-";
        public string Description { get; set; } = string.Empty;
        public bool IsIncoming { get; set; }
    }
}
