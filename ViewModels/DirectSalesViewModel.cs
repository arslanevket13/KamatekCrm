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
using KamatekCrm.Enums;
using KamatekCrm.Models;
using KamatekCrm.Services;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Perakende Satış (POS) ViewModel
    /// Mouse/Klavye için optimize edilmiş satış modülü
    /// </summary>
    public class DirectSalesViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private string _searchText = string.Empty;
        private string _customerName = "Perakende Müşteri";
        private string _statusMessage = string.Empty;
        private bool _isActionSuccessful;
        private Warehouse? _selectedWarehouse;

        public ObservableCollection<ProductDisplayItem> AllProducts { get; set; }
        public ObservableCollection<CartItem> CartItems { get; set; }
        public ObservableCollection<Warehouse> Warehouses { get; set; }

        /// <summary>
        /// Ürün listesi için filtreleme view
        /// </summary>
        public ICollectionView FilteredProducts { get; private set; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilteredProducts?.Refresh();
                }
            }
        }

        public string CustomerName
        {
            get => _customerName;
            set => SetProperty(ref _customerName, value);
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
                {
                    LoadProducts();
                }
            }
        }

        /// <summary>
        /// Sepet Toplamı
        /// </summary>
        public decimal CartTotal => CartItems?.Sum(i => i.TotalPrice) ?? 0;

        /// <summary>
        /// Sepetteki toplam ürün sayısı
        /// </summary>
        public int CartItemCount => CartItems?.Sum(i => i.Quantity) ?? 0;

        // Komutlar
        public ICommand AddToCartCommand { get; }
        public ICommand IncreaseQuantityCommand { get; }
        public ICommand DecreaseQuantityCommand { get; }
        public ICommand RemoveFromCartCommand { get; }
        public ICommand ProcessCashPaymentCommand { get; }
        public ICommand ProcessCardPaymentCommand { get; }
        public ICommand ClearCartCommand { get; }

        public DirectSalesViewModel()
        {
            _context = new AppDbContext();
            AllProducts = new ObservableCollection<ProductDisplayItem>();
            CartItems = new ObservableCollection<CartItem>();
            Warehouses = new ObservableCollection<Warehouse>(_context.Warehouses.Where(w => w.IsActive).ToList());

            // Varsayılan depoyu seç
            SelectedWarehouse = Warehouses.FirstOrDefault();

            // Filtreleme view
            FilteredProducts = CollectionViewSource.GetDefaultView(AllProducts);
            FilteredProducts.Filter = FilterProducts;

            // Komutları tanımla
            AddToCartCommand = new RelayCommand(ExecuteAddToCart, CanAddToCart);
            IncreaseQuantityCommand = new RelayCommand(ExecuteIncreaseQuantity);
            DecreaseQuantityCommand = new RelayCommand(ExecuteDecreaseQuantity);
            RemoveFromCartCommand = new RelayCommand(ExecuteRemoveFromCart);
            ProcessCashPaymentCommand = new RelayCommand(_ => ExecuteProcessPayment(PaymentMethod.Cash), _ => CanProcessPayment());
            ProcessCardPaymentCommand = new RelayCommand(_ => ExecuteProcessPayment(PaymentMethod.CreditCard), _ => CanProcessPayment());
            ClearCartCommand = new RelayCommand(_ => ExecuteClearCart(), _ => CartItems.Count > 0);

            // Sepet değişikliklerini dinle
            CartItems.CollectionChanged += (s, e) => UpdateCartTotals();
        }

        /// <summary>
        /// Ürünleri yükle
        /// </summary>
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

                    AllProducts.Add(new ProductDisplayItem
                    {
                        ProductId = product.Id,
                        ProductName = product.ProductName,
                        ModelName = product.ModelName ?? string.Empty,
                        SKU = product.SKU ?? string.Empty,
                        SalePrice = product.SalePrice,
                        StockQuantity = inventory?.Quantity ?? 0,
                        Unit = product.Unit
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

        /// <summary>
        /// Ürün filtreleme
        /// </summary>
        private bool FilterProducts(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            if (obj is ProductDisplayItem product)
            {
                return product.ProductName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || product.ModelName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || product.SKU.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private bool CanAddToCart(object? parameter)
        {
            // Negatif stok satışına izin ver (stok kontrolü kaldırıldı)
            return parameter is ProductDisplayItem;
        }

        /// <summary>
        /// Sepete ekle
        /// </summary>
        private void ExecuteAddToCart(object? parameter)
        {
            if (parameter is not ProductDisplayItem product) return;

            // Mevcut sepet kontrolü (stok sınırı kaldırıldı - negatif stok satışı destekleniyor)
            var existingCartItem = CartItems.FirstOrDefault(i => i.ProductId == product.ProductId);
            var currentCartQty = existingCartItem?.Quantity ?? 0;

            if (existingCartItem != null)
            {
                // Mevcut ürünün miktarını artır
                existingCartItem.Quantity++;
            }
            else
            {
                // Yeni ürün ekle
                var cartItem = new CartItem
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    UnitPrice = product.SalePrice,
                    Quantity = 1,
                    MaxQuantity = product.StockQuantity
                };

                cartItem.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(CartItem.TotalPrice) || e.PropertyName == nameof(CartItem.Quantity))
                    {
                        UpdateCartTotals();
                    }
                };

                CartItems.Add(cartItem);
            }

            UpdateCartTotals();
            StatusMessage = $"'{product.ProductName}' sepete eklendi.";
            IsActionSuccessful = true;
        }

        /// <summary>
        /// Miktar artır
        /// </summary>
        private void ExecuteIncreaseQuantity(object? parameter)
        {
            if (parameter is CartItem item)
            {
                if (item.Quantity < item.MaxQuantity)
                {
                    item.Quantity++;
                    UpdateCartTotals();
                }
                else
                {
                    StatusMessage = $"Maksimum stok: {item.MaxQuantity}";
                    IsActionSuccessful = false;
                }
            }
        }

        /// <summary>
        /// Miktar azalt
        /// </summary>
        private void ExecuteDecreaseQuantity(object? parameter)
        {
            if (parameter is CartItem item)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                    UpdateCartTotals();
                }
                else
                {
                    // Miktar 1 ise sepetten çıkar
                    CartItems.Remove(item);
                    UpdateCartTotals();
                }
            }
        }

        /// <summary>
        /// Sepetten çıkar
        /// </summary>
        private void ExecuteRemoveFromCart(object? parameter)
        {
            if (parameter is CartItem item)
            {
                CartItems.Remove(item);
                UpdateCartTotals();
                StatusMessage = $"'{item.ProductName}' sepetten çıkarıldı.";
                IsActionSuccessful = true;
            }
        }

        private bool CanProcessPayment()
        {
            return CartItems.Count > 0 && SelectedWarehouse != null;
        }

        /// <summary>
        /// Ödeme işle - Domain Service'e delege edildi
        /// </summary>
        private void ExecuteProcessPayment(PaymentMethod paymentMethod)
        {
            if (SelectedWarehouse == null || CartItems.Count == 0) return;

            var paymentName = paymentMethod == PaymentMethod.Cash ? "Nakit" : "Kredi Kartı";
            var result = MessageBox.Show(
                $"Toplam: {CartTotal:C}\n" +
                $"Ödeme: {paymentName}\n\n" +
                "Satışı tamamlamak istiyor musunuz?",
                "Satış Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // Domain Service'e delege et
            var salesService = new KamatekCrm.Services.Domain.SalesDomainService();
            var request = new KamatekCrm.Services.Domain.SaleRequest
            {
                WarehouseId = SelectedWarehouse.Id,
                CustomerName = CustomerName,
                PaymentMethod = paymentMethod,
                CreatedBy = AuthService.CurrentUser?.AdSoyad ?? "Sistem",
                Items = CartItems.Select(c => new KamatekCrm.Services.Domain.SaleItemRequest
                {
                    ProductId = c.ProductId,
                    ProductName = c.ProductName,
                    Quantity = c.Quantity,
                    UnitPrice = c.UnitPrice
                }).ToList()
            };

            var saleResult = salesService.ProcessSale(request);

            if (saleResult.Success)
            {
                StatusMessage = $"✅ Satış tamamlandı! Sipariş No: {saleResult.OrderNumber}";
                IsActionSuccessful = true;

                // Sepeti temizle ve ürünleri yenile
                CartItems.Clear();
                UpdateCartTotals();
                LoadProducts();

                MessageBox.Show(
                    $"Satış Başarılı!\n\n" +
                    $"Sipariş No: {saleResult.OrderNumber}\n" +
                    $"Toplam: {saleResult.TotalAmount:C}\n" +
                    $"Ödeme: {paymentName}",
                    "Satış Tamamlandı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = $"Satış hatası: {saleResult.ErrorMessage}";
                IsActionSuccessful = false;
                MessageBox.Show($"Satış işlemi sırasında hata oluştu:\n{saleResult.ErrorMessage}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Sepeti temizle
        /// </summary>
        private void ExecuteClearCart()
        {
            if (CartItems.Count == 0) return;

            var result = MessageBox.Show(
                "Sepeti temizlemek istiyor musunuz?",
                "Sepeti Temizle",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                CartItems.Clear();
                UpdateCartTotals();
                StatusMessage = "Sepet temizlendi.";
                IsActionSuccessful = true;
            }
        }

        /// <summary>
        /// Sepet toplamlarını güncelle
        /// </summary>
        private void UpdateCartTotals()
        {
            OnPropertyChanged(nameof(CartTotal));
            OnPropertyChanged(nameof(CartItemCount));
        }
    }

    /// <summary>
    /// Ürün görüntüleme modeli (DataGrid için)
    /// </summary>
    public class ProductDisplayItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public decimal SalePrice { get; set; }
        public int StockQuantity { get; set; }
        public string Unit { get; set; } = "Adet";
    }

    /// <summary>
    /// Sepet kalemi
    /// </summary>
    public class CartItem : INotifyPropertyChanged
    {
        private int _quantity;

        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int MaxQuantity { get; set; }

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPrice));
                }
            }
        }

        public decimal TotalPrice => Quantity * UnitPrice;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
