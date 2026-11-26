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
using KamatekCrm.Shared.Helpers;
using KamatekCrm.Shared.Services;
using KamatekCrm.Events;
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
        private readonly IDialogService? _dialogService;
        private readonly IToastService? _toastService;
        private ProductCategoryType _selectedCategory = ProductCategoryType.Other;
        private ProductSpecBase _currentSpecs = new GeneralSpecs();
        private Product _newProduct = new Product();
        private bool _isEditMode;
        private int _originalStock;
        private int _initialStock;
        private int _stockAdjustment;
        private string? _pendingImagePath; // Source file path before compression
        private BitmapImage? _selectedImagePreview;

        /// <summary>
        /// DI Constructor
        /// </summary>
        public AddProductViewModel(
            IDbContextFactory<AppDbContext> dbContextFactory,
            IProductImageService? imageService = null,
            IDialogService? dialogService = null,
            IToastService? toastService = null)
        {
            _dbContextFactory = dbContextFactory;
            _imageService = imageService ?? new ProductImageService();
            _dialogService = dialogService;
            _toastService = toastService;
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
                    _originalStock = existingProduct.TotalStockQuantity;
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

        private bool _isSaving;
        private string _validationMessage = string.Empty;

        /// <summary>
        /// Kayıt işlemi sürüyor mu?
        /// </summary>
        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                if (SetProperty(ref _isSaving, value))
                {
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Satır içi doğrulama hata mesajı
        /// </summary>
        public string ValidationMessage
        {
            get => _validationMessage;
            private set
            {
                if (SetProperty(ref _validationMessage, value))
                {
                    OnPropertyChanged(nameof(HasValidationError));
                }
            }
        }

        public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

        /// <summary>
        /// Ürün adı (CanSave bildirimi için observable proxy)
        /// </summary>
        public string ProductName
        {
            get => _newProduct.ProductName;
            set
            {
                var next = value ?? string.Empty;
                if (!string.Equals(_newProduct.ProductName, next, StringComparison.Ordinal))
                {
                    _newProduct.ProductName = next;
                    OnPropertyChanged();
                    if (!string.IsNullOrWhiteSpace(next) && HasValidationError)
                    {
                        ValidationMessage = string.Empty;
                    }
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Alış fiyatı (kâr marjı bildirimi için observable proxy)
        /// </summary>
        public decimal PurchasePrice
        {
            get => _newProduct.PurchasePrice;
            set
            {
                if (_newProduct.PurchasePrice != value)
                {
                    _newProduct.PurchasePrice = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProfitMarginDisplay));
                    OnPropertyChanged(nameof(ProfitColor));
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Satış fiyatı (CanSave ve kâr marjı bildirimi için observable proxy)
        /// </summary>
        public decimal SalePrice
        {
            get => _newProduct.SalePrice;
            set
            {
                if (_newProduct.SalePrice != value)
                {
                    _newProduct.SalePrice = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProfitMarginDisplay));
                    OnPropertyChanged(nameof(ProfitColor));
                    if (value >= 0 && HasValidationError)
                    {
                        ValidationMessage = string.Empty;
                    }
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Kısa stok kodu (SKU) — kullanıcı tarafından serbestçe düzenlenebilir
        /// </summary>
        public string Sku
        {
            get => _newProduct.SKU;
            set
            {
                var next = value ?? string.Empty;
                if (!string.Equals(_newProduct.SKU, next, StringComparison.Ordinal))
                {
                    _newProduct.SKU = next;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SkuBarcodePreview));
                    PrintBarcodeLabelCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Barkod (EAN-13 veya barkod okuyucu girdisi)
        /// </summary>
        public string Barcode
        {
            get => _newProduct.Barcode;
            set
            {
                var next = value ?? string.Empty;
                if (!string.Equals(_newProduct.Barcode, next, StringComparison.Ordinal))
                {
                    _newProduct.Barcode = next;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SkuBarcodePreview));
                    PrintBarcodeLabelCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string SkuBarcodePreview
        {
            get
            {
                var skuText = string.IsNullOrWhiteSpace(Sku) ? "—" : Sku.Trim();
                var barcodeText = string.IsNullOrWhiteSpace(Barcode) ? "Henüz tanımlı değil" : Barcode.Trim();
                var formatHint = ProductCodeHelper.IsValidEan13(Barcode) ? " [EAN-13]" : string.Empty;
                return $"SKU: {skuText}   •   Barkod: {barcodeText}{formatHint}";
            }
        }

        /// <summary>
        /// Ürün nesnesi
        /// </summary>
        public Product NewProduct
        {
            get => _newProduct;
            set
            {
                if (SetProperty(ref _newProduct, value))
                {
                    OnPropertyChanged(nameof(ProductName));
                    OnPropertyChanged(nameof(Sku));
                    OnPropertyChanged(nameof(Barcode));
                    OnPropertyChanged(nameof(SkuBarcodePreview));
                    OnPropertyChanged(nameof(PurchasePrice));
                    OnPropertyChanged(nameof(SalePrice));
                    SaveCommand.NotifyCanExecuteChanged();
                    PrintBarcodeLabelCommand.NotifyCanExecuteChanged();
                }
            }
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
        /// <summary>
        /// Kategoriye uygun standart teknik özellik şablonunu varsayılan değerlerle oluşturur
        /// </summary>
        private ProductSpecBase CreateSpecsForCategory(ProductCategoryType category)
        {
            return category switch
            {
                ProductCategoryType.Camera => new CameraSpecs
                {
                    Resolution = "4 MP (2560x1440)",
                    LensType = "2.8mm Sabit Lens",
                    CameraType = "Dome IP Kamera",
                    IRDistance = "30 Metre Smart IR",
                    IpRating = "IP67",
                    Compression = "H.265+",
                    IsPoE = true,
                    HasAudio = true,
                    HasColorVu = false,
                    Notes = "Standart güvenlik kamerası konfigürasyonu"
                },
                ProductCategoryType.Intercom => new IntercomSpecs
                {
                    ScreenSize = "7 inç Dokunmatik TFT LCD",
                    ConnectionType = "IP / PoE",
                    MountingType = "Sıva Üstü",
                    HasWiFi = true,
                    HasMobileApp = true,
                    HasMemory = true,
                    Notes = "Görüntülü diafon iç/dış ünite"
                },
                ProductCategoryType.FireAlarm => new FireAlarmSpecs
                {
                    DetectorType = "Optik Duman Dedektörü",
                    SystemType = "Adresli Yangın Algılama",
                    ComponentType = "Dedektör & Taban",
                    IsWireless = false,
                    HasRelay = true,
                    Notes = "EN54-7 sertifikalı yangın dedektörü"
                },
                ProductCategoryType.BurglarAlarm => new BurglarAlarmSpecs
                {
                    ConnectionType = "Kablosuz (868 MHz)",
                    ComponentType = "PIR Hareket Dedektörü",
                    DetectionType = "12m 85.9° Algılama Açısı",
                    HasGSM = true,
                    HasWiFi = true,
                    IsPetImmune = true,
                    Notes = "Evcil hayvan bağışıklıklı hırsız alarm sensörü"
                },
                ProductCategoryType.SmartHome => new SmartHomeSpecs
                {
                    Protocol = "Zigbee 3.0 / Wi-Fi",
                    ModuleType = "Röle / Anahtar Modülü",
                    LoadType = "16A Rezistif Yük",
                    RequiresHub = true,
                    HasSceneSupport = true,
                    Notes = "Akıllı ev otomasyon modülü"
                },
                ProductCategoryType.AccessControl => new AccessControlSpecs
                {
                    ReaderFrequency = "13.56 MHz Mifare / 125 KHz EM",
                    ComponentType = "Kartlı ve Şifreli Geçiş Terminali",
                    CommunicationType = "TCP/IP, Wiegand 26/34",
                    IsWaterproof = true,
                    HasFingerprint = true,
                    HasFaceRecognition = false,
                    Notes = "Geçiş kontrol ve PDKS terminali"
                },
                ProductCategoryType.Satellite => new SatelliteSpecs
                {
                    ComponentType = "Merkezi Uydu Multiswitch",
                    DishDiameter = "90 cm Ofset Çanak",
                    LnbOutputs = "Quattro LNB",
                    Material = "Galvaniz Sac / Alüminyum",
                    IsMotorized = false,
                    HasCardSlot = false,
                    Notes = "Merkezi TV dağıtım sistemi bileşeni"
                },
                ProductCategoryType.FiberOptic => new FiberSpecs
                {
                    FiberMode = "Singlemode (OS2) 9/125",
                    CoreCount = "4 Core / 8 Core",
                    CableType = "Zırhlı Dış Ortam (Loose Tube)",
                    ConnectorType = "SC/APC - LC/UPC",
                    IsArmored = true,
                    Notes = "Düşük kayıplı fiber optik omurga kablosu"
                },
                _ => new GeneralSpecs
                {
                    Material = "Metal / ABS Plastik",
                    Color = "Beyaz / Siyah",
                    Warranty = "2 Yıl Üretici Garantisi",
                    Notes = "Genel sarf ve donanım ürünü"
                }
            };
        }

        /// <summary>
        /// Seçili kategoriye ait standart özellikleri form alanlarına şablon olarak yükler
        /// </summary>
        [RelayCommand]
        public void LoadCategoryTemplate()
        {
            CurrentSpecs = CreateSpecsForCategory(SelectedCategory);
        }



        /// <summary>
        /// Kar Tutarı (₺)
        /// </summary>
        public decimal ProfitAmount => (NewProduct?.SalePrice ?? 0) - (NewProduct?.PurchasePrice ?? 0);

        /// <summary>
        /// Kar Marjı (%)
        /// </summary>
        public decimal ProfitMarginPercent
        {
            get
            {
                var cost = NewProduct?.PurchasePrice ?? 0;
                if (cost <= 0) return 0;
                return Math.Round((ProfitAmount / cost) * 100, 1);
            }
        }

        public string ProfitMarginDisplay => ProfitAmount >= 0 
            ? $"📈 Kar: ₺{ProfitAmount:N2} (%{ProfitMarginPercent:F1})"
            : $"📉 Zarar: ₺{Math.Abs(ProfitAmount):N2} (%{ProfitMarginPercent:F1})";

        public System.Windows.Media.Brush ProfitColor => ProfitAmount >= 0
            ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#4CAF50")!
            : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#F44336")!;

        public void NotifyFinancialCalculations()
        {
            OnPropertyChanged(nameof(ProfitAmount));
            OnPropertyChanged(nameof(ProfitMarginPercent));
            OnPropertyChanged(nameof(ProfitMarginDisplay));
            OnPropertyChanged(nameof(ProfitColor));
        }

        /// <summary>
        /// Benzersiz kısa SKU kodu üretir ({PREFIX}-{0001})
        /// </summary>
        private string GenerateSKU()
        {
            return ProductCodeHelper.GenerateShortSku(SelectedCategory);
        }

        /// <summary>
        /// Veritabanı benzersizlik kontrolü ile kısa SKU kodu üretir
        /// </summary>
        [RelayCommand]
        private async Task RegenerateSKU()
        {
            try
            {
                var prefix = ProductCodeHelper.GetCategoryPrefix(SelectedCategory);
                var prefixPattern = prefix + "-";
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var existingSkus = await context.Products.AsNoTracking()
                    .Where(p => !p.IsDeleted && p.SKU != null && p.SKU.ToUpper().StartsWith(prefixPattern))
                    .Select(p => p.SKU)
                    .ToListAsync();

                var nextSeq = ProductCodeHelper.ExtractNextSequence(prefix, existingSkus);
                Sku = ProductCodeHelper.GenerateShortSku(SelectedCategory, nextSeq);
                OnPropertyChanged(nameof(NewProduct));
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Sıralı kısa SKU üretilemedi; rastgele kısa SKU kullanılıyor.");
                Sku = GenerateSKU();
                OnPropertyChanged(nameof(NewProduct));
            }
        }

        /// <summary>
        /// Benzersiz dahili EAN-13 (200 + 9 rakam + kontrol basamağı) barkod üretir
        /// </summary>
        [RelayCommand]
        private async Task GenerateBarcode()
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                for (var attempt = 0; attempt < 10; attempt++)
                {
                    var candidate = ProductCodeHelper.GenerateEan13Barcode();
                    var exists = await context.Products.AsNoTracking()
                        .AnyAsync(p => !p.IsDeleted && p.Barcode == candidate);
                    if (!exists)
                    {
                        Barcode = candidate;
                        OnPropertyChanged(nameof(NewProduct));
                        return;
                    }
                }
                Barcode = ProductCodeHelper.GenerateEan13Barcode();
                OnPropertyChanged(nameof(NewProduct));
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Barkod benzersizlik kontrolü yapılamadı; yerel EAN-13 üretildi.");
                Barcode = ProductCodeHelper.GenerateEan13Barcode();
                OnPropertyChanged(nameof(NewProduct));
            }
        }

        private bool CanPrintBarcodeLabel() =>
            !string.IsNullOrWhiteSpace(Sku) || !string.IsNullOrWhiteSpace(Barcode);

        /// <summary>
        /// Stok kartından doğrudan Barkod Etiketi Yazdır penceresini açar
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPrintBarcodeLabel))]
        private void PrintBarcodeLabel()
        {
            var vm = new BarcodeLabelPrintViewModel(
                string.IsNullOrWhiteSpace(ProductName) ? "Yeni Ürün" : ProductName,
                Sku,
                Barcode,
                SalePrice,
                NewProduct.Currency,
                _dialogService,
                _toastService);

            var window = new Views.BarcodeLabelPrintWindow
            {
                DataContext = vm,
                Owner = System.Windows.Application.Current?.MainWindow
            };
            window.ShowDialog();
        }

        /// <summary>
        /// Kaydetme kontrolü
        /// </summary>
        private bool CanSave()
        {
            return !IsSaving &&
                   !string.IsNullOrWhiteSpace(NewProduct.ProductName) &&
                   NewProduct.SalePrice >= 0;
        }

        /// <summary>
        /// Ürünü kaydet veya güncelle
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task Save()
        {
            if (IsSaving) return;

            ValidationMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(NewProduct.ProductName))
            {
                ValidationMessage = "Ürün adı zorunludur.";
                _toastService?.ShowWarning("Eksik Bilgi", ValidationMessage);
                return;
            }

            if (NewProduct.SalePrice < 0 || NewProduct.PurchasePrice < 0)
            {
                ValidationMessage = "Alış ve satış fiyatı negatif olamaz.";
                _toastService?.ShowWarning("Geçersiz Fiyat", ValidationMessage);
                return;
            }

            IsSaving = true;
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();

                // 1. Normalizasyon: SKU ve Barcode alanları
                NewProduct.ProductName = NewProduct.ProductName.Trim();
                var normalizedSku = ProductCodeHelper.Normalize(NewProduct.SKU);
                if (string.IsNullOrWhiteSpace(normalizedSku))
                {
                    normalizedSku = ProductCodeHelper.GenerateSku(SelectedCategory);
                }
                var normalizedBarcode = ProductCodeHelper.Normalize(NewProduct.Barcode);

                NewProduct.SKU = normalizedSku;
                NewProduct.Barcode = normalizedBarcode;
                if (!_isEditMode)
                {
                    NewProduct.CreatedDate = DateTime.UtcNow;
                }

                // 2. Mükerrer SKU Kontrolü
                var existingWithSku = await context.Products.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id != NewProduct.Id && !p.IsDeleted && p.SKU.ToUpper() == normalizedSku);
                if (existingWithSku != null)
                {
                    ValidationMessage = $"Bu Stok Kodu (SKU) '{existingWithSku.ProductName}' ürününde zaten kayıtlı.";
                    _toastService?.ShowWarning("Mükerrer Stok Kodu", $"Bu Stok Kodu (SKU) başka bir üründe ('{existingWithSku.ProductName}') zaten kayıtlı. Lütfen farklı bir stok kodu belirleyin veya otomatik üretin.");
                    return;
                }

                // 3. Mükerrer Barkod Kontrolü
                if (!string.IsNullOrEmpty(normalizedBarcode))
                {
                    var existingWithBarcode = await context.Products.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id != NewProduct.Id && !p.IsDeleted && p.Barcode.ToUpper() == normalizedBarcode);
                    if (existingWithBarcode != null)
                    {
                        ValidationMessage = $"Bu Barkod '{existingWithBarcode.ProductName}' ürününde zaten kayıtlı.";
                        _toastService?.ShowWarning("Mükerrer Barkod", $"Bu Barkod başka bir üründe ('{existingWithBarcode.ProductName}') zaten kayıtlı. Lütfen farklı bir barkod giriniz.");
                        return;
                    }
                }

                // 4. Teknik özellikleri ata
                NewProduct.Specifications = CurrentSpecs;

                // 5. Resim işleme (varsa)
                if (!string.IsNullOrEmpty(_pendingImagePath))
                {
                    if (_isEditMode)
                    {
                        _imageService.DeleteProductImage(NewProduct.ImagePath);
                    }
                    NewProduct.ImagePath = await _imageService.SaveProductImageAsync(_pendingImagePath);
                    _pendingImagePath = null;
                }

                // 6. Tek ACID Transaction içinde Ürün ve Stok Hareketi Yönetimi (NpgsqlRetryingExecutionStrategy uyumlu)
                var txOutcome = await context.ExecuteInTransactionAsync(async transaction =>
                {
                    // Varsayılan depoyu bul veya oluştur
                    var defaultWarehouse = await context.Warehouses.FirstOrDefaultAsync(w => w.IsActive);
                    if (defaultWarehouse == null)
                    {
                        defaultWarehouse = new Warehouse
                        {
                            Name = "Ana Depo",
                            Type = WarehouseType.MainWarehouse,
                            IsActive = true
                        };
                        context.Warehouses.Add(defaultWarehouse);
                        await context.SaveChangesAsync();
                    }

                    if (_isEditMode)
                    {
                        // UPDATE: Detached UI entity yerine tracked kaydı DB'den yeniden yükle (PostgreSQL xmin concurrency uyumu)
                        var trackedProduct = await context.Products
                            .FirstOrDefaultAsync(p => p.Id == NewProduct.Id);
                        if (trackedProduct == null)
                        {
                            if (transaction is not null)
                            {
                                await transaction.RollbackAsync();
                            }
                            return (Success: false, Warning: "Güncellenmek istenen ürün kaydı bulunamadı veya başka bir işlem tarafından silinmiş. Lütfen listeyi yenileyin.", StockDelta: 0, WarehouseId: defaultWarehouse.Id, ProductId: NewProduct.Id);
                        }

                        // UPDATE: Stok mutasyonunu hareket olmadan ezme!
                        int stockDelta = 0;
                        if (StockAdjustment != 0)
                        {
                            stockDelta = StockAdjustment;
                        }
                        else if (NewProduct.TotalStockQuantity != _originalStock)
                        {
                            stockDelta = NewProduct.TotalStockQuantity - _originalStock;
                        }

                        if (stockDelta != 0)
                        {
                            var inventory = await context.Inventories
                                .FirstOrDefaultAsync(i => i.ProductId == trackedProduct.Id && i.WarehouseId == defaultWarehouse.Id);

                            if (inventory == null)
                            {
                                inventory = new Inventory
                                {
                                    ProductId = trackedProduct.Id,
                                    WarehouseId = defaultWarehouse.Id,
                                    Quantity = 0,
                                    AverageCost = NewProduct.PurchasePrice
                                };
                                context.Inventories.Add(inventory);
                            }

                            if (stockDelta < 0 && inventory.Quantity + stockDelta < 0)
                            {
                                if (transaction is not null)
                                {
                                    await transaction.RollbackAsync();
                                }
                                return (Success: false, Warning: $"Yetersiz stok! Ana depodaki mevcut stok: {inventory.Quantity}, düşülmek istenen: {Math.Abs(stockDelta)}.", StockDelta: 0, WarehouseId: defaultWarehouse.Id, ProductId: trackedProduct.Id);
                            }

                            inventory.Quantity += stockDelta;

                            var st = new StockTransaction
                            {
                                ProductId = trackedProduct.Id,
                                SourceWarehouseId = stockDelta < 0 ? defaultWarehouse.Id : null,
                                TargetWarehouseId = stockDelta > 0 ? defaultWarehouse.Id : null,
                                Quantity = Math.Abs(stockDelta),
                                TransactionType = stockDelta > 0 ? StockTransactionType.AdjustmentPlus : StockTransactionType.AdjustmentMinus,
                                UnitCost = NewProduct.PurchasePrice,
                                Date = DateTime.UtcNow,
                                Description = $"Stok Düzeltme ({(stockDelta > 0 ? "+" : "")}{stockDelta})",
                                ReferenceId = $"ADJ-{DateTime.UtcNow:yyyyMMddHHmmss}"
                            };
                            context.StockTransactions.Add(st);

                            NewProduct.TotalStockQuantity = _originalStock + stockDelta;
                            trackedProduct.TotalStockQuantity = NewProduct.TotalStockQuantity;
                        }

                        // Yalnızca skaler alanlar ve FK ID'ler kopyalanır (detached navigation eklenmez)
                        trackedProduct.ProductName = NewProduct.ProductName;
                        trackedProduct.SKU = NewProduct.SKU;
                        trackedProduct.Barcode = NewProduct.Barcode;
                        trackedProduct.ModelName = NewProduct.ModelName;
                        trackedProduct.Description = NewProduct.Description;
                        trackedProduct.CategoryId = NewProduct.CategoryId;
                        trackedProduct.BrandId = NewProduct.BrandId;
                        trackedProduct.ProductCategoryType = NewProduct.ProductCategoryType;
                        trackedProduct.Unit = NewProduct.Unit;
                        trackedProduct.Currency = NewProduct.Currency;
                        trackedProduct.PurchasePrice = NewProduct.PurchasePrice;
                        trackedProduct.SalePrice = NewProduct.SalePrice;
                        trackedProduct.AverageCost = NewProduct.AverageCost;
                        trackedProduct.VatRate = NewProduct.VatRate;
                        trackedProduct.MinStockLevel = NewProduct.MinStockLevel;
                        trackedProduct.SupplierName = NewProduct.SupplierName;
                        trackedProduct.ImagePath = NewProduct.ImagePath;
                        trackedProduct.Specifications = NewProduct.Specifications;

                        await context.SaveChangesAsync();
                        if (transaction is not null)
                        {
                            await transaction.CommitAsync();
                        }

                        return (Success: true, Warning: (string?)null, StockDelta: stockDelta, WarehouseId: defaultWarehouse.Id, ProductId: trackedProduct.Id);
                    }
                    else
                    {
                        // ADD: NpgsqlRetryingExecutionStrategy retry güvenliği için lambda içinde taze Product örneği oluştur
                        int openingStock = InitialStock > 0 ? InitialStock : 0;
                        var createdProduct = new Product
                        {
                            ProductName = NewProduct.ProductName,
                            SKU = NewProduct.SKU,
                            Barcode = NewProduct.Barcode,
                            ModelName = NewProduct.ModelName,
                            Description = NewProduct.Description,
                            CategoryId = NewProduct.CategoryId,
                            BrandId = NewProduct.BrandId,
                            ProductCategoryType = NewProduct.ProductCategoryType,
                            Unit = NewProduct.Unit,
                            Currency = NewProduct.Currency,
                            PurchasePrice = NewProduct.PurchasePrice,
                            SalePrice = NewProduct.SalePrice,
                            AverageCost = NewProduct.AverageCost > 0 ? NewProduct.AverageCost : NewProduct.PurchasePrice,
                            VatRate = NewProduct.VatRate,
                            MinStockLevel = NewProduct.MinStockLevel,
                            TotalStockQuantity = openingStock,
                            SupplierName = NewProduct.SupplierName,
                            ImagePath = NewProduct.ImagePath,
                            Specifications = NewProduct.Specifications
                        };

                        context.Products.Add(createdProduct);
                        await context.SaveChangesAsync();

                        // Açılış stoğu varsa aynı transaction içinde Inventory ve StockTransaction oluştur
                        if (openingStock > 0)
                        {
                            var inventory = new Inventory
                            {
                                ProductId = createdProduct.Id,
                                WarehouseId = defaultWarehouse.Id,
                                Quantity = openingStock,
                                AverageCost = createdProduct.PurchasePrice
                            };
                            context.Inventories.Add(inventory);

                            var st = new StockTransaction
                            {
                                ProductId = createdProduct.Id,
                                TargetWarehouseId = defaultWarehouse.Id,
                                Quantity = openingStock,
                                TransactionType = StockTransactionType.OpeningStock,
                                UnitCost = createdProduct.PurchasePrice,
                                Date = DateTime.UtcNow,
                                Description = "Açılış / Devir Stoğu",
                                ReferenceId = $"OPEN-{DateTime.UtcNow:yyyyMMddHHmmss}"
                            };
                            context.StockTransactions.Add(st);
                            await context.SaveChangesAsync();
                        }

                        if (transaction is not null)
                        {
                            await transaction.CommitAsync();
                        }

                        return (Success: true, Warning: (string?)null, StockDelta: openingStock, WarehouseId: defaultWarehouse.Id, ProductId: createdProduct.Id);
                    }
                });

                if (!txOutcome.Success)
                {
                    if (!string.IsNullOrWhiteSpace(txOutcome.Warning))
                    {
                        if (_dialogService != null)
                            await _dialogService.ShowWarningAsync(txOutcome.Warning, "Yetersiz Stok");
                        else
                            _toastService?.ShowWarning("Yetersiz Stok", txOutcome.Warning);
                    }
                    return;
                }

                if (!_isEditMode)
                {
                    NewProduct.Id = txOutcome.ProductId;
                    NewProduct.TotalStockQuantity = txOutcome.StockDelta;
                }

                // Stok listesini her başarılı kayıt/güncellemede EventAggregator üzerinden yenile
                EventAggregator.Instance.Publish(new StockUpdatedEvent(
                    NewProduct.Id,
                    txOutcome.WarehouseId,
                    _isEditMode ? _originalStock : 0,
                    NewProduct.TotalStockQuantity,
                    _isEditMode ? "Ürün Güncellendi" : (InitialStock > 0 ? "Açılış / Devir Stoğu" : "Yeni Ürün Oluşturuldu")));

                if (_isEditMode)
                {
                    if (_toastService != null)
                        _toastService.ShowSuccess("Başarılı", "Ürün başarıyla güncellendi!");
                    else if (_dialogService != null)
                        await _dialogService.ShowMessageAsync("Ürün başarıyla güncellendi!", "Başarılı");
                }
                else
                {
                    if (_toastService != null)
                        _toastService.ShowSuccess("Başarılı", "Ürün başarıyla kaydedildi!");
                    else if (_dialogService != null)
                        await _dialogService.ShowMessageAsync("Ürün başarıyla kaydedildi!", "Başarılı");
                }

                RequestClose?.Invoke(true);
            }
            catch (DbUpdateConcurrencyException concurrencyEx)
            {
                string affectedEntities = string.Join(", ", concurrencyEx.Entries.Select(e => e.Metadata.ClrType.Name));
                Serilog.Log.Error(concurrencyEx, "Stok kartı eşzamanlılık çakışması (ProductId: {ProductId}, Entities: {Entities})", NewProduct.Id, affectedEntities);
                ValidationMessage = "Kayıt başka bir işlem tarafından güncellenmiş veya silinmiş. Lütfen listeyi yenileyip tekrar deneyin.";
                _toastService?.ShowWarning("Eşzamanlılık Çakışması", ValidationMessage);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Stok kartı kaydedilirken hata oluştu.");
                ValidationMessage = $"Kaydetme hatası: {ex.Message}";
                _toastService?.ShowError("Hata", ValidationMessage);
            }
            finally
            {
                IsSaving = false;
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
                _toastService?.ShowWarning("Uyarı", $"Açılış stoğu oluşturulurken hata: {ex.Message}");
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
                _toastService?.ShowWarning("Uyarı", $"Stok düzeltme hatası: {ex.Message}");
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
                catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "AddProductViewModel.cs"); }
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
            catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "AddProductViewModel.cs"); }
        }

        #endregion
    }
}
