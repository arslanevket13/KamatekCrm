using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;
using KamatekCrm.Services.Domain;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Professional POS (Perakende Satış) ViewModel
    /// Barkod tarama, satır bazlı indirim, KDV hesaplama, split ödeme desteği
    /// F8=Nakit, F9=Kart kısayolları
    /// </summary>
    public class DirectSalesViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private readonly IAuthService _authService;
        private readonly ISalesDomainService _salesDomainService;

        private string _searchText = string.Empty;
        private string _barcodeText = string.Empty;
        private string _customerName = "Perakende Müşteri";
        private string _statusMessage = string.Empty;
        private bool _isActionSuccessful;
        private Warehouse? _selectedWarehouse;

        // Customer selection
        private Customer? _selectedCustomer;
        private string _customerSearch = string.Empty;

        // Global discount
        private decimal _globalDiscountPercent;


        // Split payment
        private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;
        private decimal _paymentAmount;
        private string _paymentReference = string.Empty;

        public ObservableCollection<PosProductItem> AllProducts { get; set; }
        public ObservableCollection<PosCartItem> CartItems { get; set; }
        public ObservableCollection<Warehouse> Warehouses { get; set; }
        public ObservableCollection<PosPaymentEntry> Payments { get; set; }

        public ICollectionView FilteredProducts { get; private set; }

        #region Properties

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    FilteredProducts?.Refresh();
            }
        }

        public string BarcodeText
        {
            get => _barcodeText;
            set => SetProperty(ref _barcodeText, value);
        }

        public string CustomerName
        {
            get => _customerName;
            set => SetProperty(ref _customerName, value);
        }

        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    CustomerName = value?.FullName ?? "Perakende Müşteri";
                    OnPropertyChanged(nameof(CustomerDisplayName));
                }
            }
        }

        public string CustomerSearch
        {
            get => _customerSearch;
            set
            {
                if (SetProperty(ref _customerSearch, value))
                    FilterCustomers();
            }
        }

        public string CustomerDisplayName =>
            SelectedCustomer != null
                ? $"{SelectedCustomer.FullName} ({SelectedCustomer.PhoneNumber})"
                : "Perakende Müşteri";

        public ObservableCollection<Customer> RecentCustomers { get; } = new();
        public ObservableCollection<Customer> FilteredCustomers { get; } = new();

        public decimal GlobalDiscountPercent
        {
            get => _globalDiscountPercent;
            set
            {
                if (SetProperty(ref _globalDiscountPercent, value))
                    ApplyGlobalDiscount();
            }
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

        public Warehouse? SelectedWarehouse
        {
            get => _selectedWarehouse;
            set
            {
                if (SetProperty(ref _selectedWarehouse, value))
                    LoadProducts();
            }
        }

        public PaymentMethod SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set => SetProperty(ref _selectedPaymentMethod, value);
        }

        public decimal PaymentAmount
        {
            get => _paymentAmount;
            set => SetProperty(ref _paymentAmount, value);
        }

        public string PaymentReference
        {
            get => _paymentReference;
            set => SetProperty(ref _paymentReference, value);
        }

        // --- Computed Totals ---
        public decimal SubTotal => CartItems?.Sum(i => i.SubTotal) ?? 0;
        public decimal DiscountTotal => CartItems?.Sum(i => i.DiscountAmount) ?? 0;
        public decimal TaxTotal => CartItems?.Sum(i => i.TaxAmount) ?? 0;
        public decimal GrandTotal => CartItems?.Sum(i => i.LineTotal) ?? 0;
        public int CartItemCount => CartItems?.Sum(i => i.Quantity) ?? 0;
        public decimal PaidAmount => Payments?.Sum(p => p.Amount) ?? 0;
        public decimal RemainingAmount => GrandTotal - PaidAmount;
        public bool CanCompleteSale => RemainingAmount <= 0 && CartItems?.Count > 0;

        public Array PaymentMethods => Enum.GetValues(typeof(PaymentMethod));

        // Düşük stok uyarıları
        private string _stockWarningMessage = string.Empty;
        public string StockWarningMessage
        {
            get => _stockWarningMessage;
            set => SetProperty(ref _stockWarningMessage, value);
        }

        public bool HasStockWarning => !string.IsNullOrEmpty(StockWarningMessage);

        private bool _focusBarcodeRequested;
        public bool FocusBarcodeRequested
        {
            get => _focusBarcodeRequested;
            set => SetProperty(ref _focusBarcodeRequested, value);
        }

        #endregion

        #region Commands

        public ICommand AddToCartCommand { get; }
        public ICommand IncreaseQuantityCommand { get; }
        public ICommand DecreaseQuantityCommand { get; }
        public ICommand RemoveFromCartCommand { get; }
        public ICommand ProcessCashPaymentCommand { get; }
        public ICommand ProcessCardPaymentCommand { get; }
        public ICommand ClearCartCommand { get; }
        public ICommand BarcodeScanCommand { get; }
        public ICommand AddPaymentCommand { get; }
        public ICommand RemovePaymentCommand { get; }
        public ICommand CompleteSaleCommand { get; }
        public ICommand PayFullCashCommand { get; }
        public ICommand PayFullCardCommand { get; }
        public ICommand QuickAddCustomerCommand { get; }
        public ICommand SelectCustomerCommand { get; }
        public ICommand ClearCustomerCommand { get; }

        #endregion

        public DirectSalesViewModel(IAuthService authService, ISalesDomainService salesDomainService)
        {
            _authService = authService;
            _salesDomainService = salesDomainService;
            _context = new AppDbContext();
            AllProducts = new ObservableCollection<PosProductItem>();
            CartItems = new ObservableCollection<PosCartItem>();
            Warehouses = new ObservableCollection<Warehouse>(_context.Warehouses.Where(w => w.IsActive).ToList());
            Payments = new ObservableCollection<PosPaymentEntry>();

            SelectedWarehouse = Warehouses.FirstOrDefault();

            FilteredProducts = CollectionViewSource.GetDefaultView(AllProducts);
            FilteredProducts.Filter = FilterProducts;

            // Commands
            AddToCartCommand = new RelayCommand(ExecuteAddToCart, CanAddToCart);
            IncreaseQuantityCommand = new RelayCommand(ExecuteIncreaseQuantity);
            DecreaseQuantityCommand = new RelayCommand(ExecuteDecreaseQuantity);
            RemoveFromCartCommand = new RelayCommand(ExecuteRemoveFromCart);
            BarcodeScanCommand = new RelayCommand(_ => ExecuteBarcodeScan());
            AddPaymentCommand = new RelayCommand(_ => ExecuteAddPayment(), _ => PaymentAmount > 0);
            RemovePaymentCommand = new RelayCommand(ExecuteRemovePayment);
            CompleteSaleCommand = new RelayCommand(_ => ExecuteCompleteSale(), _ => CanCompleteSale);
            ClearCartCommand = new RelayCommand(_ => ExecuteClearCart(), _ => CartItems.Count > 0);

            // Quick full-payment shortcuts (F8/F9)
            PayFullCashCommand = new RelayCommand(_ => ExecutePayFull(PaymentMethod.Cash), _ => CartItems.Count > 0 && GrandTotal > 0);
            PayFullCardCommand = new RelayCommand(_ => ExecutePayFull(PaymentMethod.CreditCard), _ => CartItems.Count > 0 && GrandTotal > 0);

            // Legacy commands for backward compat
            ProcessCashPaymentCommand = PayFullCashCommand;
            ProcessCardPaymentCommand = PayFullCardCommand;

            CartItems.CollectionChanged += (s, e) => UpdateAllTotals();

            QuickAddCustomerCommand = new RelayCommand(_ => ExecuteQuickAddCustomer());
            SelectCustomerCommand = new RelayCommand(p => { if (p is Customer c) SelectedCustomer = c; });
            ClearCustomerCommand = new RelayCommand(_ => { SelectedCustomer = null; CustomerSearch = string.Empty; });

            LoadRecentCustomers();
        }

        #region Product Loading

        private void LoadProducts()
        {
            AllProducts.Clear();
            if (SelectedWarehouse == null) return;

            try
            {
                var products = _context.Products
                    .Include(p => p.Inventories)
                    .ToList();

                foreach (var product in products)
                {
                    var inventory = product.Inventories
                        .FirstOrDefault(i => i.WarehouseId == SelectedWarehouse.Id);

                    AllProducts.Add(new PosProductItem
                    {
                        ProductId = product.Id,
                        ProductName = product.ProductName,
                        ModelName = product.ModelName ?? string.Empty,
                        SKU = product.SKU ?? string.Empty,
                        Barcode = product.Barcode ?? string.Empty,
                        SalePrice = product.SalePrice,
                        VatRate = product.VatRate,
                        StockQuantity = inventory?.Quantity ?? 0,
                        Unit = product.Unit,
                        ImagePath = product.ImagePath
                    });
                }

                StatusMessage = $"{AllProducts.Count} ürün yüklendi.";
                IsActionSuccessful = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Yükleme hatası: {ex.Message}";
                IsActionSuccessful = false;
            }
        }

        private bool FilterProducts(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            if (obj is PosProductItem p)
            {
                return p.ProductName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || p.ModelName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || p.SKU.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || p.Barcode.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        #endregion

        #region Customer

        private void LoadRecentCustomers()
        {
            try
            {
                RecentCustomers.Clear();
                var list = _context.Customers
                    .OrderByDescending(c => c.CreatedDate)
                    .Take(25)
                    .ToList();
                foreach (var c in list) RecentCustomers.Add(c);
                FilterCustomers();
            }
            catch { /* не критично */ }
        }

        private void FilterCustomers()
        {
            FilteredCustomers.Clear();
            var src = RecentCustomers.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(CustomerSearch))
                src = src.Where(c =>
                    c.FullName.Contains(CustomerSearch, StringComparison.OrdinalIgnoreCase) ||
                    (c.PhoneNumber?.Contains(CustomerSearch, StringComparison.OrdinalIgnoreCase) ?? false));
            foreach (var c in src.Take(10)) FilteredCustomers.Add(c);
        }

        private void ExecuteQuickAddCustomer()
        {
            var win = new Views.QuickCustomerAddWindow { Owner = System.Windows.Application.Current.MainWindow };
            if (win.ShowDialog() == true)
            {
                var vm = (QuickCustomerAddViewModel)win.DataContext;
                if (vm.SavedCustomer != null)
                {
                    RecentCustomers.Insert(0, vm.SavedCustomer);
                    FilterCustomers();
                    SelectedCustomer = vm.SavedCustomer;
                    StatusMessage = $"✅ Müşteri eklendi: {vm.SavedCustomer.FullName}";
                    IsActionSuccessful = true;
                }
            }
        }

        #endregion

        #region Global Discount

        private void ApplyGlobalDiscount()
        {
            foreach (var item in CartItems)
                item.DiscountPercent = _globalDiscountPercent;
            UpdateAllTotals();
        }

        #endregion

        #region Cart Operations

        private bool CanAddToCart(object? parameter) => parameter is PosProductItem;

        private void ExecuteAddToCart(object? parameter)
        {
            if (parameter is not PosProductItem product) return;
            AddProductToCart(product.ProductId, product.ProductName, product.SalePrice, product.VatRate, product.StockQuantity);
        }

        private void ExecuteBarcodeScan()
        {
            if (string.IsNullOrWhiteSpace(BarcodeText)) return;

            var product = AllProducts.FirstOrDefault(p =>
                p.Barcode.Equals(BarcodeText, StringComparison.OrdinalIgnoreCase) ||
                p.SKU.Equals(BarcodeText, StringComparison.OrdinalIgnoreCase));

            if (product != null)
            {
                AddProductToCart(product.ProductId, product.ProductName, product.SalePrice, product.VatRate, product.StockQuantity);
                StatusMessage = $"✅ '{product.ProductName}' barkod ile eklendi.";
                IsActionSuccessful = true;
            }
            else
            {
                StatusMessage = $"❌ Barkod bulunamadı: {BarcodeText}";
                IsActionSuccessful = false;
            }

            BarcodeText = string.Empty;
        }

        private void AddProductToCart(int productId, string productName, decimal salePrice, int vatRate, int maxQty)
        {
            // Düşük stok kontrolü (5 ve altı)
            if (maxQty <= 5 && maxQty > 0)
            {
                StockWarningMessage = $"⚠️ '{productName}' ürünü düşük stokta! (Kalan: {maxQty})";
                OnPropertyChanged(nameof(HasStockWarning));
            }
            else if (maxQty <= 0)
            {
                StockWarningMessage = $"❌ '{productName}' ürünü stokta yok!";
                OnPropertyChanged(nameof(HasStockWarning));
                return;
            }
            else
            {
                StockWarningMessage = string.Empty;
                OnPropertyChanged(nameof(HasStockWarning));
            }

            var existing = CartItems.FirstOrDefault(i => i.ProductId == productId);
            if (existing != null)
            {
                if (existing.Quantity >= maxQty)
                {
                    StatusMessage = $"❌ '{productName}' için yeterli stok yok!";
                    return;
                }
                existing.Quantity++;
            }
            else
            {
                var item = new PosCartItem
                {
                    ProductId = productId,
                    ProductName = productName,
                    UnitPrice = salePrice,
                    Quantity = 1,
                    MaxQuantity = maxQty,
                    TaxRate = vatRate,
                    DiscountPercent = 0,
                    DiscountType = DiscountType.Percentage
                };
                item.PropertyChanged += (s, e) => UpdateAllTotals();
                CartItems.Add(item);
            }

            UpdateAllTotals();
            StatusMessage = $"'{productName}' sepete eklendi.";
            IsActionSuccessful = true;
            FocusBarcodeRequested = true;
        }

        private void ExecuteIncreaseQuantity(object? parameter)
        {
            if (parameter is PosCartItem item)
            {
                item.Quantity++;
                UpdateAllTotals();
            }
        }

        private void ExecuteDecreaseQuantity(object? parameter)
        {
            if (parameter is PosCartItem item)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                    UpdateAllTotals();
                }
                else
                {
                    CartItems.Remove(item);
                    UpdateAllTotals();
                }
            }
        }

        private void ExecuteRemoveFromCart(object? parameter)
        {
            if (parameter is PosCartItem item)
            {
                CartItems.Remove(item);
                UpdateAllTotals();
                StatusMessage = $"'{item.ProductName}' sepetten çıkarıldı.";
                IsActionSuccessful = true;
            }
        }

        private void ExecuteClearCart()
        {
            if (CartItems.Count == 0) return;
            var result = MessageBox.Show("Sepeti temizlemek istiyor musunuz?", "Sepeti Temizle",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                CartItems.Clear();
                Payments.Clear();
                UpdateAllTotals();
                StatusMessage = "Sepet temizlendi.";
                IsActionSuccessful = true;
            }
        }

        #endregion

        #region Payment Operations

        /// <summary>
        /// Add a partial payment entry (for split payments)
        /// </summary>
        private void ExecuteAddPayment()
        {
            if (PaymentAmount <= 0) return;

            // Cap payment at remaining amount
            var amount = Math.Min(PaymentAmount, RemainingAmount);
            if (amount <= 0)
            {
                StatusMessage = "Kalan tutar zaten ödendi.";
                return;
            }

            Payments.Add(new PosPaymentEntry
            {
                PaymentMethod = SelectedPaymentMethod,
                Amount = amount,
                Reference = PaymentReference
            });

            PaymentAmount = 0;
            PaymentReference = string.Empty;
            UpdateAllTotals();

            StatusMessage = $"Ödeme eklendi. Kalan: {RemainingAmount:C}";
            IsActionSuccessful = true;

            // Auto-complete sale if fully paid
            if (CanCompleteSale)
            {
                ExecuteCompleteSale();
            }
        }

        private void ExecuteRemovePayment(object? parameter)
        {
            if (parameter is PosPaymentEntry entry)
            {
                Payments.Remove(entry);
                UpdateAllTotals();
            }
        }

        /// <summary>
        /// F8/F9 shortcut: pay full amount with a single method
        /// </summary>
        private void ExecutePayFull(PaymentMethod method)
        {
            if (CartItems.Count == 0 || GrandTotal <= 0) return;

            // Clear existing payments and add single full payment
            Payments.Clear();
            Payments.Add(new PosPaymentEntry
            {
                PaymentMethod = method,
                Amount = GrandTotal,
                Reference = string.Empty
            });

            UpdateAllTotals();
            ExecuteCompleteSale();
        }

        /// <summary>
        /// Final sale completion — delegates to SalesDomainService
        /// </summary>
        private void ExecuteCompleteSale()
        {
            if (!CanCompleteSale || SelectedWarehouse == null) return;

            var paymentDesc = string.Join(", ", Payments.Select(p =>
                $"{GetPaymentMethodName(p.PaymentMethod)}: {p.Amount:C}"));

            var result = MessageBox.Show(
                $"Toplam: {GrandTotal:C}\n" +
                $"Ödeme: {paymentDesc}\n\n" +
                "Satışı tamamlamak istiyor musunuz?",
                "Satış Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            var request = new SaleRequest
            {
                WarehouseId = SelectedWarehouse.Id,
                CustomerId = SelectedCustomer?.Id,
                CustomerName = CustomerName,
                PaymentMethod = Payments.First().PaymentMethod, // Primary method
                CreatedBy = _authService.CurrentUser?.AdSoyad ?? "Sistem",
                Items = CartItems.Select(c => new SaleItemRequest
                {
                    ProductId = c.ProductId,
                    ProductName = c.ProductName,
                    Quantity = c.Quantity,
                    UnitPrice = c.UnitPrice,
                    DiscountPercent = c.DiscountPercent,
                    DiscountAmount = c.DiscountAmount,
                    TaxRate = c.TaxRate,
                    LineTotal = c.LineTotal
                }).ToList()
            };

            var saleResult = _salesDomainService.ProcessSale(request);

            if (saleResult.Success)
            {
                StatusMessage = $"✅ Satış tamamlandı! Sipariş No: {saleResult.OrderNumber}";
                IsActionSuccessful = true;

                CartItems.Clear();
                Payments.Clear();
                UpdateAllTotals();
                LoadProducts();

                MessageBox.Show(
                    $"Satış Başarılı!\n\n" +
                    $"Sipariş No: {saleResult.OrderNumber}\n" +
                    $"Toplam: {saleResult.TotalAmount:C}\n" +
                    $"Ödeme: {paymentDesc}",
                    "Satış Tamamlandı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = $"Satış hatası: {saleResult.ErrorMessage}";
                IsActionSuccessful = false;
                MessageBox.Show($"Satış hatası:\n{saleResult.ErrorMessage}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetPaymentMethodName(PaymentMethod method) => method switch
        {
            PaymentMethod.Cash => "Nakit",
            PaymentMethod.CreditCard => "Kredi Kartı",
            PaymentMethod.BankTransfer => "Banka Transferi",
            PaymentMethod.MobilePayment => "Mobil Ödeme",
            _ => method.ToString()
        };

        #endregion

        #region Totals

        private void UpdateAllTotals()
        {
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(DiscountTotal));
            OnPropertyChanged(nameof(TaxTotal));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(CartItemCount));
            OnPropertyChanged(nameof(PaidAmount));
            OnPropertyChanged(nameof(RemainingAmount));
            OnPropertyChanged(nameof(CanCompleteSale));
        }

        // Legacy compat
        public decimal CartTotal => GrandTotal;
        public decimal CartGrandTotal => GrandTotal;

        #endregion
    }

    // =====================================================================
    // POS Display Models
    // =====================================================================

    /// <summary>
    /// Product display item for POS grid
    /// </summary>
    public class PosProductItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public decimal SalePrice { get; set; }
        public int VatRate { get; set; }
        public int StockQuantity { get; set; }
        public string Unit { get; set; } = "Adet";
        public string? ImagePath { get; set; }
    }

    /// <summary>
    /// Cart item with discount, tax, and line-total calculation
    /// </summary>
    public class PosCartItem : INotifyPropertyChanged
    {
        private int _quantity;
        private decimal _unitPrice;
        private decimal _discountPercent;
        private decimal _discountFlatAmount;
        private DiscountType _discountType = DiscountType.Percentage;
        private int _taxRate;

        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int MaxQuantity { get; set; }

        public int Quantity
        {
            get => _quantity;
            set { if (_quantity != value) { _quantity = value; Notify(); NotifyTotals(); } }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set { if (_unitPrice != value) { _unitPrice = value; Notify(); NotifyTotals(); } }
        }

        public DiscountType DiscountType
        {
            get => _discountType;
            set { if (_discountType != value) { _discountType = value; Notify(); NotifyTotals(); } }
        }

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set { if (_discountPercent != value) { _discountPercent = value; Notify(); NotifyTotals(); } }
        }

        public decimal DiscountFlatAmount
        {
            get => _discountFlatAmount;
            set { if (_discountFlatAmount != value) { _discountFlatAmount = value; Notify(); NotifyTotals(); } }
        }

        public int TaxRate
        {
            get => _taxRate;
            set { if (_taxRate != value) { _taxRate = value; Notify(); NotifyTotals(); } }
        }

        // --- Computed ---

        /// <summary>Quantity × UnitPrice</summary>
        public decimal SubTotal => Quantity * UnitPrice;

        /// <summary>Actual discount amount applied</summary>
        public decimal DiscountAmount => DiscountType == DiscountType.Percentage
            ? SubTotal * DiscountPercent / 100m
            : DiscountFlatAmount;

        /// <summary>SubTotal after discount, before tax</summary>
        public decimal AfterDiscount => SubTotal - DiscountAmount;

        /// <summary>Tax calculated on after-discount amount</summary>
        public decimal TaxAmount => AfterDiscount * TaxRate / 100m;

        /// <summary>Final line total: AfterDiscount + Tax</summary>
        public decimal LineTotal => AfterDiscount + TaxAmount;

        // Legacy compat
        public decimal TotalPrice => LineTotal;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private void NotifyTotals()
        {
            Notify(nameof(SubTotal));
            Notify(nameof(DiscountAmount));
            Notify(nameof(AfterDiscount));
            Notify(nameof(TaxAmount));
            Notify(nameof(LineTotal));
            Notify(nameof(TotalPrice));
        }
    }

    /// <summary>
    /// A split-payment entry in the POS
    /// </summary>
    public class PosPaymentEntry
    {
        public PaymentMethod PaymentMethod { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;

        public string DisplayName => PaymentMethod switch
        {
            PaymentMethod.Cash => "Nakit",
            PaymentMethod.CreditCard => "Kredi Kartı",
            PaymentMethod.BankTransfer => "Banka Transferi",
            PaymentMethod.MobilePayment => "Mobil Ödeme",
            _ => PaymentMethod.ToString()
        };
    }

    // Keep backward compat aliases
    public class ProductDisplayItem : PosProductItem { }
    public class CartItem : PosCartItem { }
}
