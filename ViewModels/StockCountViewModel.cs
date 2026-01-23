using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ClosedXML.Excel;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Fiziksel Stok Sayım işlemleri için ViewModel
    /// </summary>
    public class StockCountViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private Warehouse? _selectedWarehouse;
        private DateTime _countDate = DateTime.Today;
        private string _statusMessage = string.Empty;
        private bool _isActionSuccessful;
        private bool _isLoading;
        private string _searchText = string.Empty;
        private bool _isHistoryVisible;

        // Manuel Sayım için yeni alanlar
        private string _manualSearchText = string.Empty;
        private Product? _selectedSearchResult;
        private Warehouse? _manualSelectedWarehouse;

        public ObservableCollection<Warehouse> Warehouses { get; set; }
        public ObservableCollection<StockCountItem> CountItems { get; set; }
        public ObservableCollection<CountHistoryItem> CountHistory { get; set; }

        // Manuel Sayım için koleksiyonlar
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
                if (SetProperty(ref _selectedWarehouse, value))
                {
                    LoadInventoryForWarehouse();
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

        /// <summary>
        /// Arama filtresi metni
        /// </summary>
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

        /// <summary>
        /// Sayım geçmişi popup görünürlüğü
        /// </summary>
        public bool IsHistoryVisible
        {
            get => _isHistoryVisible;
            set => SetProperty(ref _isHistoryVisible, value);
        }

        /// <summary>
        /// Fark olan toplam öğe sayısı
        /// </summary>
        public int TotalDifferenceCount => CountItems?.Count(i => i.Difference != 0) ?? 0;

        /// <summary>
        /// Toplam pozitif fark (Sayım Fazlası)
        /// </summary>
        public int TotalPositiveDifference => CountItems?.Where(i => i.Difference > 0).Sum(i => i.Difference) ?? 0;

        /// <summary>
        /// Toplam negatif fark (Sayım Eksiği)
        /// </summary>
        public int TotalNegativeDifference => CountItems?.Where(i => i.Difference < 0).Sum(i => i.Difference) ?? 0;

        /// <summary>
        /// Toplam ürün sayısı (görüntülenen)
        /// </summary>
        public int TotalItemCount => CountItems?.Count ?? 0;

        // === MANUEL SAYIM PROPERTYLERİ ===

        /// <summary>
        /// Manuel sayım için arama metni
        /// </summary>
        public string ManualSearchText
        {
            get => _manualSearchText;
            set
            {
                if (SetProperty(ref _manualSearchText, value))
                {
                    ExecuteSearchProduct();
                }
            }
        }

        /// <summary>
        /// Arama sonucundan seçilen ürün
        /// </summary>
        public Product? SelectedSearchResult
        {
            get => _selectedSearchResult;
            set => SetProperty(ref _selectedSearchResult, value);
        }

        /// <summary>
        /// Manuel sayım için seçilen depo
        /// </summary>
        public Warehouse? ManualSelectedWarehouse
        {
            get => _manualSelectedWarehouse;
            set
            {
                if (SetProperty(ref _manualSelectedWarehouse, value))
                {
                    // Depo değişince listeyi temizle
                    ManualCountItems.Clear();
                    UpdateManualTotals();
                }
            }
        }

        /// <summary>
        /// Manuel sayım fark toplamı
        /// </summary>
        public int ManualTotalDifferenceCount => ManualCountItems?.Count(i => i.Difference != 0) ?? 0;

        /// <summary>
        /// Manuel sayım - pozitif fark
        /// </summary>
        public int ManualTotalPositiveDifference => ManualCountItems?.Where(i => i.Difference > 0).Sum(i => i.Difference) ?? 0;

        /// <summary>
        /// Manuel sayım - negatif fark
        /// </summary>
        public int ManualTotalNegativeDifference => ManualCountItems?.Where(i => i.Difference < 0).Sum(i => i.Difference) ?? 0;

        /// <summary>
        /// Manuel sayım - toplam ürün
        /// </summary>
        public int ManualTotalItemCount => ManualCountItems?.Count ?? 0;

        // === EXCEL SAYIM KOMUTLARI ===
        public ICommand SaveCountCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExportToExcelCommand { get; }
        public ICommand ImportFromExcelCommand { get; }
        public ICommand ShowHistoryCommand { get; }
        public ICommand CloseHistoryCommand { get; }

        // === MANUEL SAYIM KOMUTLARI ===
        public ICommand AddToManualCountCommand { get; }
        public ICommand RemoveFromManualCountCommand { get; }
        public ICommand ConfirmManualCountCommand { get; }
        public ICommand ClearManualListCommand { get; }

        public StockCountViewModel()
        {
            _context = new AppDbContext();
            Warehouses = new ObservableCollection<Warehouse>(_context.Warehouses.Where(w => w.IsActive).ToList());
            CountItems = new ObservableCollection<StockCountItem>();
            CountHistory = new ObservableCollection<CountHistoryItem>();

            // Manuel Sayım koleksiyonları
            ManualSearchResults = new ObservableCollection<Product>();
            ManualCountItems = new ObservableCollection<StockCountItem>();

            // CollectionView ve filtre ayarla
            CountItemsView = CollectionViewSource.GetDefaultView(CountItems);
            CountItemsView.Filter = FilterItems;

            // Excel Sayım Komutları
            SaveCountCommand = new RelayCommand(_ => ExecuteSaveCount(), _ => CanExecuteSaveCount());
            RefreshCommand = new RelayCommand(_ => LoadInventoryForWarehouse());
            ExportToExcelCommand = new RelayCommand(_ => ExecuteExportToExcel(), _ => CountItems.Count > 0);
            ImportFromExcelCommand = new RelayCommand(_ => ExecuteImportFromExcel(), _ => CountItems.Count > 0);
            ShowHistoryCommand = new RelayCommand(_ => ExecuteShowHistory());
            CloseHistoryCommand = new RelayCommand(_ => IsHistoryVisible = false);

            // Manuel Sayım Komutları
            AddToManualCountCommand = new RelayCommand(_ => ExecuteAddToManualCount(), _ => CanAddToManualCount());
            RemoveFromManualCountCommand = new RelayCommand(ExecuteRemoveFromManualCount);
            ConfirmManualCountCommand = new RelayCommand(_ => ExecuteConfirmManualCount(), _ => CanConfirmManualCount());
            ClearManualListCommand = new RelayCommand(_ => ExecuteClearManualList(), _ => ManualCountItems.Count > 0);
        }

        /// <summary>
        /// Ürün filtresi
        /// </summary>
        private bool FilterItems(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            if (obj is StockCountItem item)
            {
                return item.ProductCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || item.ProductName.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>
        /// Seçilen depo için envanter verilerini yükler
        /// </summary>
        private void LoadInventoryForWarehouse()
        {
            CountItems.Clear();
            StatusMessage = string.Empty;
            SearchText = string.Empty;

            if (SelectedWarehouse == null) return;

            IsLoading = true;

            try
            {
                // Seçilen depodaki tüm inventory kayıtlarını al (ürün bilgisi ile birlikte)
                var inventories = _context.Inventories
                    .Include(i => i.Product)
                    .Where(i => i.WarehouseId == SelectedWarehouse.Id)
                    .ToList();

                foreach (var inv in inventories)
                {
                    var item = new StockCountItem
                    {
                        ProductId = inv.ProductId,
                        ProductCode = inv.Product.SKU ?? $"P-{inv.ProductId:D4}",
                        ProductName = inv.Product.ProductName,
                        ModelName = inv.Product.ModelName ?? string.Empty,
                        Unit = inv.Product.Unit,
                        SystemQuantity = inv.Quantity,
                        CountedQuantity = inv.Quantity // Başlangıçta sistem değeri ile aynı
                    };

                    // Fark değiştiğinde özet güncelle
                    item.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(StockCountItem.Difference))
                        {
                            OnPropertyChanged(nameof(TotalDifferenceCount));
                            OnPropertyChanged(nameof(TotalPositiveDifference));
                            OnPropertyChanged(nameof(TotalNegativeDifference));
                        }
                    };

                    CountItems.Add(item);
                }

                // Özet bilgileri güncelle
                OnPropertyChanged(nameof(TotalDifferenceCount));
                OnPropertyChanged(nameof(TotalPositiveDifference));
                OnPropertyChanged(nameof(TotalNegativeDifference));
                OnPropertyChanged(nameof(TotalItemCount));

                StatusMessage = $"{CountItems.Count} ürün yüklendi.";
                IsActionSuccessful = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Hata: {ex.Message}";
                IsActionSuccessful = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanExecuteSaveCount()
        {
            return SelectedWarehouse != null && 
                   CountItems.Count > 0 && 
                   CountItems.Any(i => i.Difference != 0);
        }

        /// <summary>
        /// Sayım sonuçlarını kaydeder ve stok düzeltmesi yapar
        /// </summary>
        private void ExecuteSaveCount()
        {
            if (SelectedWarehouse == null) return;

            var itemsWithDifference = CountItems.Where(i => i.Difference != 0).ToList();
            if (!itemsWithDifference.Any())
            {
                StatusMessage = "Düzeltilecek fark bulunamadı.";
                IsActionSuccessful = false;
                return;
            }

            // Kullanıcı onayı iste
            var result = MessageBox.Show(
                $"{itemsWithDifference.Count} üründe fark tespit edildi.\n\n" +
                $"Sayım Fazlası: {TotalPositiveDifference} adet\n" +
                $"Sayım Eksiği: {TotalNegativeDifference} adet\n\n" +
                "Sayım kayıtları oluşturulacak ve stok güncellenecek.\nDevam etmek istiyor musunuz?",
                "Stok Sayım Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                foreach (var item in itemsWithDifference)
                {
                    // 1. Inventory güncelle
                    var inventory = _context.Inventories
                        .FirstOrDefault(i => i.ProductId == item.ProductId && i.WarehouseId == SelectedWarehouse.Id);

                    if (inventory == null) continue;

                    // 2. StockTransaction oluştur
                    var transactionType = item.Difference > 0 
                        ? StockTransactionType.AdjustmentPlus 
                        : StockTransactionType.AdjustmentMinus;

                    var stockTransaction = new StockTransaction
                    {
                        Date = CountDate,
                        ProductId = item.ProductId,
                        SourceWarehouseId = item.Difference < 0 ? SelectedWarehouse.Id : null,
                        TargetWarehouseId = item.Difference > 0 ? SelectedWarehouse.Id : null,
                        Quantity = Math.Abs(item.Difference),
                        TransactionType = transactionType,
                        Description = $"Stok sayımı - {SelectedWarehouse.Name}. " +
                                      $"Sistem: {item.SystemQuantity}, Sayılan: {item.CountedQuantity}, Fark: {item.Difference}",
                        ReferenceId = $"COUNT-{CountDate:yyyyMMdd}-{SelectedWarehouse.Id}"
                    };

                    _context.StockTransactions.Add(stockTransaction);

                    // 3. Envanter miktarını güncelle
                    inventory.Quantity = item.CountedQuantity;
                }

                _context.SaveChanges();
                transaction.Commit();

                StatusMessage = $"Sayım başarıyla kaydedildi. {itemsWithDifference.Count} ürün güncellendi.";
                IsActionSuccessful = true;

                // Listeyi yenile
                LoadInventoryForWarehouse();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                StatusMessage = $"Hata oluştu: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        /// <summary>
        /// Excel'e dışa aktarım
        /// </summary>
        private void ExecuteExportToExcel()
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
                FileName = $"StokSayim_{SelectedWarehouse?.Name ?? "Tum"}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                Title = "Stok Sayım Raporu Kaydet"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Stok Sayım");

                // Başlık bilgileri
                worksheet.Cell(1, 1).Value = "STOK SAYIM RAPORU";
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                worksheet.Range(1, 1, 1, 6).Merge();

                worksheet.Cell(2, 1).Value = $"Depo: {SelectedWarehouse?.Name ?? "Belirtilmemiş"}";
                worksheet.Cell(3, 1).Value = $"Tarih: {CountDate:dd.MM.yyyy}";
                worksheet.Cell(4, 1).Value = $"Rapor Oluşturma: {DateTime.Now:dd.MM.yyyy HH:mm}";

                // Header satırı
                int headerRow = 6;
                var headers = new[] { "Ürün Kodu", "Ürün Adı", "Birim", "Sistem Miktar", "Sayılan Miktar", "Fark" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(headerRow, i + 1).Value = headers[i];
                    worksheet.Cell(headerRow, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(headerRow, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                    worksheet.Cell(headerRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                // Veri satırları
                int dataRow = headerRow + 1;
                foreach (var item in CountItems)
                {
                    worksheet.Cell(dataRow, 1).Value = item.ProductCode;
                    worksheet.Cell(dataRow, 2).Value = item.ProductName;
                    worksheet.Cell(dataRow, 3).Value = item.Unit;
                    worksheet.Cell(dataRow, 4).Value = item.SystemQuantity;
                    worksheet.Cell(dataRow, 5).Value = item.CountedQuantity;
                    worksheet.Cell(dataRow, 6).Value = item.Difference;

                    // Fark renklendirme
                    if (item.Difference > 0)
                        worksheet.Cell(dataRow, 6).Style.Font.FontColor = XLColor.Green;
                    else if (item.Difference < 0)
                        worksheet.Cell(dataRow, 6).Style.Font.FontColor = XLColor.Red;

                    // Border
                    for (int col = 1; col <= 6; col++)
                        worksheet.Cell(dataRow, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                    dataRow++;
                }

                // Özet satırları
                dataRow += 2;
                worksheet.Cell(dataRow, 1).Value = "ÖZET";
                worksheet.Cell(dataRow, 1).Style.Font.Bold = true;
                worksheet.Cell(dataRow + 1, 1).Value = "Toplam Ürün:";
                worksheet.Cell(dataRow + 1, 2).Value = TotalItemCount;
                worksheet.Cell(dataRow + 2, 1).Value = "Farklı Ürün Sayısı:";
                worksheet.Cell(dataRow + 2, 2).Value = TotalDifferenceCount;
                worksheet.Cell(dataRow + 3, 1).Value = "Sayım Fazlası:";
                worksheet.Cell(dataRow + 3, 2).Value = TotalPositiveDifference;
                worksheet.Cell(dataRow + 3, 2).Style.Font.FontColor = XLColor.Green;
                worksheet.Cell(dataRow + 4, 1).Value = "Sayım Eksiği:";
                worksheet.Cell(dataRow + 4, 2).Value = TotalNegativeDifference;
                worksheet.Cell(dataRow + 4, 2).Style.Font.FontColor = XLColor.Red;

                // Sütun genişliklerini ayarla
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(saveDialog.FileName);

                StatusMessage = "Excel dosyası başarıyla oluşturuldu.";
                IsActionSuccessful = true;

                // Dosyayı aç
                var openResult = MessageBox.Show(
                    "Excel dosyası kaydedildi. Dosyayı açmak ister misiniz?",
                    "Başarılı",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (openResult == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = saveDialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Excel hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        /// <summary>
        /// Excel'den sayım verilerini içe aktar
        /// SKU veya ModelName ile eşleştirme yapar
        /// </summary>
        private void ExecuteImportFromExcel()
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
                var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1); // İlk satırı atla (header)

                if (rows == null)
                {
                    StatusMessage = "Excel dosyasında veri bulunamadı.";
                    IsActionSuccessful = false;
                    return;
                }

                int updatedCount = 0;
                var notFoundList = new List<string>();

                foreach (var row in rows)
                {
                    // Sütun 1: SKU veya Model (arama anahtarı)
                    var searchKey = row.Cell(1).GetValue<string>()?.Trim();
                    
                    // Sütun 2: Sayılan Miktar
                    var countedQtyStr = row.Cell(2).GetValue<string>();

                    if (string.IsNullOrWhiteSpace(searchKey))
                        continue;

                    if (!int.TryParse(countedQtyStr, out int countedQuantity))
                        continue;

                    // Önce SKU ile ara
                    var item = CountItems.FirstOrDefault(i => 
                        !string.IsNullOrEmpty(i.ProductCode) &&
                        i.ProductCode.Trim().Equals(searchKey, StringComparison.OrdinalIgnoreCase));

                    // SKU bulunamadıysa ModelName ile ara
                    if (item == null)
                    {
                        item = CountItems.FirstOrDefault(i =>
                            !string.IsNullOrEmpty(i.ModelName) &&
                            i.ModelName.Trim().Equals(searchKey, StringComparison.OrdinalIgnoreCase));
                    }

                    // Ürün Adı ile de ara
                    if (item == null)
                    {
                        item = CountItems.FirstOrDefault(i =>
                            !string.IsNullOrEmpty(i.ProductName) &&
                            i.ProductName.Trim().Equals(searchKey, StringComparison.OrdinalIgnoreCase));
                    }

                    if (item != null)
                    {
                        item.CountedQuantity = countedQuantity;
                        updatedCount++;
                    }
                    else
                    {
                        notFoundList.Add(searchKey);
                    }
                }

                // Özet bilgileri güncelle
                OnPropertyChanged(nameof(TotalDifferenceCount));
                OnPropertyChanged(nameof(TotalPositiveDifference));
                OnPropertyChanged(nameof(TotalNegativeDifference));
                CountItemsView?.Refresh();

                // Sonuç mesajı
                var message = $"{updatedCount} ürün güncellendi.";
                if (notFoundList.Count > 0)
                {
                    var notFoundSample = string.Join(", ", notFoundList.Take(5));
                    if (notFoundList.Count > 5)
                        notFoundSample += $" ve {notFoundList.Count - 5} adet daha";
                    
                    message += $"\n{notFoundList.Count} ürün bulunamadı: {notFoundSample}";
                }

                StatusMessage = message;
                IsActionSuccessful = updatedCount > 0;

                if (notFoundList.Count > 0)
                {
                    MessageBox.Show(
                        $"Excel'den içe aktarım tamamlandı.\n\n" +
                        $"✅ Güncellenen: {updatedCount} ürün\n" +
                        $"❌ Bulunamayan: {notFoundList.Count} ürün\n\n" +
                        (notFoundList.Count <= 10 
                            ? $"Bulunamayan kodlar:\n{string.Join("\n", notFoundList)}"
                            : $"Bulunamayan kodlar (ilk 10):\n{string.Join("\n", notFoundList.Take(10))}\n...ve {notFoundList.Count - 10} adet daha"),
                        "İçe Aktarım Sonucu",
                        MessageBoxButton.OK,
                        notFoundList.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"İçe aktarım hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        /// <summary>
        /// Sayım geçmişini yükle ve göster
        /// </summary>
        private void ExecuteShowHistory()
        {
            try
            {
                CountHistory.Clear();

                // StockTransactions tablosundan sayım kayıtlarını çek
                // ReferenceId formatı: COUNT-yyyyMMdd-WarehouseId
                var adjustmentTypes = new[] 
                { 
                    StockTransactionType.AdjustmentPlus, 
                    StockTransactionType.AdjustmentMinus 
                };

                var transactions = _context.StockTransactions
                    .Include(t => t.SourceWarehouse)
                    .Include(t => t.TargetWarehouse)
                    .Where(t => adjustmentTypes.Contains(t.TransactionType) 
                             && t.ReferenceId != null 
                             && t.ReferenceId.StartsWith("COUNT-"))
                    .OrderByDescending(t => t.Date)
                    .ToList();

                // ReferenceId'ye göre grupla
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
                    .Take(20)  // Son 20 sayım
                    .ToList();

                foreach (var item in grouped)
                {
                    CountHistory.Add(item);
                }

                IsHistoryVisible = true;

                if (CountHistory.Count == 0)
                {
                    StatusMessage = "Kayıtlı sayım geçmişi bulunamadı.";
                    IsActionSuccessful = false;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Geçmiş yüklenirken hata: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        // =============================================
        // === MANUEL SAYIM METODLARI ===
        // =============================================

        /// <summary>
        /// Ürün arama (SKU, Barkod, Ürün Adı ile)
        /// </summary>
        private void ExecuteSearchProduct()
        {
            ManualSearchResults.Clear();

            if (string.IsNullOrWhiteSpace(ManualSearchText) || ManualSearchText.Length < 2)
                return;

            try
            {
                var results = _context.Products
                    .Where(p =>
                        (p.SKU != null && p.SKU.Contains(ManualSearchText)) ||
                        (p.Barcode != null && p.Barcode.Contains(ManualSearchText)) ||
                        p.ProductName.Contains(ManualSearchText) ||
                        (p.ModelName != null && p.ModelName.Contains(ManualSearchText)))
                    .Take(10)
                    .ToList();

                foreach (var product in results)
                {
                    ManualSearchResults.Add(product);
                }

                // Tek sonuç varsa otomatik seç
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

        /// <summary>
        /// Listeye ekleme yapılabilir mi?
        /// </summary>
        private bool CanAddToManualCount()
        {
            return ManualSelectedWarehouse != null &&
                   (SelectedSearchResult != null || ManualSearchResults.Count == 1);
        }

        /// <summary>
        /// Seçilen ürünü manuel sayım listesine ekle
        /// </summary>
        private void ExecuteAddToManualCount()
        {
            if (ManualSelectedWarehouse == null)
            {
                StatusMessage = "Lütfen önce bir depo seçin.";
                IsActionSuccessful = false;
                return;
            }

            // Seçili ürün yoksa tek sonucu kullan
            var product = SelectedSearchResult ?? ManualSearchResults.FirstOrDefault();

            if (product == null)
            {
                StatusMessage = "Eklenecek ürün bulunamadı.";
                IsActionSuccessful = false;
                return;
            }

            // Zaten listede mi kontrol et
            if (ManualCountItems.Any(i => i.ProductId == product.Id))
            {
                StatusMessage = $"'{product.ProductName}' zaten listede.";
                IsActionSuccessful = false;
                return;
            }

            // Envanterdeki miktarı çek
            var inventory = _context.Inventories
                .FirstOrDefault(i => i.ProductId == product.Id && i.WarehouseId == ManualSelectedWarehouse.Id);

            int systemQty = inventory?.Quantity ?? 0;

            var item = new StockCountItem
            {
                ProductId = product.Id,
                ProductCode = product.SKU ?? $"P-{product.Id:D4}",
                ProductName = product.ProductName,
                ModelName = product.ModelName ?? string.Empty,
                Unit = product.Unit,
                SystemQuantity = systemQty,
                CountedQuantity = 0 // Kullanıcı girecek
            };

            // Fark değişimlerini dinle
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(StockCountItem.Difference))
                {
                    UpdateManualTotals();
                }
            };

            ManualCountItems.Add(item);
            UpdateManualTotals();

            // Arama alanını temizle
            ManualSearchText = string.Empty;
            SelectedSearchResult = null;
            ManualSearchResults.Clear();

            StatusMessage = $"'{product.ProductName}' listeye eklendi.";
            IsActionSuccessful = true;
        }

        /// <summary>
        /// Ürünü manuel sayım listesinden çıkar
        /// </summary>
        private void ExecuteRemoveFromManualCount(object? parameter)
        {
            if (parameter is StockCountItem item)
            {
                ManualCountItems.Remove(item);
                UpdateManualTotals();
                StatusMessage = $"'{item.ProductName}' listeden çıkarıldı.";
                IsActionSuccessful = true;
            }
        }

        /// <summary>
        /// Sayım onaylanabilir mi?
        /// </summary>
        private bool CanConfirmManualCount()
        {
            return ManualSelectedWarehouse != null &&
                   ManualCountItems.Count > 0 &&
                   ManualCountItems.Any(i => i.CountedQuantity >= 0);
        }

        /// <summary>
        /// Manuel sayımı onayla ve stokları güncelle
        /// </summary>
        private void ExecuteConfirmManualCount()
        {
            if (ManualSelectedWarehouse == null) return;

            var itemsWithDifference = ManualCountItems.Where(i => i.Difference != 0).ToList();
            if (!itemsWithDifference.Any())
            {
                StatusMessage = "Düzeltilecek fark bulunamadı.";
                IsActionSuccessful = false;
                return;
            }

            // Kullanıcı onayı
            var positiveDiff = ManualCountItems.Where(i => i.Difference > 0).Sum(i => i.Difference);
            var negativeDiff = ManualCountItems.Where(i => i.Difference < 0).Sum(i => i.Difference);

            var result = MessageBox.Show(
                $"{itemsWithDifference.Count} üründe fark tespit edildi.\n\n" +
                $"Sayım Fazlası: +{positiveDiff} adet\n" +
                $"Sayım Eksiği: {negativeDiff} adet\n\n" +
                "Sayım kayıtları oluşturulacak ve stok güncellenecek.\nDevam etmek istiyor musunuz?",
                "Manuel Sayım Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                var referenceId = $"MANUAL-{DateTime.Now:yyyyMMdd-HHmmss}-{ManualSelectedWarehouse.Id}";

                foreach (var item in itemsWithDifference)
                {
                    // Inventory bul veya oluştur
                    var inventory = _context.Inventories
                        .FirstOrDefault(i => i.ProductId == item.ProductId && i.WarehouseId == ManualSelectedWarehouse.Id);

                    if (inventory == null)
                    {
                        // Envanter yoksa oluştur
                        inventory = new Inventory
                        {
                            ProductId = item.ProductId,
                            WarehouseId = ManualSelectedWarehouse.Id,
                            Quantity = 0
                        };
                        _context.Inventories.Add(inventory);
                    }

                    // StockTransaction oluştur
                    var transactionType = item.Difference > 0
                        ? StockTransactionType.AdjustmentPlus
                        : StockTransactionType.AdjustmentMinus;

                    var stockTransaction = new StockTransaction
                    {
                        Date = DateTime.Now,
                        ProductId = item.ProductId,
                        SourceWarehouseId = item.Difference < 0 ? ManualSelectedWarehouse.Id : null,
                        TargetWarehouseId = item.Difference > 0 ? ManualSelectedWarehouse.Id : null,
                        Quantity = Math.Abs(item.Difference),
                        TransactionType = transactionType,
                        Description = $"Manuel sayım - {ManualSelectedWarehouse.Name}. " +
                                      $"Sistem: {item.SystemQuantity}, Sayılan: {item.CountedQuantity}, Fark: {item.Difference}",
                        ReferenceId = referenceId
                    };

                    _context.StockTransactions.Add(stockTransaction);

                    // Envanter miktarını güncelle
                    inventory.Quantity = item.CountedQuantity;
                }

                _context.SaveChanges();
                transaction.Commit();

                StatusMessage = $"Manuel sayım başarıyla kaydedildi. {itemsWithDifference.Count} ürün güncellendi.";
                IsActionSuccessful = true;

                // Listeyi temizle
                ManualCountItems.Clear();
                UpdateManualTotals();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                StatusMessage = $"Hata oluştu: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        /// <summary>
        /// Manuel sayım listesini temizle
        /// </summary>
        private void ExecuteClearManualList()
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

        /// <summary>
        /// Manuel sayım özet bilgilerini güncelle
        /// </summary>
        private void UpdateManualTotals()
        {
            OnPropertyChanged(nameof(ManualTotalDifferenceCount));
            OnPropertyChanged(nameof(ManualTotalPositiveDifference));
            OnPropertyChanged(nameof(ManualTotalNegativeDifference));
            OnPropertyChanged(nameof(ManualTotalItemCount));
        }
    }

    /// <summary>
    /// Sayım sırasında kullanılan ürün wrapper sınıfı
    /// </summary>
    public class StockCountItem : INotifyPropertyChanged
    {
        private int _countedQuantity;

        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string Unit { get; set; } = "Adet";
        public int SystemQuantity { get; set; }

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
                }
            }
        }

        /// <summary>
        /// Sayılan - Sistem = Fark
        /// Pozitif: Sayım Fazlası, Negatif: Sayım Eksiği
        /// </summary>
        public int Difference => CountedQuantity - SystemQuantity;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Sayım geçmişi öğesi
    /// </summary>
    public class CountHistoryItem
    {
        public DateTime Date { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public int TotalDifference { get; set; }
        public string ReferenceId { get; set; } = string.Empty;
    }
}
