using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Professional Enterprise POS (Perakende Satış) ViewModel
    /// Barkod tarama, Termal Fiş Yazdırma, Para Üstü Hesabı, Sepet Park Etme, Cari Satış ve Parçalı Ödeme
    /// </summary>
    public partial class DirectSalesViewModel : ViewModelBase
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private readonly IDirectSalesService _directSalesService;
        private readonly IThermalReceiptPrintService _thermalPrintService;
        private readonly IPersonalDataProtectionService _personalDataProtection;
        private readonly SalesReturnViewModel _salesReturnViewModel;
        private Guid _saleAttemptId = Guid.NewGuid();

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

        // Split payment & Cash calculator
        private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;
        private decimal _paymentAmount;
        private string _paymentReference = string.Empty;
        private decimal _tenderedAmount;
        private bool _autoPrintReceipt = true;
        private SalesOrder? _lastCompletedOrder;

        public ObservableCollection<PosProductItem> AllProducts { get; set; }
        public ObservableCollection<PosCartItem> CartItems { get; set; }
        public ObservableCollection<Warehouse> Warehouses { get; set; }
        public ObservableCollection<PosPaymentEntry> Payments { get; set; }
        public ObservableCollection<PosParkedCart> ParkedCarts { get; set; }

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
                ? $"{SelectedCustomer.FullName} ({_personalDataProtection.Protect(SelectedCustomer.PhoneNumber, PersonalDataKind.Phone)})"
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
                    _ = LoadProducts();
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

        // Cash Change Calculator Properties
        public decimal TenderedAmount
        {
            get => _tenderedAmount;
            set
            {
                if (SetProperty(ref _tenderedAmount, value))
                    OnPropertyChanged(nameof(ChangeAmount));
            }
        }

        public decimal ChangeAmount => TenderedAmount > GrandTotal ? TenderedAmount - GrandTotal : 0m;

        public bool AutoPrintReceipt
        {
            get => _autoPrintReceipt;
            set => SetProperty(ref _autoPrintReceipt, value);
        }

        public SalesOrder? LastCompletedOrder
        {
            get => _lastCompletedOrder;
            set
            {
                if (SetProperty(ref _lastCompletedOrder, value))
                    OnPropertyChanged(nameof(HasLastOrder));
            }
        }

        public bool HasLastOrder => LastCompletedOrder != null;
        public bool HasParkedCarts => ParkedCarts.Count > 0;

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

        public DirectSalesViewModel(
            IAuthService authService,
            IDbContextFactory<AppDbContext> dbContextFactory,
            IToastService toastService,
            ILoadingService loadingService,
            IDirectSalesService directSalesService,
            IThermalReceiptPrintService thermalPrintService,
            IPersonalDataProtectionService personalDataProtection,
            SalesReturnViewModel salesReturnViewModel)
        {
            _authService = authService;
            _dbContextFactory = dbContextFactory;
            _toastService = toastService;
            _loadingService = loadingService;
            _directSalesService = directSalesService;
            _thermalPrintService = thermalPrintService;
            _personalDataProtection = personalDataProtection;
            _salesReturnViewModel = salesReturnViewModel;

            AllProducts = new ObservableCollection<PosProductItem>();
            CartItems = new ObservableCollection<PosCartItem>();
            Warehouses = new ObservableCollection<Warehouse>();
            Payments = new ObservableCollection<PosPaymentEntry>();
            ParkedCarts = new ObservableCollection<PosParkedCart>();

            FilteredProducts = CollectionViewSource.GetDefaultView(AllProducts);
            FilteredProducts.Filter = FilterProducts;

            CartItems.CollectionChanged += (s, e) =>
            {
                UpdateAllTotals();
                if (TenderedAmount > 0) OnPropertyChanged(nameof(ChangeAmount));
            };

            ParkedCarts.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasParkedCarts));

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            _loadingService?.Show();
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var warehouses = await context.Warehouses.ToListAsync();
                Warehouses.Clear();
                foreach (var w in warehouses) Warehouses.Add(w);
                SelectedWarehouse = Warehouses.FirstOrDefault();

                await LoadRecentCustomers();
            }
            finally
            {
                _loadingService?.Hide();
            }
        }

        #region Product Loading

        private async Task LoadProducts()
        {
            AllProducts.Clear();
            if (SelectedWarehouse == null) return;

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var inventories = await context.Inventories
                    .Include(i => i.Product)
                    .Where(i => i.WarehouseId == SelectedWarehouse.Id)
                    .ToListAsync();

                foreach (var inventory in inventories.Where(i => i.Product != null))
                {
                    var product = inventory.Product;
                    AllProducts.Add(new PosProductItem
                    {
                        ProductId = product.Id,
                        ProductName = product.ProductName,
                        ModelName = product.ModelName ?? string.Empty,
                        SKU = product.SKU ?? string.Empty,
                        Barcode = product.Barcode ?? string.Empty,
                        SalePrice = product.SalePrice,
                        VatRate = product.VatRate,
                        StockQuantity = inventory.Quantity,
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
                _toastService?.ShowError("Ürünler yüklenirken hata oluştu.");
            }
            finally
            {
                _loadingService?.Hide();
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

        [RelayCommand]
        private void OpenReturns()
        {
            var window = new Views.SalesReturnWindow
            {
                Owner = System.Windows.Application.Current?.MainWindow,
                DataContext = _salesReturnViewModel
            };
            window.ShowDialog();
        }

        private async Task LoadRecentCustomers()
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var customers = await context.Customers
                    .OrderByDescending(c => c.CreatedDate)
                    .Take(25)
                    .ToListAsync();
                
                RecentCustomers.Clear();
                foreach (var c in customers) RecentCustomers.Add(c);
                FilterCustomers();
            }
            catch { /* non-critical */ }
        }

        private void FilterCustomers()
        {
            FilteredCustomers.Clear();
            var src = RecentCustomers.AsEnumerable();
            var canSearchPhone = _personalDataProtection.CanView(PersonalDataKind.Phone);
            if (!string.IsNullOrWhiteSpace(CustomerSearch))
                src = src.Where(c =>
                    c.FullName.Contains(CustomerSearch, StringComparison.OrdinalIgnoreCase) ||
                    (canSearchPhone && c.PhoneNumber?.Contains(CustomerSearch, StringComparison.OrdinalIgnoreCase) == true));
            foreach (var c in src.Take(10)) FilteredCustomers.Add(c);
        }

        [RelayCommand]
        private void QuickAddCustomer()
        {
            var win = new Views.QuickCustomerAddWindow { Owner = System.Windows.Application.Current?.MainWindow };
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

        [RelayCommand]
        private void SelectCustomer(Customer? customer)
        {
            SelectedCustomer = customer;
            CustomerSearch = string.Empty;
        }

        [RelayCommand]
        private void ClearCustomer()
        {
            SelectedCustomer = null;
            CustomerSearch = string.Empty;
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

        [RelayCommand]
        private void AddToCart(object? parameter)
        {
            if (parameter is not PosProductItem product) return;
            AddProductToCart(product.ProductId, product.ProductName, product.SalePrice, product.VatRate, product.StockQuantity);
        }

        [RelayCommand]
        private void BarcodeScan()
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

        private void OnCartItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateAllTotals();
            if (TenderedAmount > 0) OnPropertyChanged(nameof(ChangeAmount));
        }

        private void AddProductToCart(int productId, string productName, decimal salePrice, int vatRate, int maxQty)
        {
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
                item.PropertyChanged += OnCartItemPropertyChanged;
                CartItems.Add(item);
            }

            UpdateAllTotals();
            StatusMessage = $"'{productName}' sepete eklendi.";
            IsActionSuccessful = true;
            FocusBarcodeRequested = true;
        }

        [RelayCommand]
        private void IncreaseQuantity(object? parameter)
        {
            if (parameter is PosCartItem item)
            {
                if (item.Quantity < item.MaxQuantity)
                {
                    item.Quantity++;
                    UpdateAllTotals();
                }
                else
                {
                    StatusMessage = $"❌ Yetersiz stok! Maksimum stok: {item.MaxQuantity}";
                }
            }
        }

        [RelayCommand]
        private void DecreaseQuantity(object? parameter)
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
                    item.PropertyChanged -= OnCartItemPropertyChanged;
                    CartItems.Remove(item);
                    UpdateAllTotals();
                }
            }
        }

        [RelayCommand]
        private void RemoveFromCart(object? parameter)
        {
            if (parameter is PosCartItem item)
            {
                item.PropertyChanged -= OnCartItemPropertyChanged;
                CartItems.Remove(item);
                UpdateAllTotals();
                StatusMessage = $"'{item.ProductName}' sepetten çıkarıldı.";
                IsActionSuccessful = true;
            }
        }

        [RelayCommand]
        private void ClearCart()
        {
            if (CartItems.Count == 0) return;
            var result = MessageBox.Show("Sepeti temizlemek istiyor musunuz?", "Sepeti Temizle",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                ResetCartState();
                StatusMessage = "Sepet temizlendi.";
                IsActionSuccessful = true;
            }
        }

        private void ResetCartState()
        {
            foreach (var item in CartItems)
            {
                item.PropertyChanged -= OnCartItemPropertyChanged;
            }
            CartItems.Clear();
            Payments.Clear();
            _saleAttemptId = Guid.NewGuid();
            TenderedAmount = 0m;
            UpdateAllTotals();
        }

        #endregion

        #region Hold / Park Cart

        [RelayCommand]
        private void ParkCurrentCart()
        {
            if (CartItems.Count == 0)
            {
                StatusMessage = "⚠️ Park edilecek ürün yok.";
                return;
            }

            var label = SelectedCustomer != null
                ? $"{SelectedCustomer.FullName} ({DateTime.Now:HH:mm})"
                : $"Müşteri #{ParkedCarts.Count + 1} ({DateTime.Now:HH:mm})";

            var parkedCart = new PosParkedCart
            {
                Label = label,
                Customer = SelectedCustomer,
                GlobalDiscountPercent = GlobalDiscountPercent,
                CartItems = CartItems.Select(i => new PosCartItem
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    MaxQuantity = i.MaxQuantity,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TaxRate = i.TaxRate,
                    DiscountPercent = i.DiscountPercent,
                    DiscountType = i.DiscountType
                }).ToList()
            };

            ParkedCarts.Add(parkedCart);
            ResetCartState();
            SelectedCustomer = null;

            StatusMessage = $"📌 Sepet park edildi: {label}";
            IsActionSuccessful = true;
        }

        [RelayCommand]
        private void RestoreParkedCart(PosParkedCart? parkedCart)
        {
            if (parkedCart == null) return;

            if (CartItems.Count > 0)
            {
                var confirm = MessageBox.Show("Mevcut sepet temizlenip parktaki sepet yüklenecek. Devam edilsin mi?",
                    "Sepet Değiştir", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;
            }

            ResetCartState();
            SelectedCustomer = parkedCart.Customer;
            GlobalDiscountPercent = parkedCart.GlobalDiscountPercent;

            foreach (var item in parkedCart.CartItems)
            {
                item.PropertyChanged += OnCartItemPropertyChanged;
                CartItems.Add(item);
            }

            ParkedCarts.Remove(parkedCart);
            UpdateAllTotals();

            StatusMessage = $"▶️ Park edilen sepet geri yüklendi: {parkedCart.Label}";
            IsActionSuccessful = true;
        }

        #endregion

        #region Cash Change Calculator Presets

        [RelayCommand]
        private void SetTenderedPreset(object? param)
        {
            if (param == null) return;
            if (decimal.TryParse(param.ToString(), out var val))
            {
                TenderedAmount = val;
            }
        }

        [RelayCommand]
        private void SetTenderedExact()
        {
            TenderedAmount = GrandTotal;
        }

        #endregion

        #region Payment Operations & Sale Completion

        [RelayCommand]
        private void AddPayment()
        {
            if (PaymentAmount <= 0) return;

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

            if (CanCompleteSale)
            {
                _ = CompleteSale();
            }
        }

        [RelayCommand]
        private void RemovePayment(object? parameter)
        {
            if (parameter is PosPaymentEntry entry)
            {
                Payments.Remove(entry);
                UpdateAllTotals();
            }
        }

        [RelayCommand]
        private async Task PayFullCash() => await PayFull(PaymentMethod.Cash);

        [RelayCommand]
        private async Task PayFullCard() => await PayFull(PaymentMethod.CreditCard);

        [RelayCommand]
        private async Task PayFullOnAccount()
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("Veresiye / Cari Satış yapabilmek için önce kayıtlı bir Müşteri seçmelisiniz! (F7)",
                    "Müşteri Gerekli", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            await PayFull(PaymentMethod.OnAccount);
        }

        [RelayCommand]
        private async Task PayFull(PaymentMethod method)
        {
            if (CartItems.Count == 0 || GrandTotal <= 0) return;

            Payments.Clear();
            Payments.Add(new PosPaymentEntry
            {
                PaymentMethod = method,
                Amount = GrandTotal,
                Reference = string.Empty
            });

            UpdateAllTotals();
            await CompleteSale();
        }

        [RelayCommand]
        private async Task CompleteSale()
        {
            if (!CanCompleteSale || SelectedWarehouse == null) return;

            var paymentDesc = string.Join(", ", Payments.Select(p =>
                $"{GetPaymentMethodName(p.PaymentMethod)}: {p.Amount:C}"));

            var changeNotice = ChangeAmount > 0 ? $"\nPara Üstü: {ChangeAmount:C}" : string.Empty;

            var result = MessageBox.Show(
                $"Toplam: {GrandTotal:C}\n" +
                $"Ödeme: {paymentDesc}{changeNotice}\n\n" +
                "Satışı tamamlamak istiyor musunuz?",
                "Satış Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;
            
            try
            {
                _loadingService?.Show();

                var currentUserName = _authService?.CurrentUser?.Username ?? "Kasiyer";

                var salesOrder = await _directSalesService.ProcessSaleAsync(
                    SelectedCustomer?.Id,
                    CustomerDisplayName,
                    SelectedWarehouse.Id,
                    CartItems,
                    Payments,
                    $"POS Perakende Satış ({SelectedWarehouse.Name})",
                    currentUserName,
                    _saleAttemptId.ToString());

                LastCompletedOrder = salesOrder;
                StatusMessage = $"✅ Satış tamamlandı! Sipariş No: {salesOrder.OrderNumber}";
                IsActionSuccessful = true;

                // Auto-print thermal receipt if enabled
                if (AutoPrintReceipt && _thermalPrintService != null)
                {
                    _ = _thermalPrintService.PrintReceiptAsync(salesOrder);
                }

                ResetCartState();
                await LoadProducts(); // Reload stock quantities

                _toastService?.ShowSuccess($"Satış tamamlandı: Sipariş #{salesOrder.OrderNumber}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Hata: {ex.Message}";
                IsActionSuccessful = false;
                _toastService?.ShowError($"Satış tamamlanamadı:\n{ex.Message}");
            }
            finally
            {
                _loadingService?.Hide();
            }
        }

        [RelayCommand]
        private async Task PrintLastReceipt()
        {
            if (LastCompletedOrder == null)
            {
                _toastService?.ShowError("Yazdırılacak son satış bulunamadı.");
                return;
            }

            if (_thermalPrintService != null)
            {
                await _thermalPrintService.PrintReceiptAsync(LastCompletedOrder);
                _toastService?.ShowSuccess("Fiş yazıcıya gönderildi.");
            }
        }

        private string GetPaymentMethodName(PaymentMethod method) => method switch
        {
            PaymentMethod.Cash => "Nakit",
            PaymentMethod.CreditCard => "Kredi Kartı",
            PaymentMethod.BankTransfer => "Banka Transferi",
            PaymentMethod.MobilePayment => "Mobil Ödeme",
            PaymentMethod.OnAccount => "Cari / Veresiye",
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
            OnPropertyChanged(nameof(ChangeAmount));
        }

        public decimal CartTotal => GrandTotal;
        public decimal CartGrandTotal => GrandTotal;

        #endregion
    }

    #region POS Models

    public class PosParkedCart
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Label { get; set; } = string.Empty;
        public DateTime ParkedAt { get; set; } = DateTime.Now;
        public Customer? Customer { get; set; }
        public decimal GlobalDiscountPercent { get; set; }
        public List<PosCartItem> CartItems { get; set; } = new();
        public decimal GrandTotal => CartItems.Sum(i => i.LineTotal);
        public int ItemCount => CartItems.Sum(i => i.Quantity);
    }

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
            set
            {
                var sanitized = Math.Max(0m, value);
                if (_unitPrice != sanitized) { _unitPrice = sanitized; Notify(); NotifyTotals(); }
            }
        }

        public DiscountType DiscountType
        {
            get => _discountType;
            set { if (_discountType != value) { _discountType = value; Notify(); NotifyTotals(); } }
        }

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                var sanitized = Math.Clamp(value, 0m, 100m);
                if (_discountPercent != sanitized) { _discountPercent = sanitized; Notify(); NotifyTotals(); }
            }
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

        public decimal SubTotal => Quantity * UnitPrice;

        public decimal DiscountAmount => DiscountType == DiscountType.Percentage
            ? SubTotal * DiscountPercent / 100m
            : DiscountFlatAmount;

        public decimal AfterDiscount => SubTotal - DiscountAmount;

        public decimal TaxAmount => AfterDiscount * TaxRate / 100m;

        public decimal LineTotal => AfterDiscount + TaxAmount;

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
        }
    }

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
            PaymentMethod.OnAccount => "Cari / Veresiye",
            _ => PaymentMethod.ToString()
        };
    }

    public class ProductDisplayItem : PosProductItem { }
    public class CartItem : PosCartItem { }

    #endregion
}
