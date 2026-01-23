using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Stok Hareketleri Raporlama ViewModel
    /// </summary>
    public class StockReportsViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        
        // Filtreler
        private DateTime? _startDate;
        private DateTime? _endDate;
        private StockTransactionType? _selectedTransactionType;
        private Product? _selectedProduct;
        private Warehouse? _selectedWarehouse;
        private string _statusMessage = string.Empty;
        private bool _isLoading;

        // Koleksiyonlar
        public ObservableCollection<StockTransactionReportItem> Transactions { get; set; }
        public ObservableCollection<Warehouse> Warehouses { get; set; }
        public ObservableCollection<Product> Products { get; set; }
        public ObservableCollection<TransactionTypeItem> TransactionTypes { get; set; }

        #region Properties

        public DateTime? StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        public DateTime? EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        public StockTransactionType? SelectedTransactionType
        {
            get => _selectedTransactionType;
            set => SetProperty(ref _selectedTransactionType, value);
        }

        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set => SetProperty(ref _selectedProduct, value);
        }

        public Warehouse? SelectedWarehouse
        {
            get => _selectedWarehouse;
            set => SetProperty(ref _selectedWarehouse, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        #endregion

        #region Summary Properties

        private int _totalIn;
        private int _totalOut;
        private int _transactionCount;

        public int TotalIn
        {
            get => _totalIn;
            set => SetProperty(ref _totalIn, value);
        }

        public int TotalOut
        {
            get => _totalOut;
            set => SetProperty(ref _totalOut, value);
        }

        public int NetChange => TotalIn - TotalOut;

        public int TransactionCount
        {
            get => _transactionCount;
            set => SetProperty(ref _transactionCount, value);
        }

        #endregion

        public ICommand SearchCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand ExportCommand { get; }

        public StockReportsViewModel()
        {
            _context = new AppDbContext();
            
            Transactions = new ObservableCollection<StockTransactionReportItem>();
            Warehouses = new ObservableCollection<Warehouse>(_context.Warehouses.ToList());
            Products = new ObservableCollection<Product>(_context.Products.OrderBy(p => p.ProductName).ToList());
            
            // İşlem tipleri için Türkçe karşılıklar
            TransactionTypes = new ObservableCollection<TransactionTypeItem>
            {
                new() { Type = null, DisplayName = "Tümü" },
                new() { Type = StockTransactionType.Purchase, DisplayName = "Satınalma" },
                new() { Type = StockTransactionType.Sale, DisplayName = "Satış" },
                new() { Type = StockTransactionType.ServiceUsage, DisplayName = "Servis Kullanımı" },
                new() { Type = StockTransactionType.Transfer, DisplayName = "Transfer" },
                new() { Type = StockTransactionType.AdjustmentPlus, DisplayName = "Sayım Fazlası" },
                new() { Type = StockTransactionType.AdjustmentMinus, DisplayName = "Sayım Eksiği" },
                new() { Type = StockTransactionType.ReturnToSupplier, DisplayName = "Tedarikçiye İade" },
                new() { Type = StockTransactionType.ReturnFromCustomer, DisplayName = "Müşteriden İade" }
            };

            // Varsayılan tarih aralığı: Son 30 gün
            StartDate = DateTime.Today.AddDays(-30);
            EndDate = DateTime.Today;

            SearchCommand = new RelayCommand(_ => ExecuteSearch());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            ExportCommand = new RelayCommand(_ => ExportToClipboard(), _ => Transactions.Any());

            // İlk yükleme
            ExecuteSearch();
        }

        /// <summary>
        /// Filtrelere göre stok hareketlerini sorgular
        /// </summary>
        private void ExecuteSearch()
        {
            IsLoading = true;
            Transactions.Clear();

            try
            {
                var query = _context.StockTransactions
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
                    .Take(500) // Performans için limit
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
        /// Özet değerleri hesaplar
        /// </summary>
        private void CalculateSummaries(System.Collections.Generic.List<StockTransaction> transactions)
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
                StockTransactionType.Purchase => true,
                StockTransactionType.AdjustmentPlus => true,
                StockTransactionType.ReturnFromCustomer => true,
                StockTransactionType.Transfer => true, // Hedef için giriş
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
                StockTransactionType.Purchase => "Satınalma",
                StockTransactionType.Sale => "Satış",
                StockTransactionType.ServiceUsage => "Servis Kullanımı",
                StockTransactionType.Transfer => "Transfer",
                StockTransactionType.AdjustmentPlus => "Sayım Fazlası",
                StockTransactionType.AdjustmentMinus => "Sayım Eksiği",
                StockTransactionType.ReturnToSupplier => "Tedarikçiye İade",
                StockTransactionType.ReturnFromCustomer => "Müşteriden İade",
                _ => type.ToString()
            };
        }

        /// <summary>
        /// Filtreleri temizler
        /// </summary>
        private void ClearFilters()
        {
            StartDate = DateTime.Today.AddDays(-30);
            EndDate = DateTime.Today;
            SelectedTransactionType = null;
            SelectedProduct = null;
            SelectedWarehouse = null;
            ExecuteSearch();
        }

        /// <summary>
        /// Sonuçları clipboard'a kopyalar (basit export)
        /// </summary>
        private void ExportToClipboard()
        {
            try
            {
                var lines = new System.Collections.Generic.List<string>
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
