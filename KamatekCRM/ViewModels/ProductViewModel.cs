using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services.Domain;
using KamatekCrm.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Stok/Ürün yönetimi ViewModel - GÜNCELLENMİŞ VERSİYON
    /// </summary>
    public partial class ProductViewModel : ViewModelBase
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private AppDbContext _context;
        private readonly IInventoryDomainService _inventoryDomainService;
        private readonly IProductImageService _imageService;
        private Product? _selectedProduct;
        private string _searchText = string.Empty;
        private ICollectionView? _productsView;
        private string _statusMessage = string.Empty;
        private bool _isActionSuccessful;

        /// <summary>
        /// Ürünler koleksiyonu
        /// </summary>
        public ObservableCollection<Product> Products { get; set; }

        /// <summary>
        /// Filtrelenmiş ürün listesi
        /// </summary>
        public ICollectionView ProductsView => _productsView!;

        /// <summary>
        /// Seçili ürün
        /// </summary>
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value))
                {
                    OnPropertyChanged(nameof(IsProductSelected));
                    OnPropertyChanged(nameof(HasProductPhoto));
                    UploadProductPhotoCommand.NotifyCanExecuteChanged();
                    DeleteProductPhotoCommand.NotifyCanExecuteChanged();
                    TransferStockCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Arama metni
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _productsView?.Refresh();
                }
            }
        }

        /// <summary>
        /// Durum mesajı
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// İşlem başarılı mı?
        /// </summary>
        public bool IsActionSuccessful
        {
            get => _isActionSuccessful;
            set => SetProperty(ref _isActionSuccessful, value);
        }

        public bool IsProductSelected => SelectedProduct != null;
        public bool HasProductPhoto => SelectedProduct != null && !string.IsNullOrWhiteSpace(SelectedProduct.ImagePath);

        // ===== KPI METRİK PROPERTİES =====
        public int TotalProductCount => Products.Count;
        public int TotalStockQuantityCount => Products.Sum(p => p.TotalStockQuantity);
        public int LowStockCount => Products.Count(p => p.TotalStockQuantity <= 5);
        public decimal TotalInventoryValue => Products.Sum(p => p.TotalStockQuantity * p.PurchasePrice);
        public string TotalInventoryValueDisplay => $"₺{TotalInventoryValue:N0}";

        // ===== FİLTRELEME PROPERTİES =====
        private string _selectedCategoryFilter = "Tümü";
        public string SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set
            {
                if (SetProperty(ref _selectedCategoryFilter, value))
                {
                    _productsView?.Refresh();
                }
            }
        }

        private string _stockStatusFilter = "Tümü";
        public string StockStatusFilter
        {
            get => _stockStatusFilter;
            set
            {
                if (SetProperty(ref _stockStatusFilter, value))
                {
                    _productsView?.Refresh();
                }
            }
        }

        public ObservableCollection<string> CategoryFilterItems { get; } = new()
        {
            "Tümü", "Kamera", "Diyafon", "Yangın Alarmı", "Hırsız Alarmı", "Akıllı Ev", "Erişim Kontrolü", "Uydu", "Fiber Optik", "Genel"
        };

        public void NotifyKpiMetrics()
        {
            OnPropertyChanged(nameof(TotalProductCount));
            OnPropertyChanged(nameof(TotalStockQuantityCount));
            OnPropertyChanged(nameof(LowStockCount));
            OnPropertyChanged(nameof(TotalInventoryValue));
            OnPropertyChanged(nameof(TotalInventoryValueDisplay));
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public ProductViewModel(IInventoryDomainService inventoryDomainService, IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _inventoryDomainService = inventoryDomainService;
            _dbContextFactory = dbContextFactory;
            _context = _dbContextFactory.CreateDbContext(); // TODO: Refactor to short-lived contexts
            _imageService = new ProductImageService();
            Products = new ObservableCollection<Product>();

            // Komutları tanımla

            // Verileri yükle
            LoadProducts();

            // Filtreleme görünümünü oluştur
            _productsView = CollectionViewSource.GetDefaultView(Products);
            _productsView.Filter = FilterProducts;
        }

        /// <summary>
        /// Ürünleri veritabanından yükle (İlişkili verilerle birlikte)
        /// </summary>
        private void LoadProducts()
        {
            Products.Clear();
            var products = _context.Products
                .Include(p => p.Category)      // Kategori bilgisi için
                .Include(p => p.Inventories) // Stok hesaplaması için
                .ToList();

            foreach (var product in products)
            {
                // Stok miktarını hesapla (Inventory tablosundaki toplam)
                product.TotalStockQuantity = product.Inventories.Sum(i => i.Quantity);

                Products.Add(product);
            }

            NotifyKpiMetrics();
        }

        /// <summary>
        /// Ürün filtreleme mantığı
        /// </summary>
        private bool FilterProducts(object obj)
        {
            if (obj is not Product product) return false;

            // 1. Metin Araması
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLower();
                bool matchesText = (product.ProductName != null && product.ProductName.ToLower().Contains(search)) ||
                                   (product.SKU != null && product.SKU.ToLower().Contains(search)) ||
                                   (product.Category != null && product.Category.Name != null && product.Category.Name.ToLower().Contains(search)) ||
                                   (product.Brand != null && product.Brand.BrandName != null && product.Brand.BrandName.ToLower().Contains(search));
                if (!matchesText) return false;
            }

            // 2. Kategori Filtresi
            if (SelectedCategoryFilter != "Tümü" && !string.IsNullOrEmpty(SelectedCategoryFilter))
            {
                if (product.Category?.Name != SelectedCategoryFilter) return false;
            }

            // 3. Stok Durumu Filtresi
            if (StockStatusFilter != "Tümü" && !string.IsNullOrEmpty(StockStatusFilter))
            {
                if (StockStatusFilter == "Stokta Var" && product.TotalStockQuantity <= 0) return false;
                if (StockStatusFilter == "Kritik Stok" && (product.TotalStockQuantity > 5 || product.TotalStockQuantity <= 0)) return false;
                if (StockStatusFilter == "Tükenenler" && product.TotalStockQuantity > 0) return false;
            }

            return true;
        }

        /// <summary>
        /// Yeni ürün ekleme penceresini aç
        /// </summary>
        [RelayCommand]
        private void AddNewProduct()
        {
            var editVm = App.ServiceProvider.GetRequiredService<AddProductViewModel>();
            editVm.Initialize(null);

            var window = new Views.AddProductWindow
            {
                DataContext = editVm,
                Owner = System.Windows.Application.Current?.MainWindow
            };
            var result = window.ShowDialog();

            if (result == true)
            {
                // Liste yeniden yükle
                RefreshProductList();
            }
        }

        /// <summary>
        /// Seçili ürünü düzenle
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsProductSelected))]
        private void EditProduct()
        {
            if (SelectedProduct == null) return;

            var editVm = App.ServiceProvider.GetRequiredService<AddProductViewModel>();
            editVm.Initialize(SelectedProduct);

            var window = new Views.AddProductWindow
            {
                DataContext = editVm,
                Owner = System.Windows.Application.Current?.MainWindow
            };
            var result = window.ShowDialog();

            if (result == true)
            {
                // Liste yeniden yükle
                RefreshProductList();
                StatusMessage = "Ürün güncellendi.";
                IsActionSuccessful = true;
            }
        }

        /// <summary>
        /// Seçili ürünü sil
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsProductSelected))]
        private void DeleteProduct()
        {
            if (SelectedProduct == null) return;

            var result = MessageBox.Show($"'{SelectedProduct.ProductName}' ürününü silmek istediğinize emin misiniz?", "Ürün Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var productToDelete = _context.Products.Find(SelectedProduct.Id);
                    if (productToDelete != null)
                    {
                        _context.Products.Remove(productToDelete);
                        _context.SaveChanges();

                        RefreshProductList();
                        StatusMessage = "Ürün başarıyla silindi.";
                        IsActionSuccessful = true;
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Ürün silinirken hata oluştu: {ex.Message}";
                    IsActionSuccessful = false;
                }
            }
        }

        /// <summary>
        /// Excel'den ürün içe aktar
        /// Excel formatı: SKU | Ürün Adı | Kategori | Alış Fiyatı | Satış Fiyatı | Stok Miktarı
        /// </summary>
        [RelayCommand]
        private void ImportExcel()
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "Excel Dosyası (*.xlsx)|*.xlsx|Tüm Dosyalar (*.*)|*.*",
                Title = "Ürün Listesi İçe Aktar"
            };

            if (openDialog.ShowDialog() != true) return;

            try
            {
                using var workbook = new XLWorkbook(openDialog.FileName);
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1); // İlk satır header

                if (rows == null)
                {
                    StatusMessage = "Excel dosyasında veri bulunamadı.";
                    IsActionSuccessful = false;
                    return;
                }

                int addedCount = 0;
                int updatedCount = 0;
                int skippedCount = 0;
                var notFoundCategories = new List<string>();

                foreach (var row in rows)
                {
                    try
                    {
                        // Sütunları oku
                        var sku = row.Cell(1).GetValue<string>()?.Trim();
                        var productName = row.Cell(2).GetValue<string>()?.Trim();
                        var categoryName = row.Cell(3).GetValue<string>()?.Trim();
                        var purchasePriceStr = row.Cell(4).GetValue<string>();
                        var salePriceStr = row.Cell(5).GetValue<string>();
                        var stockQtyStr = row.Cell(6).GetValue<string>();

                        // Boş satırları atla
                        if (string.IsNullOrEmpty(sku) && string.IsNullOrEmpty(productName))
                        {
                            continue;
                        }

                        // Fiyatları parse et
                        decimal.TryParse(purchasePriceStr, out decimal purchasePrice);
                        decimal.TryParse(salePriceStr, out decimal salePrice);
                        int.TryParse(stockQtyStr, out int stockQty);

                        // Kategori kontrolü ve dinamik oluşturma
                        Category? category = null;
                        if (!string.IsNullOrEmpty(categoryName))
                        {
                            category = _context.Categories
                                .FirstOrDefault(c => c.Name.ToLower() == categoryName.ToLower());

                            if (category == null)
                            {
                                // Kategori yoksa oluştur
                                category = new Category { Name = categoryName };
                                _context.Categories.Add(category);
                                _context.SaveChanges();
                                notFoundCategories.Add(categoryName);
                            }
                        }

                        // Mevcut ürün var mı kontrol et (SKU ile)
                        var existingProduct = !string.IsNullOrEmpty(sku)
                            ? _context.Products.FirstOrDefault(p => p.SKU != null && p.SKU.ToLower() == sku.ToLower())
                            : null;

                        if (existingProduct != null)
                        {
                            // GÜNCELLE
                            existingProduct.ProductName = productName ?? existingProduct.ProductName;
                            existingProduct.PurchasePrice = purchasePrice;
                            existingProduct.SalePrice = salePrice;
                            if (category != null) existingProduct.CategoryId = category.Id;

                            _context.Products.Update(existingProduct);
                            updatedCount++;
                        }
                        else
                        {
                            // YENİ ÜRÜN EKLE
                            var newProduct = new Product
                            {
                                SKU = sku ?? $"IMP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                                ProductName = productName ?? "İsimsiz Ürün",
                                PurchasePrice = purchasePrice,
                                SalePrice = salePrice,
                                CategoryId = category?.Id,
                                Unit = "Adet",
                                VatRate = 20,
                                Currency = "TRY"
                            };

                            _context.Products.Add(newProduct);
                            _context.SaveChanges(); // Product ID almak için kaydet

                            // Stok miktarı varsa Inventory ve StockTransaction oluştur
                            if (stockQty > 0)
                            {
                                // Ana Depo'yu bul veya oluştur
                                var mainWarehouse = _context.Warehouses.FirstOrDefault(w => w.IsActive);
                                if (mainWarehouse == null)
                                {
                                    mainWarehouse = new Warehouse
                                    {
                                        Name = "Ana Depo",
                                        Type = WarehouseType.MainWarehouse,
                                        IsActive = true
                                    };
                                    _context.Warehouses.Add(mainWarehouse);
                                    _context.SaveChanges();
                                }

                                // Inventory kaydı oluştur
                                var inventory = new Inventory
                                {
                                    ProductId = newProduct.Id,
                                    WarehouseId = mainWarehouse.Id,
                                    Quantity = stockQty
                                };
                                _context.Inventories.Add(inventory);

                                // StockTransaction kaydı oluştur
                                var transaction = new StockTransaction
                                {
                                    ProductId = newProduct.Id,
                                    TargetWarehouseId = mainWarehouse.Id,
                                    Quantity = stockQty,
                                    TransactionType = StockTransactionType.OpeningStock,
                                    UnitCost = purchasePrice,
                                    Date = DateTime.UtcNow,
                                    Description = "Excel Açılış Stoğu"
                                };
                                _context.StockTransactions.Add(transaction);

                                // Toplam stok güncelle
                                newProduct.TotalStockQuantity = stockQty;
                                _context.SaveChanges();
                            }

                            addedCount++;
                        }
                    }
                    catch
                    {
                        skippedCount++;
                    }
                }

                _context.SaveChanges();

                // Sonuç mesajı
                var message = $"İçe aktarım tamamlandı.\n\n" +
                              $"✅ Eklenen: {addedCount} ürün\n" +
                              $"🔄 Güncellenen: {updatedCount} ürün\n" +
                              $"⏭️ Atlanan: {skippedCount} satır";

                if (notFoundCategories.Count > 0)
                {
                    message += $"\n\n📁 Oluşturulan yeni kategoriler:\n{string.Join(", ", notFoundCategories.Distinct())}";
                }

                MessageBox.Show(message, "İçe Aktarım Sonucu", MessageBoxButton.OK, MessageBoxImage.Information);

                StatusMessage = $"{addedCount} ürün eklendi, {updatedCount} ürün güncellendi.";
                IsActionSuccessful = true;

                // Listeyi yenile
                RefreshProductList();
            }
            catch (Exception ex)
            {
                StatusMessage = $"İçe aktarım hatası: {ex.Message}";
                IsActionSuccessful = false;
                MessageBox.Show($"Excel okuma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Stok transfer penceresini aç
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsProductSelected))]
        private void TransferStock()
        {
            if (SelectedProduct == null) return;

            var vm = new StockTransferViewModel(_inventoryDomainService, _dbContextFactory);
            var window = new Views.StockTransferView(vm);

            // ViewModel'deki SelectedProduct'ı set et
            vm.SelectedProduct = SelectedProduct;

            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();

            // Transfer sonrası ana listedeki stok miktarını güncelle
            RefreshProductList();
        }

        /// <summary>
        /// Ürün listesini yenile
        /// </summary>
        private void RefreshProductList()
        {
            // Context'i yenile
            _context.ChangeTracker.Clear();
            LoadProducts();
            _productsView?.Refresh();
        }

        #region Product Photo Management

        /// <summary>
        /// Dosya seçici açar, seçilen görseli ProductImageService ile sıkıştırıp kaydeder
        /// ve Product.ImagePath'i günceller.
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsProductSelected))]
        private async Task UploadProductPhoto()
        {
            if (SelectedProduct == null) return;

            var dialog = new OpenFileDialog
            {
                Title = "Ürün Fotoğrafı Seç",
                Filter = "Resim Dosyaları (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                // Eski görseli sil
                _imageService.DeleteProductImage(SelectedProduct.ImagePath);

                // Yeni görseli sıkıştırıp kaydet (relative path döner)
                var relativePath = await _imageService.SaveProductImageAsync(dialog.FileName);

                // DB güncelle
                var dbProduct = _context.Products.Find(SelectedProduct.Id);
                if (dbProduct != null)
                {
                    dbProduct.ImagePath = relativePath;
                    _context.SaveChanges();
                }

                SelectedProduct.ImagePath = relativePath;

                var fileName = Path.GetFileName(relativePath);
                StatusMessage = $"✅ Fotoğraf güncellendi: {fileName}";
                IsActionSuccessful = true;

                RefreshProductList();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fotoğraf yükleme hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        /// <summary>
        /// Ürün fotoğrafını siler ve ImagePath'i temizler.
        /// </summary>
        [RelayCommand(CanExecute = nameof(HasProductPhoto))]
        private void DeleteProductPhoto()
        {
            if (SelectedProduct?.ImagePath == null) return;

            var result = MessageBox.Show(
                $"'{SelectedProduct.ProductName}' ürününün fotoğrafını silmek istediğinizden emin misiniz?",
                "Fotoğrafı Sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                // ProductImageService ile sil (relative path destekli)
                _imageService.DeleteProductImage(SelectedProduct.ImagePath);

                var dbProduct = _context.Products.Find(SelectedProduct.Id);
                if (dbProduct != null)
                {
                    dbProduct.ImagePath = null;
                    _context.SaveChanges();
                }

                SelectedProduct.ImagePath = null;
                StatusMessage = "Fotoğraf silindi.";
                IsActionSuccessful = true;

                RefreshProductList();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fotoğraf silme hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        [RelayCommand]
        private void QuickAdjustStock(Product? product)
        {
            var target = product ?? SelectedProduct;
            if (target == null) return;

            var editVm = App.ServiceProvider.GetRequiredService<AddProductViewModel>();
            editVm.Initialize(target);

            var window = new Views.AddProductWindow
            {
                DataContext = editVm,
                Owner = System.Windows.Application.Current?.MainWindow
            };
            var result = window.ShowDialog();
            if (result == true)
            {
                RefreshProductList();
            }
        }

        #endregion
    }
}
