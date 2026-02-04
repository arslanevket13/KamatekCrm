using System;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Models.Specs;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Ürün Ekleme/Düzenleme ViewModel - Add ve Edit modlarını destekler
    /// </summary>
    public class AddProductViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private ProductCategoryType _selectedCategory = ProductCategoryType.Other;
        private ProductSpecBase _currentSpecs;
        private Product _newProduct;
        private bool _isEditMode;
        private int _initialStock;
        private int _stockAdjustment;

        /// <summary>
        /// Yeni ürün eklemek için constructor
        /// </summary>
        public AddProductViewModel() : this(null) { }

        /// <summary>
        /// Ürün eklemek veya düzenlemek için constructor
        /// </summary>
        /// <param name="productToEdit">Düzenlenecek ürün. Null ise yeni ürün eklenir.</param>
        public AddProductViewModel(Product? productToEdit)
        {
            _context = new AppDbContext();

            if (productToEdit != null && productToEdit.Id > 0)
            {
                // EDIT MODE: Mevcut ürünü yükle
                _isEditMode = true;

                // Context'ten ürünü al (tracking için)
                var existingProduct = _context.Products.Find(productToEdit.Id);
                if (existingProduct != null)
                {
                    _newProduct = existingProduct;
                    _selectedCategory = existingProduct.ProductCategoryType;


                    // Teknik özellikleri deserialize et
                    if (!string.IsNullOrEmpty(existingProduct.TechSpecsJson))
                    {
                        try
                        {
                            _currentSpecs = DeserializeSpecs(existingProduct.TechSpecsJson, _selectedCategory);
                        }
                        catch
                        {
                            _currentSpecs = CreateSpecsForCategory(_selectedCategory);
                        }
                    }
                    else
                    {
                        _currentSpecs = CreateSpecsForCategory(_selectedCategory);
                    }
                }
                else
                {
                    // Ürün bulunamadı, yeni ürün moduna geç
                    InitializeNewProduct();
                }
            }
            else
            {
                // ADD MODE: Yeni ürün oluştur
                InitializeNewProduct();

                // Eğer taslak ürün geldiyse (örn: Satın alma ekranından isimle gelmesi)
                if (productToEdit != null && productToEdit.Id == 0)
                {
                    if (!string.IsNullOrEmpty(productToEdit.ProductName))
                        _newProduct.ProductName = productToEdit.ProductName;
                }
            }

            // Komutları tanımla
            SaveCommand = new RelayCommand(_ => SaveProduct(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => Cancel());
            GenerateSKUCommand = new RelayCommand(_ => RegenerateSKU());
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
        public Array Categories => Enum.GetValues(typeof(ProductCategory));

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

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand GenerateSKUCommand { get; }

        /// <summary>
        /// Pencere kapatma olayı
        /// </summary>
        public event Action<bool>? RequestClose;

        #endregion

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
        /// JSON'dan teknik özellikleri deserialize eder
        /// </summary>
        private ProductSpecBase DeserializeSpecs(string json, ProductCategoryType category)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            return category switch
            {
                ProductCategoryType.Camera => JsonSerializer.Deserialize<CameraSpecs>(json, options) ?? new CameraSpecs(),
                ProductCategoryType.Intercom => JsonSerializer.Deserialize<IntercomSpecs>(json, options) ?? new IntercomSpecs(),
                ProductCategoryType.FireAlarm => JsonSerializer.Deserialize<FireAlarmSpecs>(json, options) ?? new FireAlarmSpecs(),
                ProductCategoryType.BurglarAlarm => JsonSerializer.Deserialize<BurglarAlarmSpecs>(json, options) ?? new BurglarAlarmSpecs(),
                ProductCategoryType.SmartHome => JsonSerializer.Deserialize<SmartHomeSpecs>(json, options) ?? new SmartHomeSpecs(),
                ProductCategoryType.AccessControl => JsonSerializer.Deserialize<AccessControlSpecs>(json, options) ?? new AccessControlSpecs(),
                ProductCategoryType.Satellite => JsonSerializer.Deserialize<SatelliteSpecs>(json, options) ?? new SatelliteSpecs(),
                ProductCategoryType.FiberOptic => JsonSerializer.Deserialize<FiberSpecs>(json, options) ?? new FiberSpecs(),
                _ => JsonSerializer.Deserialize<GeneralSpecs>(json, options) ?? new GeneralSpecs()
            };
        }

        /// <summary>
        /// Benzersiz SKU kodu üretir
        /// </summary>
        private string GenerateSKU()
        {
            return $"PRD-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        }

        /// <summary>
        /// SKU'yu yeniden üretir
        /// </summary>
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
        private void SaveProduct()
        {
            try
            {
                // Teknik özellikleri JSON'a serialize et
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                NewProduct.TechSpecsJson = JsonSerializer.Serialize(CurrentSpecs, CurrentSpecs.GetType(), jsonOptions);

                if (_isEditMode)
                {
                    // UPDATE: Mevcut ürünü güncelle
                    _context.Products.Update(NewProduct);
                    _context.SaveChanges();

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
                    // ADD: Yeni ürün ekle
                    _context.Products.Add(NewProduct);
                    _context.SaveChanges();

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
                // Varsayılan depoyu bul veya oluştur
                var defaultWarehouse = _context.Warehouses.FirstOrDefault(w => w.IsActive);
                if (defaultWarehouse == null)
                {
                    defaultWarehouse = new Warehouse
                    {
                        Name = "Ana Depo",
                        Type = WarehouseType.MainWarehouse,
                        IsActive = true
                    };
                    _context.Warehouses.Add(defaultWarehouse);
                    _context.SaveChanges();
                }

                // Inventory kaydı oluştur
                var inventory = new Inventory
                {
                    ProductId = NewProduct.Id,
                    WarehouseId = defaultWarehouse.Id,
                    Quantity = InitialStock
                };
                _context.Inventories.Add(inventory);

                // StockTransaction kaydı oluştur
                var transaction = new StockTransaction
                {
                    ProductId = NewProduct.Id,
                    TargetWarehouseId = defaultWarehouse.Id,
                    Quantity = InitialStock,
                    TransactionType = StockTransactionType.OpeningStock,
                    UnitCost = NewProduct.PurchasePrice,
                    Date = DateTime.Now,
                    Description = "Açılış Stoğu"
                };
                _context.StockTransactions.Add(transaction);

                // Ürün toplam stok güncelle
                NewProduct.TotalStockQuantity = InitialStock;
                _context.Products.Update(NewProduct);

                _context.SaveChanges();
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
                // Varsayılan depoyu bul
                var defaultWarehouse = _context.Warehouses.FirstOrDefault(w => w.IsActive);
                if (defaultWarehouse == null)
                {
                    defaultWarehouse = new Warehouse
                    {
                        Name = "Ana Depo",
                        Type = WarehouseType.MainWarehouse,
                        IsActive = true
                    };
                    _context.Warehouses.Add(defaultWarehouse);
                    _context.SaveChanges();
                }

                // Mevcut inventory'yi bul veya oluştur
                var inventory = _context.Inventories
                    .FirstOrDefault(i => i.ProductId == NewProduct.Id && i.WarehouseId == defaultWarehouse.Id);

                if (inventory == null)
                {
                    inventory = new Inventory
                    {
                        ProductId = NewProduct.Id,
                        WarehouseId = defaultWarehouse.Id,
                        Quantity = 0
                    };
                    _context.Inventories.Add(inventory);
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
                    Date = DateTime.Now,
                    Description = StockAdjustment > 0 ? "Manuel Stok Artışı" : "Manuel Stok Azaltması"
                };
                _context.StockTransactions.Add(transaction);

                // Ürün toplam stok güncelle
                NewProduct.TotalStockQuantity += StockAdjustment;
                _context.Products.Update(NewProduct);

                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Stok düzeltme hatası: {ex.Message}", "Uyarı",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion
    }
}
