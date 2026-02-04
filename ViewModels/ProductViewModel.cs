using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ClosedXML.Excel;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Stok/Ürün yönetimi ViewModel - GÜNCELLENMİŞ VERSİYON
    /// </summary>
    public class ProductViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
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
            set => SetProperty(ref _selectedProduct, value);
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

        /// <summary>
        /// Yeni Ürün Ekle Komutu
        /// </summary>
        public ICommand AddNewProductCommand { get; }

        /// <summary>
        /// Ürün Düzenle Komutu
        /// </summary>
        public ICommand EditProductCommand { get; }

        /// <summary>
        /// Excel'den İçe Aktar Komutu
        /// </summary>
        public ICommand ImportExcelCommand { get; }

        /// <summary>
        /// Stok Transfer Komutu
        /// </summary>
        public ICommand TransferStockCommand { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ProductViewModel()
        {
            _context = new AppDbContext();
            Products = new ObservableCollection<Product>();

            // Komutları tanımla
            AddNewProductCommand = new RelayCommand(_ => AddNewProduct());
            EditProductCommand = new RelayCommand(_ => EditProduct(), _ => SelectedProduct != null);
            ImportExcelCommand = new RelayCommand(_ => ImportFromExcel());
            TransferStockCommand = new RelayCommand(_ => TransferStock(), _ => SelectedProduct != null);

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
        }

        /// <summary>
        /// Ürün filtreleme mantığı
        /// </summary>
        private bool FilterProducts(object obj)
        {
            if (obj is not Product product) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            var search = SearchText.ToLower();

            // Ürün Adı, SKU veya Kategori Adı içinde arama yap
            return product.ProductName.ToLower().Contains(search) ||
                   (product.SKU != null && product.SKU.ToLower().Contains(search)) ||
                   (product.Category != null && product.Category.Name.ToLower().Contains(search));
        }

        /// <summary>
        /// Yeni ürün ekleme penceresini aç
        /// </summary>
        private void AddNewProduct()
        {
            var window = new Views.AddProductWindow();
            window.Owner = Application.Current.MainWindow;
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
        private void EditProduct()
        {
            if (SelectedProduct == null) return;

            var window = new Views.AddProductWindow(SelectedProduct);
            window.Owner = Application.Current.MainWindow;
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
        /// Excel'den ürün içe aktar
        /// Excel formatı: SKU | Ürün Adı | Kategori | Alış Fiyatı | Satış Fiyatı | Stok Miktarı
        /// </summary>
        private void ImportFromExcel()
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
                                SKU = sku ?? $"IMP-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
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
                                    Date = DateTime.Now,
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
        private void TransferStock()
        {
            if (SelectedProduct == null) return;

            var window = new Views.StockTransferView();

            // ViewModel'deki SelectedProduct'ı set et
            if (window.DataContext is StockTransferViewModel vm)
            {
                vm.SelectedProduct = SelectedProduct;
            }

            window.Owner = Application.Current.MainWindow;
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
    }
}
