using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace KamatekCrm.ViewModels
{
    public enum StockCountFilter
    {
        All,
        DifferencesOnly,
        SurplusesOnly,
        ShortagesOnly,
        UncountedOnly
    }

    /// <summary>
    /// Fiziksel Stok Sayım işlemleri için yenilenmiş, yüksek performanslı ViewModel
    /// </summary>
    public partial class StockCountViewModel : ViewModelBase
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        
        private Warehouse? _selectedWarehouse;
        private DateTime _countDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        private string _statusMessage = string.Empty;
        private bool _isActionSuccessful;
        private bool _isLoading;
        private string _searchText = string.Empty;
        private bool _isHistoryVisible;
        private bool _isHistoryDetailVisible;

        // Barkod & Filtreleme Özellikleri
        private bool _isBarcodeScanMode;
        private string _barcodeScanInput = string.Empty;
        private StockCountFilter _selectedFilterMode = StockCountFilter.All;

        // Manuel Sayım alanları
        private string _manualSearchText = string.Empty;
        private Product? _selectedSearchResult;
        private Warehouse? _manualSelectedWarehouse;

        public ObservableCollection<Warehouse> Warehouses { get; set; }
        public ObservableCollection<StockCountItem> CountItems { get; set; }
        public ObservableCollection<CountHistoryItem> CountHistory { get; set; }
        public ObservableCollection<CountHistoryDetailItem> CountHistoryDetails { get; set; }

        // Manuel Sayım koleksiyonları
        public ObservableCollection<Product> ManualSearchResults { get; set; }
        public ObservableCollection<StockCountItem> ManualCountItems { get; set; }
        
        /// <summary>
        /// Filtreleme için CollectionView
        /// </summary>
        public ICollectionView CountItemsView { get; private set; }

        public Warehouse? SelectedWarehouse
        {
            get => _selectedWarehouse;
            set
            {
                if (_selectedWarehouse != value)
                {
                    if (CountItems.Any(i => i.Difference != 0))
                    {
                        var res = MessageBox.Show(
                            "Girilen sayım verileri var. Depo değiştirdiğinizde bu veriler sıfırlanacaktır. Devam etmek istiyor musunuz?",
                            "Uyarı",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (res != MessageBoxResult.Yes)
                        {
                            OnPropertyChanged(nameof(SelectedWarehouse));
                            return;
                        }
                    }

                    if (SetProperty(ref _selectedWarehouse, value))
                    {
                        _ = RefreshAsync();
                    }
                }
            }
        }

        public DateTime CountDate
        {
            get => _countDate;
            set => SetProperty(ref _countDate, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsActionSuccessful
        {
            get => _isActionSuccessful;
            set => SetProperty(ref _isActionSuccessful, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    CountItemsView?.Refresh();
                }
            }
        }

        public bool IsBarcodeScanMode
        {
            get => _isBarcodeScanMode;
            set => SetProperty(ref _isBarcodeScanMode, value);
        }

        public string BarcodeScanInput
        {
            get => _barcodeScanInput;
            set => SetProperty(ref _barcodeScanInput, value);
        }

        public StockCountFilter SelectedFilterMode
        {
            get => _selectedFilterMode;
            set
            {
                if (SetProperty(ref _selectedFilterMode, value))
                {
                    CountItemsView?.Refresh();
                }
            }
        }

        public bool IsHistoryVisible
        {
            get => _isHistoryVisible;
            set => SetProperty(ref _isHistoryVisible, value);
        }

        public bool IsHistoryDetailVisible
        {
            get => _isHistoryDetailVisible;
            set => SetProperty(ref _isHistoryDetailVisible, value);
        }

        // Metrik & Finansal Özetler
        public int TotalDifferenceCount => CountItems?.Count(i => i.Difference != 0) ?? 0;
        public int TotalPositiveDifference => CountItems?.Where(i => i.Difference > 0).Sum(i => i.Difference) ?? 0;
        public int TotalNegativeDifference => CountItems?.Where(i => i.Difference < 0).Sum(i => i.Difference) ?? 0;
        public int TotalItemCount => CountItems?.Count ?? 0;
        public decimal TotalFinancialDifference => CountItems?.Sum(i => i.FinancialDifference) ?? 0m;

        // Manuel Sayım Özellikleri
        public string ManualSearchText
        {
            get => _manualSearchText;
            set
            {
                if (SetProperty(ref _manualSearchText, value))
                {
                    _ = ExecuteSearchProductAsync();
                }
            }
        }

        public Product? SelectedSearchResult
        {
            get => _selectedSearchResult;
            set => SetProperty(ref _selectedSearchResult, value);
        }

        public Warehouse? ManualSelectedWarehouse
        {
            get => _manualSelectedWarehouse;
            set
            {
                if (_manualSelectedWarehouse != value)
                {
                    if (ManualCountItems.Any(i => i.Difference != 0))
                    {
                        var res = MessageBox.Show(
                            "Manuel sayım listesinde değişiklikler var. Depo değiştirdiğinizde temizlenecektir. Devam etmek istiyor musunuz?",
                            "Uyarı",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (res != MessageBoxResult.Yes)
                        {
                            OnPropertyChanged(nameof(ManualSelectedWarehouse));
                            return;
                        }
                    }

                    if (SetProperty(ref _manualSelectedWarehouse, value))
                    {
                        ManualCountItems.Clear();
                        UpdateManualTotals();
                    }
                }
            }
        }

        public int ManualTotalDifferenceCount => ManualCountItems?.Count(i => i.Difference != 0) ?? 0;
        public int ManualTotalPositiveDifference => ManualCountItems?.Where(i => i.Difference > 0).Sum(i => i.Difference) ?? 0;
        public int ManualTotalNegativeDifference => ManualCountItems?.Where(i => i.Difference < 0).Sum(i => i.Difference) ?? 0;
        public int ManualTotalItemCount => ManualCountItems?.Count ?? 0;
        public decimal ManualTotalFinancialDifference => ManualCountItems?.Sum(i => i.FinancialDifference) ?? 0m;

        public StockCountViewModel(IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
            
            Warehouses = new ObservableCollection<Warehouse>();
            CountItems = new ObservableCollection<StockCountItem>();
            CountHistory = new ObservableCollection<CountHistoryItem>();
            CountHistoryDetails = new ObservableCollection<CountHistoryDetailItem>();

            ManualSearchResults = new ObservableCollection<Product>();
            ManualCountItems = new ObservableCollection<StockCountItem>();

            CountItemsView = CollectionViewSource.GetDefaultView(CountItems);
            CountItemsView.Filter = FilterItems;

            _ = LoadWarehousesAsync();
        }

        private async Task LoadWarehousesAsync()
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var activeWarehouses = await context.Warehouses
                    .Where(w => w.IsActive)
                    .OrderBy(w => w.Name)
                    .ToListAsync();

                Warehouses.Clear();
                foreach (var w in activeWarehouses)
                {
                    Warehouses.Add(w);
                }

                if (Warehouses.Count > 0 && SelectedWarehouse == null)
                {
                    SelectedWarehouse = Warehouses[0];
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Depolar yüklenirken hata: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        private bool FilterItems(object obj)
        {
            if (obj is not StockCountItem item) return false;

            // 1. Text Filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                bool textMatches = item.ProductCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                   item.ProductName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                   item.ModelName.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
                if (!textMatches) return false;
            }

            // 2. Mode Filter
            return SelectedFilterMode switch
            {
                StockCountFilter.DifferencesOnly => item.Difference != 0,
                StockCountFilter.SurplusesOnly => item.Difference > 0,
                StockCountFilter.ShortagesOnly => item.Difference < 0,
                StockCountFilter.UncountedOnly => item.CountedQuantity == item.SystemQuantity,
                _ => true
            };
        }

        [RelayCommand]
        private void SetFilterMode(string modeStr)
        {
            if (Enum.TryParse<StockCountFilter>(modeStr, true, out var mode))
            {
                SelectedFilterMode = mode;
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            CountItems.Clear();
            StatusMessage = string.Empty;
            SearchText = string.Empty;

            if (SelectedWarehouse == null) return;

            IsLoading = true;

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var inventories = await context.Inventories
                    .Include(i => i.Product)
                    .Where(i => i.WarehouseId == SelectedWarehouse.Id)
                    .ToListAsync();

                foreach (var inv in inventories)
                {
                    if (inv.Product == null) continue;

                    var item = new StockCountItem
                    {
                        ProductId = inv.ProductId ?? 0,
                        ProductCode = inv.Product.SKU ?? $"P-{inv.ProductId:D4}",
                        ProductName = inv.Product.ProductName,
                        ModelName = inv.Product.ModelName ?? string.Empty,
                        Unit = inv.Product.Unit ?? "Adet",
                        SystemQuantity = inv.Quantity,
                        CountedQuantity = inv.Quantity,
                        PurchasePrice = inv.Product.PurchasePrice
                    };

                    item.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName is nameof(StockCountItem.Difference) or nameof(StockCountItem.FinancialDifference))
                        {
                            UpdateTotals();
                        }
                    };

                    CountItems.Add(item);
                }

                UpdateTotals();
                StatusMessage = $"{CountItems.Count} ürün başarıyla yüklendi.";
                IsActionSuccessful = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Yükleme hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateTotals()
        {
            OnPropertyChanged(nameof(TotalDifferenceCount));
            OnPropertyChanged(nameof(TotalPositiveDifference));
            OnPropertyChanged(nameof(TotalNegativeDifference));
            OnPropertyChanged(nameof(TotalItemCount));
            OnPropertyChanged(nameof(TotalFinancialDifference));
        }

        [RelayCommand]
        private void ProcessBarcodeScan()
        {
            if (string.IsNullOrWhiteSpace(BarcodeScanInput)) return;

            var code = BarcodeScanInput.Trim();

            var item = CountItems.FirstOrDefault(i =>
                i.ProductCode.Equals(code, StringComparison.OrdinalIgnoreCase) ||
                i.ProductName.Equals(code, StringComparison.OrdinalIgnoreCase));

            if (item != null)
            {
                item.CountedQuantity += 1;
                StatusMessage = $"✓ [{item.ProductCode}] {item.ProductName} sayımı arttırıldı: {item.CountedQuantity}";
                IsActionSuccessful = true;

                try { SystemSounds.Beep.Play(); } catch { }
            }
            else
            {
                StatusMessage = $"⚠️ Barkod / Stok Kodu bulunamadı: {code}";
                IsActionSuccessful = false;
                try { SystemSounds.Asterisk.Play(); } catch { }
            }

            BarcodeScanInput = string.Empty;
        }

        [RelayCommand]
        private async Task SaveCountAsync()
        {
            if (SelectedWarehouse == null) return;

            var itemsWithDifference = CountItems.Where(i => i.Difference != 0).ToList();
            if (!itemsWithDifference.Any())
            {
                StatusMessage = "Düzeltilecek fark bulunamadı.";
                IsActionSuccessful = false;
                return;
            }

            var result = MessageBox.Show(
                $"{itemsWithDifference.Count} üründe fark tespit edildi.\n\n" +
                $"Sayım Fazlası: +{TotalPositiveDifference} adet\n" +
                $"Sayım Eksiği: {TotalNegativeDifference} adet\n" +
                $"Net Finansal Sapma: {TotalFinancialDifference:C2}\n\n" +
                "Sayım kayıtları oluşturulacak ve stok güncellenecek.\nDevam etmek istiyor musunuz?",
                "Stok Sayım Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                using var transaction = await context.Database.BeginTransactionAsync();

                var batchRef = $"COUNT-{CountDate:yyyyMMdd-HHmmss}-{SelectedWarehouse.Id}";

                foreach (var item in itemsWithDifference)
                {
                    var inventory = await context.Inventories
                        .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.WarehouseId == SelectedWarehouse.Id);

                    if (inventory == null) continue;

                    var transactionType = item.Difference > 0 
                        ? StockTransactionType.AdjustmentPlus 
                        : StockTransactionType.AdjustmentMinus;

                    var stockTransaction = new StockTransaction
                    {
                        Date = DateTime.SpecifyKind(CountDate, DateTimeKind.Utc),
                        ProductId = item.ProductId,
                        SourceWarehouseId = item.Difference < 0 ? SelectedWarehouse.Id : null,
                        TargetWarehouseId = item.Difference > 0 ? SelectedWarehouse.Id : null,
                        Quantity = Math.Abs(item.Difference),
                        TransactionType = transactionType,
                        Description = $"Stok sayımı - {SelectedWarehouse.Name}. " +
                                      $"Sistem: {item.SystemQuantity}, Sayılan: {item.CountedQuantity}, Fark: {item.Difference}",
                        ReferenceId = batchRef
                    };

                    context.StockTransactions.Add(stockTransaction);
                    inventory.Quantity = item.CountedQuantity;
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                StatusMessage = $"Sayım başarıyla kaydedildi. {itemsWithDifference.Count} ürün güncellendi.";
                IsActionSuccessful = true;

                await RefreshAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Kayıt hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            if (CountItems.Count == 0)
            {
                StatusMessage = "Dışa aktarılacak veri bulunamadı.";
                IsActionSuccessful = false;
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
                FileName = $"StokSayim_{SelectedWarehouse?.Name ?? "Tum"}_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx",
                Title = "Stok Sayım Raporu Kaydet"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Stok Sayım");

                worksheet.Cell(1, 1).Value = "STOK SAYIM RAPORU";
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                worksheet.Range(1, 1, 1, 7).Merge();

                worksheet.Cell(2, 1).Value = $"Depo: {SelectedWarehouse?.Name ?? "Belirtilmemiş"}";
                worksheet.Cell(3, 1).Value = $"Tarih: {CountDate:dd.MM.yyyy}";

                int headerRow = 5;
                var headers = new[] { "Ürün Kodu", "Ürün Adı", "Birim", "Sistem Miktar", "Sayılan Miktar", "Fark", "Finansal Fark (TL)" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(headerRow, i + 1).Value = headers[i];
                    worksheet.Cell(headerRow, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(headerRow, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int dataRow = headerRow + 1;
                foreach (var item in CountItems)
                {
                    worksheet.Cell(dataRow, 1).Value = item.ProductCode;
                    worksheet.Cell(dataRow, 2).Value = item.ProductName;
                    worksheet.Cell(dataRow, 3).Value = item.Unit;
                    worksheet.Cell(dataRow, 4).Value = item.SystemQuantity;
                    worksheet.Cell(dataRow, 5).Value = item.CountedQuantity;
                    worksheet.Cell(dataRow, 6).Value = item.Difference;
                    worksheet.Cell(dataRow, 7).Value = item.FinancialDifference;

                    if (item.Difference > 0)
                        worksheet.Cell(dataRow, 6).Style.Font.FontColor = XLColor.Green;
                    else if (item.Difference < 0)
                        worksheet.Cell(dataRow, 6).Style.Font.FontColor = XLColor.Red;

                    dataRow++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(saveDialog.FileName);

                StatusMessage = "Excel dosyası oluşturuldu.";
                IsActionSuccessful = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Excel aktarım hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        [RelayCommand]
        private void ImportFromExcel()
        {
            if (CountItems.Count == 0)
            {
                StatusMessage = "Önce bir depo seçin ve ürünleri yükleyin.";
                IsActionSuccessful = false;
                return;
            }

            var openDialog = new OpenFileDialog
            {
                Filter = "Excel Dosyası (*.xlsx)|*.xlsx|Tüm Dosyalar (*.*)|*.*",
                Title = "Sayım Verilerini İçe Aktar"
            };

            if (openDialog.ShowDialog() != true) return;

            try
            {
                using var workbook = new XLWorkbook(openDialog.FileName);
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1);

                if (rows == null)
                {
                    StatusMessage = "Excel dosyasında veri bulunamadı.";
                    IsActionSuccessful = false;
                    return;
                }

                // O(1) arama için Hızlı İndeks Sözlüğü
                var skuLookup = CountItems
                    .Where(i => !string.IsNullOrWhiteSpace(i.ProductCode))
                    .ToDictionary(i => i.ProductCode.Trim(), i => i, StringComparer.OrdinalIgnoreCase);

                var modelLookup = CountItems
                    .Where(i => !string.IsNullOrWhiteSpace(i.ModelName))
                    .GroupBy(i => i.ModelName.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var nameLookup = CountItems
                    .Where(i => !string.IsNullOrWhiteSpace(i.ProductName))
                    .GroupBy(i => i.ProductName.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                int updatedCount = 0;
                var notFoundList = new List<string>();

                foreach (var row in rows)
                {
                    var searchKey = row.Cell(1).GetValue<string>()?.Trim();
                    var countedQtyStr = row.Cell(2).GetValue<string>();

                    if (string.IsNullOrWhiteSpace(searchKey) || !int.TryParse(countedQtyStr, out int countedQty))
                        continue;

                    StockCountItem? item = null;
                    if (skuLookup.TryGetValue(searchKey, out var matchedSku)) item = matchedSku;
                    else if (modelLookup.TryGetValue(searchKey, out var matchedModel)) item = matchedModel;
                    else if (nameLookup.TryGetValue(searchKey, out var matchedName)) item = matchedName;

                    if (item != null)
                    {
                        item.CountedQuantity = countedQty;
                        updatedCount++;
                    }
                    else
                    {
                        notFoundList.Add(searchKey);
                    }
                }

                UpdateTotals();
                CountItemsView?.Refresh();

                StatusMessage = $"{updatedCount} ürün Excel'den yüklendi. ({notFoundList.Count} tanınmayan ürün)";
                IsActionSuccessful = updatedCount > 0;
            }
            catch (Exception ex)
            {
                StatusMessage = $"İçe aktarım hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        [RelayCommand]
        private async Task ShowHistoryAsync()
        {
            try
            {
                CountHistory.Clear();

                using var context = await _dbContextFactory.CreateDbContextAsync();
                var adjustmentTypes = new[] { StockTransactionType.AdjustmentPlus, StockTransactionType.AdjustmentMinus };

                var transactions = await context.StockTransactions
                    .Include(t => t.SourceWarehouse)
                    .Include(t => t.TargetWarehouse)
                    .Where(t => adjustmentTypes.Contains(t.TransactionType) 
                             && t.ReferenceId != null 
                             && t.ReferenceId.StartsWith("COUNT-"))
                    .OrderByDescending(t => t.Date)
                    .ToListAsync();

                var grouped = transactions
                    .GroupBy(t => t.ReferenceId)
                    .Select(g =>
                    {
                        var first = g.First();
                        var warehouseName = first.TargetWarehouse?.Name ?? first.SourceWarehouse?.Name ?? "Bilinmiyor";
                        var totalPlus = g.Where(t => t.TransactionType == StockTransactionType.AdjustmentPlus).Sum(t => t.Quantity);
                        var totalMinus = g.Where(t => t.TransactionType == StockTransactionType.AdjustmentMinus).Sum(t => t.Quantity);

                        return new CountHistoryItem
                        {
                            Date = first.Date,
                            WarehouseName = warehouseName,
                            ProductCount = g.Count(),
                            TotalDifference = totalPlus - totalMinus,
                            ReferenceId = first.ReferenceId ?? ""
                        };
                    })
                    .Take(30)
                    .ToList();

                foreach (var item in grouped)
                {
                    CountHistory.Add(item);
                }

                IsHistoryVisible = true;
                IsHistoryDetailVisible = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Geçmiş yüklenirken hata: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        [RelayCommand]
        private async Task ViewHistoryDetailAsync(CountHistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.ReferenceId)) return;

            try
            {
                CountHistoryDetails.Clear();

                using var context = await _dbContextFactory.CreateDbContextAsync();
                var details = await context.StockTransactions
                    .Include(t => t.Product)
                    .Where(t => t.ReferenceId == item.ReferenceId)
                    .ToListAsync();

                foreach (var d in details)
                {
                    CountHistoryDetails.Add(new CountHistoryDetailItem
                    {
                        ProductCode = d.Product?.SKU ?? $"P-{d.ProductId}",
                        ProductName = d.Product?.ProductName ?? "Ürün",
                        Quantity = d.TransactionType == StockTransactionType.AdjustmentPlus ? d.Quantity : -d.Quantity,
                        Description = d.Description ?? ""
                    });
                }

                IsHistoryDetailVisible = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Detay yükleme hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        [RelayCommand]
        private void CloseHistoryDetail()
        {
            IsHistoryDetailVisible = false;
        }

        // =============================================
        // === MANUEL SAYIM METODLARI ===
        // =============================================

        private async Task ExecuteSearchProductAsync()
        {
            ManualSearchResults.Clear();

            if (string.IsNullOrWhiteSpace(ManualSearchText) || ManualSearchText.Length < 2)
                return;

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var results = await context.Products
                    .Where(p =>
                        (p.SKU != null && p.SKU.Contains(ManualSearchText)) ||
                        (p.Barcode != null && p.Barcode.Contains(ManualSearchText)) ||
                        p.ProductName.Contains(ManualSearchText) ||
                        (p.ModelName != null && p.ModelName.Contains(ManualSearchText)))
                    .Take(15)
                    .ToListAsync();

                foreach (var product in results)
                {
                    ManualSearchResults.Add(product);
                }

                if (ManualSearchResults.Count == 1)
                {
                    SelectedSearchResult = ManualSearchResults[0];
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Arama hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        [RelayCommand]
        private async Task AddToManualCountAsync()
        {
            if (ManualSelectedWarehouse == null)
            {
                StatusMessage = "Lütfen önce bir depo seçin.";
                IsActionSuccessful = false;
                return;
            }

            var product = SelectedSearchResult ?? ManualSearchResults.FirstOrDefault();
            if (product == null)
            {
                StatusMessage = "Eklenecek ürün bulunamadı.";
                IsActionSuccessful = false;
                return;
            }

            if (ManualCountItems.Any(i => i.ProductId == product.Id))
            {
                StatusMessage = $"'{product.ProductName}' zaten listede.";
                IsActionSuccessful = false;
                return;
            }

            int systemQty = 0;
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var inventory = await context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == product.Id && i.WarehouseId == ManualSelectedWarehouse.Id);
                systemQty = inventory?.Quantity ?? 0;
            }
            catch { }

            var item = new StockCountItem
            {
                ProductId = product.Id,
                ProductCode = product.SKU ?? $"P-{product.Id:D4}",
                ProductName = product.ProductName,
                ModelName = product.ModelName ?? string.Empty,
                Unit = product.Unit ?? "Adet",
                SystemQuantity = systemQty,
                CountedQuantity = 0,
                PurchasePrice = product.PurchasePrice
            };

            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName is nameof(StockCountItem.Difference) or nameof(StockCountItem.FinancialDifference))
                {
                    UpdateManualTotals();
                }
            };

            ManualCountItems.Add(item);
            UpdateManualTotals();

            ManualSearchText = string.Empty;
            SelectedSearchResult = null;
            ManualSearchResults.Clear();

            StatusMessage = $"'{product.ProductName}' manuel listeye eklendi.";
            IsActionSuccessful = true;
        }

        [RelayCommand]
        private void RemoveFromManualCount(object? parameter)
        {
            if (parameter is StockCountItem item)
            {
                ManualCountItems.Remove(item);
                UpdateManualTotals();
                StatusMessage = $"'{item.ProductName}' listeden çıkarıldı.";
                IsActionSuccessful = true;
            }
        }

        [RelayCommand]
        private async Task ConfirmManualCountAsync()
        {
            if (ManualSelectedWarehouse == null) return;

            var itemsWithDifference = ManualCountItems.Where(i => i.Difference != 0).ToList();
            if (!itemsWithDifference.Any())
            {
                StatusMessage = "Düzeltilecek fark bulunamadı.";
                IsActionSuccessful = false;
                return;
            }

            var result = MessageBox.Show(
                $"{itemsWithDifference.Count} üründe fark tespit edildi.\n\n" +
                $"Sayım Fazlası: +{ManualTotalPositiveDifference} adet\n" +
                $"Sayım Eksiği: {ManualTotalNegativeDifference} adet\n\n" +
                "Sayım kayıtları oluşturulacak ve stok güncellenecek.\nDevam etmek istiyor musunuz?",
                "Manuel Sayım Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                using var transaction = await context.Database.BeginTransactionAsync();

                var referenceId = $"MANUAL-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{ManualSelectedWarehouse.Id}";

                foreach (var item in itemsWithDifference)
                {
                    var inventory = await context.Inventories
                        .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.WarehouseId == ManualSelectedWarehouse.Id);

                    if (inventory == null)
                    {
                        inventory = new Inventory
                        {
                            ProductId = item.ProductId,
                            WarehouseId = ManualSelectedWarehouse.Id,
                            Quantity = 0
                        };
                        context.Inventories.Add(inventory);
                    }

                    var transactionType = item.Difference > 0
                        ? StockTransactionType.AdjustmentPlus
                        : StockTransactionType.AdjustmentMinus;

                    var stockTransaction = new StockTransaction
                    {
                        Date = DateTime.UtcNow,
                        ProductId = item.ProductId,
                        SourceWarehouseId = item.Difference < 0 ? ManualSelectedWarehouse.Id : null,
                        TargetWarehouseId = item.Difference > 0 ? ManualSelectedWarehouse.Id : null,
                        Quantity = Math.Abs(item.Difference),
                        TransactionType = transactionType,
                        Description = $"Manuel sayım - {ManualSelectedWarehouse.Name}. " +
                                      $"Sistem: {item.SystemQuantity}, Sayılan: {item.CountedQuantity}, Fark: {item.Difference}",
                        ReferenceId = referenceId
                    };

                    context.StockTransactions.Add(stockTransaction);
                    inventory.Quantity = item.CountedQuantity;
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                StatusMessage = $"Manuel sayım kaydedildi. {itemsWithDifference.Count} ürün güncellendi.";
                IsActionSuccessful = true;

                ManualCountItems.Clear();
                UpdateManualTotals();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Manuel sayım kayıt hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ClearManualList()
        {
            if (ManualCountItems.Count == 0) return;

            var result = MessageBox.Show(
                "Sayım listesi temizlenecek. Devam etmek istiyor musunuz?",
                "Listeyi Temizle",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ManualCountItems.Clear();
                UpdateManualTotals();
                StatusMessage = "Liste temizlendi.";
                IsActionSuccessful = true;
            }
        }

        private void UpdateManualTotals()
        {
            OnPropertyChanged(nameof(ManualTotalDifferenceCount));
            OnPropertyChanged(nameof(ManualTotalPositiveDifference));
            OnPropertyChanged(nameof(ManualTotalNegativeDifference));
            OnPropertyChanged(nameof(ManualTotalItemCount));
            OnPropertyChanged(nameof(ManualTotalFinancialDifference));
        }

        [RelayCommand]
        private void CloseHistory()
        {
            IsHistoryVisible = false;
            IsHistoryDetailVisible = false;
        }
    }

    public class StockCountItem : INotifyPropertyChanged
    {
        private int _countedQuantity;

        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string Unit { get; set; } = "Adet";
        public int SystemQuantity { get; set; }
        public decimal PurchasePrice { get; set; }

        public int CountedQuantity
        {
            get => _countedQuantity;
            set
            {
                if (_countedQuantity != value)
                {
                    _countedQuantity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Difference));
                    OnPropertyChanged(nameof(FinancialDifference));
                }
            }
        }

        public int Difference => CountedQuantity - SystemQuantity;
        public decimal FinancialDifference => Difference * PurchasePrice;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CountHistoryItem
    {
        public DateTime Date { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public int TotalDifference { get; set; }
        public string ReferenceId { get; set; } = string.Empty;
    }

    public class CountHistoryDetailItem
    {
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
