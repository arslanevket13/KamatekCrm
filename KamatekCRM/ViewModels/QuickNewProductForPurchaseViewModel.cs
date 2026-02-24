using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Satın Alma ekranından hızlı yeni ürün oluşturma ViewModel.
    /// Kayıt sonrası SavedProduct set edilip pencere kapanır.
    /// </summary>
    public class QuickNewProductForPurchaseViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;

        #region Fields & Properties

        private string _productName = string.Empty;
        public string ProductName
        {
            get => _productName;
            set
            {
                SetProperty(ref _productName, value);
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _sku = string.Empty;
        public string SKU
        {
            get => _sku;
            set => SetProperty(ref _sku, value);
        }

        private string _barcode = string.Empty;
        public string Barcode
        {
            get => _barcode;
            set => SetProperty(ref _barcode, value);
        }

        private string _unit = "Adet";
        public string Unit
        {
            get => _unit;
            set => SetProperty(ref _unit, value);
        }

        private int _vatRate = 20;
        public int VatRate
        {
            get => _vatRate;
            set => SetProperty(ref _vatRate, value);
        }

        private decimal _purchasePrice;
        public decimal PurchasePrice
        {
            get => _purchasePrice;
            set
            {
                SetProperty(ref _purchasePrice, value);
                // Auto-suggest sale price (20% margin) if not edited yet
                if (SalePrice == 0 && value > 0)
                    SalePrice = Math.Round(value * 1.20m, 2);
            }
        }

        private decimal _salePrice;
        public decimal SalePrice
        {
            get => _salePrice;
            set => SetProperty(ref _salePrice, value);
        }

        private int _initialQuantity;
        public int InitialQuantity
        {
            get => _initialQuantity;
            set => SetProperty(ref _initialQuantity, value);
        }

        private Category? _selectedCategory;
        public Category? SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                SetProperty(ref _errorMessage, value);
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public bool IsBusy { get; private set; }

        public ObservableCollection<Category> Categories { get; } = new();
        public ObservableCollection<string> Units { get; } = new() { "Adet", "Kutu", "Metre", "Kg", "Litre", "Rulo", "Paket" };
        public int[] VatRates { get; } = { 0, 1, 10, 20 };

        /// <summary>Başarılı kayıt sonrası dönen ürün.</summary>
        public Product? SavedProduct { get; private set; }

        #endregion

        #region Commands

        public ICommand SaveProductCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        public event Action<bool>? RequestClose;

        public QuickNewProductForPurchaseViewModel()
        {
            _context = new AppDbContext();
            LoadCategories();

            SaveProductCommand = new RelayCommand(
                _ => ExecuteSaveProduct(),
                _ => !string.IsNullOrWhiteSpace(ProductName) && !IsBusy);

            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        private void LoadCategories()
        {
            try
            {
                Categories.Clear();
                foreach (var c in _context.Categories.OrderBy(x => x.Name).ToList())
                    Categories.Add(c);
            }
            catch { /* silently ignore — category is optional */ }
        }

        private void ExecuteSaveProduct()
        {
            if (string.IsNullOrWhiteSpace(ProductName))
            {
                ErrorMessage = "Ürün adı zorunludur.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                // Auto-generate SKU if empty
                var sku = string.IsNullOrWhiteSpace(SKU)
                    ? $"POI-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}"
                    : SKU.Trim();

                var product = new Product
                {
                    ProductName = ProductName.Trim(),
                    SKU = sku,
                    Barcode = Barcode.Trim(),
                    Unit = Unit,
                    VatRate = VatRate,
                    PurchasePrice = PurchasePrice,
                    SalePrice = SalePrice > 0 ? SalePrice : Math.Round(PurchasePrice * 1.20m, 2),
                    AverageCost = PurchasePrice,
                    CategoryId = SelectedCategory?.Id,
                    Currency = "TRY",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "Satın Alma-Hızlı"
                };

                _context.Products.Add(product);
                _context.SaveChanges();

                // If initial quantity set, create stock entry
                if (InitialQuantity > 0)
                {
                    var warehouse = _context.Warehouses.FirstOrDefault(w => w.IsActive);
                    if (warehouse != null)
                    {
                        var inv = new Inventory
                        {
                            ProductId = product.Id,
                            WarehouseId = warehouse.Id,
                            Quantity = InitialQuantity,
                            AverageCost = PurchasePrice
                        };
                        _context.Inventories.Add(inv);
                        product.TotalStockQuantity = InitialQuantity;
                        _context.SaveChanges();
                    }
                }

                SavedProduct = product;
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Kayıt hatası: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
