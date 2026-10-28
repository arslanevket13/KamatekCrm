using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Helpers;
using KamatekCrm.Shared.Models;
using KamatekCrm.Events;
using KamatekCrm.Services;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Satın Alma ekranından hızlı yeni ürün oluşturma ViewModel.
    /// Kayıt sonrası SavedProduct set edilip pencere kapanır.
    /// </summary>
    public partial class QuickNewProductForPurchaseViewModel : ViewModelBase
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

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

        #endregion

        public event Action<bool>? RequestClose;

        public QuickNewProductForPurchaseViewModel(IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            LoadCategories();
        }

        private void LoadCategories()
        {
            try
            {
                Categories.Clear();
                using var context = _dbContextFactory.CreateDbContext();
                foreach (var c in context.Categories.OrderBy(x => x.Name).ToList())
                    Categories.Add(c);
            }
            catch { /* silently ignore — category is optional */ }
        }

        private bool CanSaveProduct() => !string.IsNullOrWhiteSpace(ProductName) && !IsBusy;

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke(false);

        [RelayCommand(CanExecute = nameof(CanSaveProduct))]
        private void SaveProduct()
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
                using var context = _dbContextFactory.CreateDbContext();

                // 1. Barkod ve SKU Normalizasyonu
                var normalizedSku = string.IsNullOrWhiteSpace(SKU)
                    ? ProductCodeHelper.GenerateSku(SelectedCategory != null ? Enum.TryParse<ProductCategoryType>(SelectedCategory.Name, out var cat) ? cat : ProductCategoryType.Other : ProductCategoryType.Other)
                    : ProductCodeHelper.Normalize(SKU);

                var normalizedBarcode = ProductCodeHelper.Normalize(Barcode);

                // 2. Mükerrer SKU Kontrolü
                var existingSku = context.Products.AsNoTracking()
                    .FirstOrDefault(p => !p.IsDeleted && p.SKU.ToUpper() == normalizedSku);
                if (existingSku != null)
                {
                    ErrorMessage = $"Bu Stok Kodu (SKU) '{existingSku.ProductName}' ürününde zaten kayıtlı.";
                    return;
                }

                // 3. Mükerrer Barkod Kontrolü
                if (!string.IsNullOrEmpty(normalizedBarcode))
                {
                    var existingBarcode = context.Products.AsNoTracking()
                        .FirstOrDefault(p => !p.IsDeleted && p.Barcode.ToUpper() == normalizedBarcode);
                    if (existingBarcode != null)
                    {
                        ErrorMessage = $"Bu Barkod '{existingBarcode.ProductName}' ürününde zaten kayıtlı.";
                        return;
                    }
                }

                // 4. Tek ACID Transaction içinde Ürün ve Açılış Stoğu Hareketi
                using var transaction = context.Database.BeginTransaction();
                try
                {
                    var product = new Product
                    {
                        ProductName = ProductName.Trim(),
                        SKU = normalizedSku,
                        Barcode = normalizedBarcode,
                        Unit = Unit,
                        VatRate = VatRate,
                        PurchasePrice = PurchasePrice,
                        SalePrice = SalePrice > 0 ? SalePrice : Math.Round(PurchasePrice * 1.20m, 2),
                        AverageCost = PurchasePrice,
                        CategoryId = SelectedCategory?.Id,
                        Currency = "TRY",
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = "Satın Alma-Hızlı",
                        TotalStockQuantity = InitialQuantity > 0 ? InitialQuantity : 0
                    };

                    context.Products.Add(product);
                    context.SaveChanges();

                    int warehouseId = 0;
                    if (InitialQuantity > 0)
                    {
                        var warehouse = context.Warehouses.FirstOrDefault(w => w.IsActive);
                        if (warehouse == null)
                        {
                            warehouse = new Warehouse
                            {
                                Name = "Ana Depo",
                                Type = WarehouseType.MainWarehouse,
                                IsActive = true
                            };
                            context.Warehouses.Add(warehouse);
                            context.SaveChanges();
                        }

                        warehouseId = warehouse.Id;

                        var inv = new Inventory
                        {
                            ProductId = product.Id,
                            WarehouseId = warehouse.Id,
                            Quantity = InitialQuantity,
                            AverageCost = PurchasePrice
                        };
                        context.Inventories.Add(inv);

                        var st = new StockTransaction
                        {
                            ProductId = product.Id,
                            TargetWarehouseId = warehouse.Id,
                            Quantity = InitialQuantity,
                            UnitCost = PurchasePrice,
                            TransactionType = StockTransactionType.OpeningStock,
                            Date = DateTime.UtcNow,
                            Description = "Açılış / Devir Stoğu",
                            ReferenceId = $"OPEN-{DateTime.UtcNow:yyyyMMddHHmmss}",
                            UserId = "Satın Alma-Hızlı"
                        };
                        context.StockTransactions.Add(st);
                        context.SaveChanges();
                    }

                    transaction.Commit();

                    if (InitialQuantity > 0 && warehouseId > 0)
                    {
                        EventAggregator.Instance.Publish(new StockUpdatedEvent(
                            product.Id, warehouseId, 0, InitialQuantity, "Açılış / Devir Stoğu (Hızlı Ürün)"));
                    }

                    SavedProduct = product;
                    RequestClose?.Invoke(true);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
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
