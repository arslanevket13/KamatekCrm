using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Models.Specs;
using KamatekCrm.Services;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Ürün Ekleme/Düzenleme ViewModel - Add ve Edit modlarını destekler
    /// </summary>
    public partial class AddProductViewModel : ViewModelBase
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IProductImageService _imageService;
        private ProductCategoryType _selectedCategory = ProductCategoryType.Other;
        private ProductSpecBase _currentSpecs = new GeneralSpecs();
        private Product _newProduct = new Product();
        private bool _isEditMode;
        private int _initialStock;
        private int _stockAdjustment;
        private string? _pendingImagePath; // Source file path before compression
        private BitmapImage? _selectedImagePreview;

        /// <summary>
        /// Yeni ürün eklemek için constructor
        /// </summary>
        /// <summary>
        /// Yeni ürün eklemek için constructor
        /// </summary>
        /// <summary>
        /// DI Constructor
        /// </summary>
        public AddProductViewModel(IDbContextFactory<AppDbContext> dbContextFactory, IProductImageService? imageService = null)
        {
            _dbContextFactory = dbContextFactory;
            _imageService = imageService ?? new ProductImageService();
        }

        /// <summary>
        /// Ürün eklemek veya düzenlemek için başlatma metodu
        /// </summary>
        /// <param name="productToEdit">Düzenlenecek ürün. Null ise yeni ürün eklenir.</param>
        public void Initialize(Product? productToEdit)
        {
            if (_currentSpecs == null) _currentSpecs = new GeneralSpecs();
            if (_newProduct == null) _newProduct = new Product(); 

            if (productToEdit != null && productToEdit.Id > 0)
            {
                // EDIT MODE: Mevcut ürünü yükle
                _isEditMode = true;

                using var context = _dbContextFactory.CreateDbContext();
                var existingProduct = context.Products.Find(productToEdit.Id);
                if (existingProduct != null)
                {
                    _newProduct = existingProduct;
                    _selectedCategory = existingProduct.ProductCategoryType;

                    if (existingProduct.Specifications != null)
                    {
                        _currentSpecs = existingProduct.Specifications;
                    }
                    else
                    {
                        _currentSpecs = CreateSpecsForCategory(_selectedCategory);
                    }
                }
                else
                {
                    InitializeNewProduct();
                }
            }
            else
            {
                // ADD MODE: Yeni ürün oluştur
                InitializeNewProduct();

                if (productToEdit != null && productToEdit.Id == 0)
                {
                    if (!string.IsNullOrEmpty(productToEdit.ProductName))
                        _newProduct.ProductName = productToEdit.ProductName;
                }
            }

            if (_isEditMode && !string.IsNullOrEmpty(_newProduct.ImagePath))
            {
                LoadExistingImagePreview();
            }
        }

        private void InitializeNewProduct()
        {
            _isEditMode = false;
            _newProduct = new Product
            {
                SKU = GenerateSKU(),
                Unit = "Adet",
                VatRate = 20,
                Currency = "TRY"
            };
            _currentSpecs = new GeneralSpecs();
        }

        #region Properties

        /// <summary>
        /// Düzenleme modu mu?
        /// </summary>
        public bool IsEditMode => _isEditMode;

        /// <summary>
        /// Pencere başlığı
        /// </summary>
        public string WindowTitle => _isEditMode ? "Ürün Düzenle" : "Yeni Stok Kartı Oluştur";

        /// <summary>
        /// Kaydet butonu metni
        /// </summary>
        public string SaveButtonText => _isEditMode ? "Güncelle" : "Kaydet";

        /// <summary>
        /// Ürün nesnesi
        /// </summary>
        public Product NewProduct
        {
            get => _newProduct;
            set => SetProperty(ref _newProduct, value);
        }

        /// <summary>
        /// Seçili kategori
        /// </summary>
        public ProductCategoryType SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    NewProduct.ProductCategoryType = value;
                    CurrentSpecs = CreateSpecsForCategory(value);
                }
            }
        }

        /// <summary>
        /// Kategoriye göre dinamik teknik özellikler
        /// </summary>
        public ProductSpecBase CurrentSpecs
        {
            get => _currentSpecs;
            set => SetProperty(ref _currentSpecs, value);
        }

        /// <summary>
        /// Kategori listesi
        /// </summary>
        public Array Categories => Enum.GetValues(typeof(ProductCategoryType));

        /// <summary>
        /// KDV Oranları
        /// </summary>
        public int[] VatRates => new[] { 1, 10, 20 };

        /// <summary>
        /// Para Birimleri
        /// </summary>
        public string[] Currencies => new[] { "TRY", "USD", "EUR" };

        /// <summary>
        /// Birimler
        /// </summary>
        public string[] Units => new[] { "Adet", "Metre", "Kg", "Paket", "Kutu", "Rulo", "Set" };

        /// <summary>
        /// Açılış stok miktarı (Sadece Add modunda)
        /// </summary>
        public int InitialStock
        {
            get => _initialStock;
            set => SetProperty(ref _initialStock, value);
        }

        /// <summary>
        /// Açılış stoğu görünür mü? (Edit modunda gizle)
        /// </summary>
        public bool IsInitialStockVisible => !IsEditMode;

        /// <summary>
        /// Stok düzeltme miktarı (Edit modunda kullanılır, + veya - olabilir)
        /// </summary>
        public int StockAdjustment
        {
            get => _stockAdjustment;
            set => SetProperty(ref _stockAdjustment, value);
        }

        /// <summary>
        /// Stok düzeltme alanı görünür mü? (Sadece Edit modunda)
        /// </summary>
        public bool IsStockAdjustmentVisible => IsEditMode;

        /// <summary>
        /// Mevcut toplam stok (Edit modunda bilgi amaçlı)
        /// </summary>
        public int CurrentTotalStock => NewProduct?.TotalStockQuantity ?? 0;

        #endregion

        private bool CanRemoveImage() => !string.IsNullOrEmpty(_pendingImagePath) || !string.IsNullOrEmpty(NewProduct?.ImagePath);
        /// <summary>
        /// Pencere kapatma olayı
        /// </summary>
        public event Action<bool>? RequestClose;

        /// <summary>
        /// Seçilen resmin önizlemesi
        /// </summary>
        public BitmapImage? SelectedImagePreview
        {
            get => _selectedImagePreview;
            set => SetProperty(ref _selectedImagePreview, value);
        }

        /// <summary>
        /// Resim seçilmiş mi?
        /// </summary>
        public bool HasImage => _selectedImagePreview != null;


        #region Methods

        /// <summary>
        /// Kategoriye göre uygun Specs sınıfını oluşturur
        /// </summary>
        private ProductSpecBase CreateSpecsForCategory(ProductCategoryType category)

        {
            return category switch
            {
                ProductCategoryType.Camera => new CameraSpecs(),
                ProductCategoryType.Intercom => new IntercomSpecs(),
                ProductCategoryType.FireAlarm => new FireAlarmSpecs(),
                ProductCategoryType.BurglarAlarm => new BurglarAlarmSpecs(),
                ProductCategoryType.SmartHome => new SmartHomeSpecs(),
                ProductCategoryType.AccessControl => new AccessControlSpecs(),
                ProductCategoryType.Satellite => new SatelliteSpecs(),
                ProductCategoryType.FiberOptic => new FiberSpecs(),
                _ => new GeneralSpecs()
            };
        }



        /// <summary>
        /// Benzersiz SKU kodu üretir
        /// </summary>
        private string GenerateSKU()
        {
            return $"PRD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        }

        /// <summary>
        /// SKU'yu yeniden üretir
        /// </summary>
        [RelayCommand]
        private void RegenerateSKU()
        {
            NewProduct.SKU = GenerateSKU();
            OnPropertyChanged(nameof(NewProduct));
        }

        /// <summary>
        /// Kaydetme kontrolü
        /// </summary>
        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(NewProduct.ProductName) &&
                   NewProduct.SalePrice >= 0;
        }

        /// <summary>
        /// Ürünü kaydet veya güncelle
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task Save()
        {
            try
            {
                // Teknik özellikleri ata (EF Core ToJson mapping)
                NewProduct.Specifications = CurrentSpecs;

                using var context = _dbContextFactory.CreateDbContext();

                if (_isEditMode)
                {
                    // Resim işleme (varsa)
                    if (!string.IsNullOrEmpty(_pendingImagePath))
                    {
                        // Eski resmi sil
                        _imageService.DeleteProductImage(NewProduct.ImagePath);
                        NewProduct.ImagePath = await _imageService.SaveProductImageAsync(_pendingImagePath);
                    }

                    // UPDATE: Mevcut ürünü güncelle
                    context.Products.Update(NewProduct);
                    context.SaveChanges();

                    // Stok düzeltme varsa işle
                    if (StockAdjustment != 0)
                    {
                        AdjustStock();
                    }

                    MessageBox.Show("Ürün başarıyla güncellendi!", "Başarılı",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Resim işleme (varsa)
                    if (!string.IsNullOrEmpty(_pendingImagePath))
                    {
                        NewProduct.ImagePath = await _imageService.SaveProductImageAsync(_pendingImagePath);
                    }

                    // ADD: Yeni ürün ekle
                    context.Products.Add(NewProduct);
                    context.SaveChanges();

                    // Açılış stoğu varsa Inventory ve Transaction oluştur
                    if (InitialStock > 0)
                    {
                        CreateInitialStock();
                    }

                    MessageBox.Show("Ürün başarıyla kaydedildi!", "Başarılı",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// İptal et
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(false);
        }

        /// <summary>
        /// Açılış stoğu oluştur (Inventory + StockTransaction)
        /// </summary>
        private void CreateInitialStock()
        {
            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                // Varsayılan depoyu bul veya oluştur
                var defaultWarehouse = context.Warehouses.FirstOrDefault(w => w.IsActive);
                if (defaultWarehouse == null)
                {
                    defaultWarehouse = new Warehouse
                    {
                        Name = "Ana Depo",
                        Type = WarehouseType.MainWarehouse,
                        IsActive = true
                    };
                    context.Warehouses.Add(defaultWarehouse);
                    context.SaveChanges();
                }

                // Inventory kaydı oluştur
                var inventory = new Inventory
                {
                    ProductId = NewProduct.Id,
                    WarehouseId = defaultWarehouse.Id,
                    Quantity = InitialStock
                };
                context.Inventories.Add(inventory);

                // StockTransaction kaydı oluştur
                var transaction = new StockTransaction
                {
                    ProductId = NewProduct.Id,
                    TargetWarehouseId = defaultWarehouse.Id,
                    Quantity = InitialStock,
                    TransactionType = StockTransactionType.OpeningStock,
                    UnitCost = NewProduct.PurchasePrice,
                    Date = DateTime.UtcNow,
                    Description = "Açılış Stoğu"
                };
                context.StockTransactions.Add(transaction);

                // Ürün toplam stok güncelle
                NewProduct.TotalStockQuantity = InitialStock;
                context.Products.Update(NewProduct);

                context.SaveChanges();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Açılış stoğu oluşturulurken hata: {ex.Message}", "Uyarı",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Stok düzeltme işlemi (Edit modunda kullanılır)
        /// </summary>
        private void AdjustStock()
        {
            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                // Varsayılan depoyu bul
                var defaultWarehouse = context.Warehouses.FirstOrDefault(w => w.IsActive);
                if (defaultWarehouse == null)
                {
                    defaultWarehouse = new Warehouse
                    {
                        Name = "Ana Depo",
                        Type = WarehouseType.MainWarehouse,
                        IsActive = true
                    };
                    context.Warehouses.Add(defaultWarehouse);
                    context.SaveChanges();
                }

                // Mevcut inventory'yi bul veya oluştur
                var inventory = context.Inventories
                    .FirstOrDefault(i => i.ProductId == NewProduct.Id && i.WarehouseId == defaultWarehouse.Id);

                if (inventory == null)
                {
                    inventory = new Inventory
                    {
                        ProductId = NewProduct.Id,
                        WarehouseId = defaultWarehouse.Id,
                        Quantity = 0
                    };
                    context.Inventories.Add(inventory);
                }

                // Inventory miktarını güncelle
                inventory.Quantity += StockAdjustment;

                // StockTransaction kaydı oluştur
                var transactionType = StockAdjustment > 0
                    ? StockTransactionType.AdjustmentPlus
                    : StockTransactionType.AdjustmentMinus;

                var transaction = new StockTransaction
                {
                    ProductId = NewProduct.Id,
                    TargetWarehouseId = defaultWarehouse.Id,
                    Quantity = Math.Abs(StockAdjustment),
                    TransactionType = transactionType,
                    UnitCost = NewProduct.PurchasePrice,
                    Date = DateTime.UtcNow,
                    Description = StockAdjustment > 0 ? "Manuel Stok Artışı" : "Manuel Stok Azaltması"
                };
                context.StockTransactions.Add(transaction);

                // Ürün toplam stok güncelle
                NewProduct.TotalStockQuantity += StockAdjustment;
                context.Products.Update(NewProduct);

                context.SaveChanges();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Stok düzeltme hatası: {ex.Message}", "Uyarı",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Dosya seçici ile resim seç
        /// </summary>
        [RelayCommand]
        private void BrowseImage()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Ürün Resmi Seç",
                Filter = "Resim Dosyaları|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Tüm Dosyalar|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                _pendingImagePath = dialog.FileName;

                // Önizleme göster
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_pendingImagePath);
                    bitmap.DecodePixelWidth = 200;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    SelectedImagePreview = bitmap;
                    OnPropertyChanged(nameof(HasImage));
                }
                catch { /* Önizleme yüklenemezse sessizce atla */ }
            }
        }

        /// <summary>
        /// Seçilen resmi kaldır
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRemoveImage))]
        private void RemoveImage()
        {
            _pendingImagePath = null;
            SelectedImagePreview = null;
            OnPropertyChanged(nameof(HasImage));

            if (_isEditMode && !string.IsNullOrEmpty(NewProduct?.ImagePath))
            {
                _imageService.DeleteProductImage(NewProduct.ImagePath);
                NewProduct.ImagePath = null;
            }
        }

        /// <summary>
        /// Mevcut ürün resmini önizle (Edit modunda)
        /// </summary>
        private void LoadExistingImagePreview()
        {
            try
            {
                var absolutePath = _imageService.GetAbsolutePath(NewProduct.ImagePath!);
                if (System.IO.File.Exists(absolutePath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(absolutePath);
                    bitmap.DecodePixelWidth = 200;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    SelectedImagePreview = bitmap;
                    OnPropertyChanged(nameof(HasImage));
                }
            }
            catch { /* Resim yüklenemezse sessizce atla */ }
        }

        #endregion
    }
}
