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
using KamatekCrm.ApplicationCore.DTOs.Inventory;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;

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
        private readonly IStockCountCommandService _stockCountCommandService;
        private readonly IStockCountReadService _stockCountReadService;
        private readonly IDialogService _dialogService;
        private Guid _currentCountIdempotencyKey = Guid.NewGuid();
        private Guid _manualCountIdempotencyKey = Guid.NewGuid();
        private CancellationTokenSource? _manualSearchCts;
        private bool _acceptWarehouseChange;
        private bool _acceptManualWarehouseChange;
        
        private StockCountWarehouseDto? _selectedWarehouse;
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
        private StockCountProductDto? _selectedSearchResult;
        private StockCountWarehouseDto? _manualSelectedWarehouse;

        public ObservableCollection<StockCountWarehouseDto> Warehouses { get; set; }
        public ObservableCollection<StockCountItem> CountItems { get; set; }
        public ObservableCollection<StockCountHistoryDto> CountHistory { get; set; }
        public ObservableCollection<StockCountHistoryLineDto> CountHistoryDetails { get; set; }

        // Manuel Sayım koleksiyonları
        public ObservableCollection<StockCountProductDto> ManualSearchResults { get; set; }
        public ObservableCollection<StockCountItem> ManualCountItems { get; set; }
        
        /// <summary>
        /// Filtreleme için CollectionView
        /// </summary>
        public ICollectionView CountItemsView { get; private set; }

        public StockCountWarehouseDto? SelectedWarehouse
        {
            get => _selectedWarehouse;
            set
            {
                if (_selectedWarehouse != value)
                {
                    if (!_acceptWarehouseChange && CountItems.Any(i => i.IsCounted))
                    {
                        _ = ConfirmWarehouseChangeAsync(value);
                        OnPropertyChanged(nameof(SelectedWarehouse));
                        return;
                    }

                    if (SetProperty(ref _selectedWarehouse, value))
                    {
                        _ = RefreshCoreAsync();
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
        public int TotalDifferenceCount => CountItems?.Count(i => i.IsCounted && i.Difference != 0) ?? 0;
        public int TotalPositiveDifference => CountItems?.Where(i => i.IsCounted && i.Difference > 0).Sum(i => i.Difference) ?? 0;
        public int TotalNegativeDifference => CountItems?.Where(i => i.IsCounted && i.Difference < 0).Sum(i => i.Difference) ?? 0;
        public int TotalItemCount => CountItems?.Count ?? 0;
        public decimal TotalFinancialDifference => CountItems?.Where(i => i.IsCounted).Sum(i => i.FinancialDifference) ?? 0m;

        // Manuel Sayım Özellikleri
        public string ManualSearchText
        {
            get => _manualSearchText;
            set
            {
                if (SetProperty(ref _manualSearchText, value))
                {
                    _manualSearchCts?.Cancel();
                    _manualSearchCts = new CancellationTokenSource();
                    _ = DebounceManualSearchAsync(_manualSearchCts.Token);
                }
            }
        }

        public StockCountProductDto? SelectedSearchResult
        {
            get => _selectedSearchResult;
            set => SetProperty(ref _selectedSearchResult, value);
        }

        public StockCountWarehouseDto? ManualSelectedWarehouse
        {
            get => _manualSelectedWarehouse;
            set
            {
                if (_manualSelectedWarehouse != value)
                {
                    if (!_acceptManualWarehouseChange && ManualCountItems.Any(i => i.IsCounted))
                    {
                        _ = ConfirmManualWarehouseChangeAsync(value);
                        OnPropertyChanged(nameof(ManualSelectedWarehouse));
                        return;
                    }

                    if (SetProperty(ref _manualSelectedWarehouse, value))
                    {
                        ManualCountItems.Clear();
                        _manualCountIdempotencyKey = Guid.NewGuid();
                        UpdateManualTotals();
                    }
                }
            }
        }

        public int ManualTotalDifferenceCount => ManualCountItems?.Count(i => i.IsCounted && i.Difference != 0) ?? 0;
        public int ManualTotalPositiveDifference => ManualCountItems?.Where(i => i.IsCounted && i.Difference > 0).Sum(i => i.Difference) ?? 0;
        public int ManualTotalNegativeDifference => ManualCountItems?.Where(i => i.IsCounted && i.Difference < 0).Sum(i => i.Difference) ?? 0;
        public int ManualTotalItemCount => ManualCountItems?.Count ?? 0;
        public decimal ManualTotalFinancialDifference => ManualCountItems?.Where(i => i.IsCounted).Sum(i => i.FinancialDifference) ?? 0m;

        public StockCountViewModel(
            IStockCountCommandService stockCountCommandService,
            IStockCountReadService stockCountReadService,
            IDialogService dialogService)
        {
            _stockCountCommandService = stockCountCommandService;
            _stockCountReadService = stockCountReadService;
            _dialogService = dialogService;
            
            Warehouses = new ObservableCollection<StockCountWarehouseDto>();
            CountItems = new ObservableCollection<StockCountItem>();
            CountHistory = new ObservableCollection<StockCountHistoryDto>();
            CountHistoryDetails = new ObservableCollection<StockCountHistoryLineDto>();

            ManualSearchResults = new ObservableCollection<StockCountProductDto>();
            ManualCountItems = new ObservableCollection<StockCountItem>();

            CountItemsView = CollectionViewSource.GetDefaultView(CountItems);
            CountItemsView.Filter = FilterItems;

            _ = LoadWarehousesAsync();
        }

        private async Task LoadWarehousesAsync()
        {
            try
            {
                var result = await _stockCountReadService.GetWarehousesAsync();
                if (result.IsFailure)
                {
                    StatusMessage = result.Error;
                    IsActionSuccessful = false;
                    return;
                }

                Warehouses.Clear();
                foreach (var warehouse in result.Value!)
                {
                    Warehouses.Add(warehouse);
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

        private async Task ConfirmWarehouseChangeAsync(StockCountWarehouseDto? warehouse)
        {
            bool confirmed = await _dialogService.ShowConfirmationAsync(
                "Girilen sayım verileri var. Depo değiştirildiğinde bu veriler sıfırlanacaktır. Devam etmek istiyor musunuz?",
                "Depo Değişikliği");
            if (!confirmed)
            {
                OnPropertyChanged(nameof(SelectedWarehouse));
                return;
            }

            _acceptWarehouseChange = true;
            try { SelectedWarehouse = warehouse; }
            finally { _acceptWarehouseChange = false; }
        }

        private async Task ConfirmManualWarehouseChangeAsync(StockCountWarehouseDto? warehouse)
        {
            bool confirmed = await _dialogService.ShowConfirmationAsync(
                "Manuel sayım listesinde değişiklikler var. Depo değiştirildiğinde liste temizlenecektir. Devam etmek istiyor musunuz?",
                "Depo Değişikliği");
            if (!confirmed)
            {
                OnPropertyChanged(nameof(ManualSelectedWarehouse));
                return;
            }

            _acceptManualWarehouseChange = true;
            try { ManualSelectedWarehouse = warehouse; }
            finally { _acceptManualWarehouseChange = false; }
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
                StockCountFilter.DifferencesOnly => item.IsCounted && item.Difference != 0,
                StockCountFilter.SurplusesOnly => item.IsCounted && item.Difference > 0,
                StockCountFilter.ShortagesOnly => item.IsCounted && item.Difference < 0,
                StockCountFilter.UncountedOnly => !item.IsCounted,
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
            if (CountItems.Any(item => item.IsCounted))
            {
                bool confirmed = await _dialogService.ShowConfirmationAsync(
                    "Girilen sayım verileri temizlenecek ve güncel stok yeniden yüklenecek. Devam etmek istiyor musunuz?",
                    "Sayımı Yenile");
                if (!confirmed) return;
            }

            await RefreshCoreAsync();
        }

        private async Task RefreshCoreAsync()
        {
            CountItems.Clear();
            StatusMessage = string.Empty;
            SearchText = string.Empty;

            if (SelectedWarehouse == null) return;

            IsLoading = true;

            try
            {
                var result = await _stockCountReadService.GetWarehouseSnapshotAsync(SelectedWarehouse.Id);
                if (result.IsFailure)
                {
                    StatusMessage = result.Error;
                    IsActionSuccessful = false;
                    return;
                }

                foreach (var product in result.Value!)
                {
                    var item = new StockCountItem
                    {
                        ProductId = product.ProductId,
                        ProductCode = string.IsNullOrWhiteSpace(product.ProductCode) ? $"P-{product.ProductId:D4}" : product.ProductCode,
                        Barcode = product.Barcode,
                        ProductName = product.ProductName,
                        ModelName = product.ModelName,
                        Unit = string.IsNullOrWhiteSpace(product.Unit) ? "Adet" : product.Unit,
                        SystemQuantity = product.SystemQuantity,
                        CountedQuantity = product.SystemQuantity,
                        IsCounted = false,
                        PurchasePrice = product.PurchasePrice
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
                _currentCountIdempotencyKey = Guid.NewGuid();
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
                i.Barcode.Equals(code, StringComparison.OrdinalIgnoreCase) ||
                i.ProductName.Equals(code, StringComparison.OrdinalIgnoreCase));

            if (item != null)
            {
                item.CountedQuantity = item.IsCounted ? item.CountedQuantity + 1 : 1;
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

            var itemsWithDifference = CountItems.Where(i => i.IsCounted && i.Difference != 0).ToList();
            if (!itemsWithDifference.Any())
            {
                StatusMessage = "Düzeltilecek fark bulunamadı.";
                IsActionSuccessful = false;
                return;
            }

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                $"{itemsWithDifference.Count} üründe fark tespit edildi.\n\n" +
                $"Sayım Fazlası: +{TotalPositiveDifference} adet\n" +
                $"Sayım Eksiği: {TotalNegativeDifference} adet\n" +
                $"Net Finansal Sapma: {TotalFinancialDifference:C2}\n\n" +
                "Sayım kayıtları oluşturulacak ve stok güncellenecek.\nDevam etmek istiyor musunuz?",
                "Stok Sayım Onayı");
            if (!confirmed) return;

            IsLoading = true;
            try
            {
                DateTime countedAt = DateTime.SpecifyKind(
                    CountDate.Date.Add(DateTime.UtcNow.TimeOfDay),
                    DateTimeKind.Utc);
                var command = new ApplyStockCountCommand(
                    _currentCountIdempotencyKey,
                    SelectedWarehouse.Id,
                    countedAt,
                    StockCountMode.FullWarehouse,
                    itemsWithDifference.Select(item => new StockCountLineCommand(
                        item.ProductId, item.SystemQuantity, item.CountedQuantity)).ToList(),
                    App.CurrentUser?.Username ?? "Sistem");
                var result = await _stockCountCommandService.ApplyAsync(command);
                if (result.IsFailure || result.Value is null)
                {
                    StatusMessage = result.Error;
                    IsActionSuccessful = false;
                    return;
                }

                StatusMessage = result.Value.WasAlreadyApplied
                    ? $"Sayım daha önce uygulanmıştı ({result.Value.ReferenceNumber}); stok ikinci kez değiştirilmedi."
                    : $"Sayım kaydedildi ({result.Value.ReferenceNumber}). {result.Value.ProductCount} ürün güncellendi.";
                IsActionSuccessful = true;

                await RefreshCoreAsync();
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
        private async Task ExportToExcel()
        {
            if (CountItems.Count == 0)
            {
                StatusMessage = "Dışa aktarılacak veri bulunamadı.";
                IsActionSuccessful = false;
                return;
            }

            var filePath = await _dialogService.ShowSaveFileDialogAsync(
                "Stok Sayım Raporu Kaydet",
                "Excel Dosyası (*.xlsx)|*.xlsx",
                $"StokSayim_{SelectedWarehouse?.Name ?? "Tum"}_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx");
            if (string.IsNullOrWhiteSpace(filePath)) return;

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
                workbook.SaveAs(filePath);

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
        private async Task ImportFromExcel()
        {
            if (CountItems.Count == 0)
            {
                StatusMessage = "Önce bir depo seçin ve ürünleri yükleyin.";
                IsActionSuccessful = false;
                return;
            }

            var filePath = await _dialogService.ShowOpenFileDialogAsync(
                "Sayım Verilerini İçe Aktar",
                "Excel Dosyası (*.xlsx)|*.xlsx|Tüm Dosyalar (*.*)|*.*");
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1);
                var usedRows = worksheet.RangeUsed()?.RowsUsed().ToList();

                if (usedRows == null || usedRows.Count == 0)
                {
                    StatusMessage = "Excel dosyasında veri bulunamadı.";
                    IsActionSuccessful = false;
                    return;
                }

                var headerRow = usedRows.FirstOrDefault(row =>
                    string.Equals(row.Cell(1).GetValue<string>()?.Trim(), "Ürün Kodu", StringComparison.OrdinalIgnoreCase));
                int quantityColumn = 2;
                IEnumerable<IXLRangeRow> rows = usedRows;
                if (headerRow != null)
                {
                    int lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 2;
                    for (int column = 1; column <= lastColumn; column++)
                    {
                        if (string.Equals(
                                headerRow.Cell(column).GetValue<string>()?.Trim(),
                                "Sayılan Miktar",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            quantityColumn = column;
                            break;
                        }
                    }
                    int headerNumber = headerRow.RangeAddress.FirstAddress.RowNumber;
                    rows = usedRows.Where(row => row.RangeAddress.FirstAddress.RowNumber > headerNumber);
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
                int invalidQuantityCount = 0;

                foreach (var row in rows)
                {
                    var searchKey = row.Cell(1).GetValue<string>()?.Trim();
                    var countedQtyStr = row.Cell(quantityColumn).GetValue<string>();

                    if (string.IsNullOrWhiteSpace(searchKey))
                        continue;
                    if (!int.TryParse(countedQtyStr, out int countedQty) || countedQty < 0)
                    {
                        invalidQuantityCount++;
                        continue;
                    }

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

                StatusMessage = $"{updatedCount} ürün Excel'den yüklendi. " +
                                $"({notFoundList.Count} tanınmayan ürün, {invalidQuantityCount} geçersiz miktar)";
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
                var result = await _stockCountReadService.GetHistoryAsync();
                if (result.IsFailure)
                {
                    StatusMessage = result.Error;
                    IsActionSuccessful = false;
                    return;
                }
                foreach (var item in result.Value!) CountHistory.Add(item);

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
        private async Task ViewHistoryDetailAsync(StockCountHistoryDto? item)
        {
            if (item == null || string.IsNullOrEmpty(item.ReferenceNumber)) return;

            try
            {
                CountHistoryDetails.Clear();

                var result = await _stockCountReadService.GetHistoryDetailAsync(item.SessionId, item.ReferenceNumber);
                if (result.IsFailure)
                {
                    StatusMessage = result.Error;
                    IsActionSuccessful = false;
                    return;
                }
                foreach (var detail in result.Value!) CountHistoryDetails.Add(detail);

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

        private async Task DebounceManualSearchAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(350, cancellationToken);
                await ExecuteSearchProductAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Yeni metin önceki aramanın yerini aldı.
            }
        }

        private async Task ExecuteSearchProductAsync(CancellationToken cancellationToken = default)
        {
            ManualSearchResults.Clear();

            if (ManualSelectedWarehouse == null || string.IsNullOrWhiteSpace(ManualSearchText) || ManualSearchText.Length < 2)
                return;

            try
            {
                var result = await _stockCountReadService.SearchProductsAsync(
                    ManualSelectedWarehouse.Id,
                    ManualSearchText,
                    cancellationToken: cancellationToken);
                if (result.IsFailure)
                {
                    StatusMessage = result.Error;
                    IsActionSuccessful = false;
                    return;
                }

                foreach (var product in result.Value!)
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

            if (ManualCountItems.Any(i => i.ProductId == product.ProductId))
            {
                StatusMessage = $"'{product.ProductName}' zaten listede.";
                IsActionSuccessful = false;
                return;
            }

            var item = new StockCountItem
            {
                ProductId = product.ProductId,
                ProductCode = string.IsNullOrWhiteSpace(product.ProductCode) ? $"P-{product.ProductId:D4}" : product.ProductCode,
                Barcode = product.Barcode,
                ProductName = product.ProductName,
                ModelName = product.ModelName,
                Unit = string.IsNullOrWhiteSpace(product.Unit) ? "Adet" : product.Unit,
                SystemQuantity = product.SystemQuantity,
                CountedQuantity = 0,
                IsCounted = false,
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

            var itemsWithDifference = ManualCountItems.Where(i => i.IsCounted && i.Difference != 0).ToList();
            if (!itemsWithDifference.Any())
            {
                StatusMessage = "Düzeltilecek fark bulunamadı.";
                IsActionSuccessful = false;
                return;
            }

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                $"{itemsWithDifference.Count} üründe fark tespit edildi.\n\n" +
                $"Sayım Fazlası: +{ManualTotalPositiveDifference} adet\n" +
                $"Sayım Eksiği: {ManualTotalNegativeDifference} adet\n\n" +
                "Sayım kayıtları oluşturulacak ve stok güncellenecek.\nDevam etmek istiyor musunuz?",
                "Manuel Sayım Onayı");
            if (!confirmed) return;

            IsLoading = true;
            try
            {
                var command = new ApplyStockCountCommand(
                    _manualCountIdempotencyKey,
                    ManualSelectedWarehouse.Id,
                    DateTime.UtcNow,
                    StockCountMode.Manual,
                    itemsWithDifference.Select(item => new StockCountLineCommand(
                        item.ProductId, item.SystemQuantity, item.CountedQuantity)).ToList(),
                    App.CurrentUser?.Username ?? "Sistem");
                var result = await _stockCountCommandService.ApplyAsync(command);
                if (result.IsFailure || result.Value is null)
                {
                    StatusMessage = result.Error;
                    IsActionSuccessful = false;
                    return;
                }

                StatusMessage = result.Value.WasAlreadyApplied
                    ? $"Manuel sayım daha önce uygulanmıştı ({result.Value.ReferenceNumber}); stok ikinci kez değiştirilmedi."
                    : $"Manuel sayım kaydedildi ({result.Value.ReferenceNumber}). {result.Value.ProductCount} ürün güncellendi.";
                IsActionSuccessful = true;

                ManualCountItems.Clear();
                _manualCountIdempotencyKey = Guid.NewGuid();
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
        private async Task ClearManualList()
        {
            if (ManualCountItems.Count == 0) return;

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                "Sayım listesi temizlenecek. Devam etmek istiyor musunuz?",
                "Listeyi Temizle");
            if (confirmed)
            {
                ManualCountItems.Clear();
                _manualCountIdempotencyKey = Guid.NewGuid();
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
        private bool _isCounted;

        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string Unit { get; set; } = "Adet";
        public int SystemQuantity { get; set; }
        public decimal PurchasePrice { get; set; }
        public bool IsCounted
        {
            get => _isCounted;
            set
            {
                if (_isCounted == value) return;
                _isCounted = value;
                OnPropertyChanged();
            }
        }

        public int CountedQuantity
        {
            get => _countedQuantity;
            set
            {
                int normalized = Math.Max(0, value);
                IsCounted = true;
                if (_countedQuantity != normalized)
                {
                    _countedQuantity = normalized;
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

}
