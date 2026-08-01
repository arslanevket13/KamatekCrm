using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Repositories;
using KamatekCrm.Infrastructure.Repositories;
using KamatekCrm.Services.Domain;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Views;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;

namespace KamatekCrm.ViewModels
{
    public partial class PurchaseOrderViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPurchasingDomainService _purchasingService;
        private readonly IApplicationAuthorizationService _authorizationService;

        public PurchaseOrderViewModel(
            IUnitOfWork unitOfWork,
            IPurchasingDomainService purchasingService,
            IApplicationAuthorizationService authorizationService)
        {
            _unitOfWork = unitOfWork;
            _purchasingService = purchasingService;
            _authorizationService = authorizationService;

            // Init
            _ = Refresh();
        }

        #region Properties

        private ObservableCollection<PurchaseOrder> _orders = new ObservableCollection<PurchaseOrder>();
        public ObservableCollection<PurchaseOrder> Orders
        {
            get => _orders;
            set => SetProperty(ref _orders, value);
        }

        private ObservableCollection<Product> _productList = new ObservableCollection<Product>();
        public ObservableCollection<Product> ProductList
        {
            get => _productList;
            set => SetProperty(ref _productList, value);
        }

        private ObservableCollection<Supplier> _suppliers = new ObservableCollection<Supplier>();
        public ObservableCollection<Supplier> Suppliers
        {
            get => _suppliers;
            set => SetProperty(ref _suppliers, value);
        }

        // Selection for New Order
        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set => SetProperty(ref _selectedSupplier, value);
        }

        private ObservableCollection<Warehouse> _warehouses = new ObservableCollection<Warehouse>();
        public ObservableCollection<Warehouse> Warehouses
        {
            get => _warehouses;
            set => SetProperty(ref _warehouses, value);
        }

        private Warehouse? _selectedWarehouse;
        public Warehouse? SelectedWarehouse
        {
            get => _selectedWarehouse;
            set => SetProperty(ref _selectedWarehouse, value);
        }

        public string[] PaymentMethods { get; } = new[]
        {
            "Cari Borç (Vadeli)",
            "Nakit Peşin",
            "Kredi Kartı",
            "Banka Havalesi"
        };

        private string _selectedPaymentMethod = "Cari Borç (Vadeli)";
        public string SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set => SetProperty(ref _selectedPaymentMethod, value);
        }

        // The working list for the new order
        private ObservableCollection<PurchaseOrderItem> _currentOrderItems = new ObservableCollection<PurchaseOrderItem>();
        public ObservableCollection<PurchaseOrderItem> CurrentOrderItems
        {
            get => _currentOrderItems;
            set => SetProperty(ref _currentOrderItems, value);
        }

        // Manual Entry Inputs
        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value))
                {
                    if (value != null)
                    {
                        UnitPrice = value.PurchasePrice;
                    }
                }
            }
        }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set => SetProperty(ref _unitPrice, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        #endregion

        #region Commands

        #endregion

        #region Methods

        [RelayCommand]
        private async Task Refresh()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                // Load Products
                var products = await ((UnitOfWork)_unitOfWork).Context.Products.OrderBy(p => p.ProductName).ToListAsync();
                ProductList = new ObservableCollection<Product>(products);

                // Load Suppliers
                var suppliers = await ((UnitOfWork)_unitOfWork).Context.Suppliers.OrderBy(s => s.CompanyName).ToListAsync();
                Suppliers = new ObservableCollection<Supplier>(suppliers);

                // Load Warehouses
                var warehouses = await ((UnitOfWork)_unitOfWork).Context.Warehouses.Where(w => w.IsActive).OrderBy(w => w.Name).ToListAsync();
                Warehouses = new ObservableCollection<Warehouse>(warehouses);
                SelectedWarehouse = Warehouses.FirstOrDefault();

                // Load Orders (Recent 50?)
                var orders = await ((UnitOfWork)_unitOfWork).Context.PurchaseOrders
                    .Include(o => o.Supplier)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(50)
                    .ToListAsync();
                Orders = new ObservableCollection<PurchaseOrder>(orders);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veri yükleme hatası: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void CreateOrder()
        {
            // Reset form
            CurrentOrderItems.Clear();
            SelectedSupplier = null;
            SelectedProduct = null;
            Quantity = 1;
            UnitPrice = 0;
            MessageBox.Show("Yeni sipariş formu hazırlandı.", "Bilgi");
        }

        [RelayCommand]
        private void AddManualItem()
        {
            if (SelectedProduct == null)
            {
                MessageBox.Show("Lütfen bir ürün seçin.", "Hata");
                return;
            }
            if (Quantity <= 0)
            {
                MessageBox.Show("Miktar 0'dan büyük olmalı.", "Hata");
                return;
            }

            var item = new PurchaseOrderItem
            {
                ProductId = SelectedProduct.Id,
                ProductName = SelectedProduct.ProductName,
                Quantity = Quantity,
                UnitPrice = UnitPrice,
                LineTotal = Quantity * UnitPrice // Ensure calculation
            };

            CurrentOrderItems.Add(item);
            
            // Reset Inputs (keep price? optional, let's reset)
            SelectedProduct = null;
            Quantity = 1;
            UnitPrice = 0;
        }

        [RelayCommand]
        private void UploadPdf()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PDF Dosyaları (*.pdf)|*.pdf",
                Title = "Fatura Yükle (PDF)"
            };

            if (dialog.ShowDialog() == true)
            {
                IsBusy = true;
                try
                {
                    var parser = new Services.PdfInvoiceParserService();
                    var items = parser.Parse(dialog.FileName);

                    if (items.Count == 0)
                    {
                        MessageBox.Show("PDF'ten okunabilen uygun kalem bulunamadı.", "Uyarı");
                        return;
                    }

                    // Show Preview
                    var vm = new PdfImportPreviewViewModel(items);
                    var window = new Views.PdfImportPreviewWindow
                    {
                        DataContext = vm
                    };

                    window.ShowDialog();

                    if (vm.IsConfirmed)
                    {
                        foreach (var item in vm.ParsedItems)
                        {
                            // Try to match with existing product by name query (simple match)
                            // Ideally, we would ask user to map products if not found.
                            // For now, we just add them.
                            CurrentOrderItems.Add(item);
                        }
                        MessageBox.Show($"{vm.ParsedItems.Count} kalem başarıyla eklendi.", "Aktarım Tamamlandı");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hata: {ex.Message}", "PDF Okuma Hatası");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        private async Task SaveOrder()
        {
           await SaveOrderInternal(false);
        }

        [RelayCommand]
        private async Task SaveAndReceive()
        {
            await SaveOrderInternal(true);
        }

        [RelayCommand]
        private void CreateAndAddProduct()
        {
            var win = new QuickNewProductForPurchaseWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (win.ShowDialog() == true)
            {
                var vm = (QuickNewProductForPurchaseViewModel)win.DataContext;
                if (vm.SavedProduct != null)
                {
                    var product = vm.SavedProduct;

                    // Add to product list for future manual selections
                    ProductList.Insert(0, product);

                    // Immediately add as order line
                    var item = new PurchaseOrderItem
                    {
                        ProductId = product.Id,
                        ProductName = product.ProductName,
                        Quantity = vm.InitialQuantity > 0 ? vm.InitialQuantity : 1,
                        UnitPrice = product.PurchasePrice,
                        LineTotal = (vm.InitialQuantity > 0 ? vm.InitialQuantity : 1) * product.PurchasePrice
                    };

                    CurrentOrderItems.Add(item);

                    MessageBox.Show(
                        $"✅ '{product.ProductName}' ürünü oluşturuldu ve siparişe eklendi.",
                        "Ürün Oluşturuldu",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
}

        private async Task SaveOrderInternal(bool autoReceive)
        {
            if (autoReceive)
            {
                var authorization = _authorizationService.Authorize(ApplicationPermission.ApprovePurchase);
                if (authorization.IsFailure)
                {
                    MessageBox.Show(authorization.Error, "Yetkisiz işlem", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (SelectedSupplier == null)
            {
                MessageBox.Show("Tedarikçi seçmelisiniz.", "Hata");
                return;
            }
            if (!CurrentOrderItems.Any())
            {
                MessageBox.Show("Sepette ürün yok.", "Hata");
                return;
            }

            IsBusy = true;
            try
            {
                // 1. Siparişi kaydet
                var order = new PurchaseOrder
                {
                    SupplierId = SelectedSupplier.Id,
                    OrderDate = DateTime.UtcNow,
                    Date = DateTime.UtcNow,
                    Status = autoReceive ? PurchaseStatus.Pending : PurchaseStatus.Pending,
                    InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                    Notes = string.Empty,
                    Items = new ObservableCollection<PurchaseOrderItem>(CurrentOrderItems)
                };

                ((UnitOfWork)_unitOfWork).Context.PurchaseOrders.Add(order);
                await _unitOfWork.SaveChangesAsync();

                // 2. Otomatik teslim al (stok artır + WAC hesapla + cari borç)
                if (autoReceive)
                {
                    var warehouseId = SelectedWarehouse?.Id ?? (await ((UnitOfWork)_unitOfWork).Context.Warehouses.FirstOrDefaultAsync(w => w.IsActive))?.Id ?? 1;

                    var result = await _purchasingService.CompletePurchaseOrderAsync(new PurchaseCompletionRequest
                    {
                        PurchaseOrderId = order.Id,
                        WarehouseId = warehouseId,
                        CreatedBy = App.CurrentUser?.AdSoyad ?? "Sistem"
                    });

                    if (!result.Success)
                    {
                        MessageBox.Show($"Stok işleme hatası: {result.ErrorMessage}", "Uyarı");
                    }
                    else
                    {
                        // Tedarikçi Cari Borç & Kasa Entegrasyonu
                        var context = ((UnitOfWork)_unitOfWork).Context;
                        var dbSupplier = await context.Suppliers.FindAsync(SelectedSupplier.Id);
                        if (dbSupplier != null)
                        {
                            if (SelectedPaymentMethod.Contains("Cari Borç"))
                            {
                                dbSupplier.Balance += result.TotalAmount;
                            }
                            else
                            {
                                var cashExp = new CashTransaction
                                {
                                    Date = DateTime.UtcNow,
                                    Amount = result.TotalAmount,
                                    TransactionType = CashTransactionType.Expense,
                                    Description = $"Satın Alma Faturası Peşin Ödeme: {order.InvoiceNumber} ({SelectedSupplier.CompanyName})",
                                    Category = "Malzeme / Demirbaş",
                                    CreatedBy = App.CurrentUser?.AdSoyad ?? "Sistem",
                                    CreatedAt = DateTime.UtcNow
                                };
                                context.CashTransactions.Add(cashExp);
                            }
                            await _unitOfWork.SaveChangesAsync();
                        }

                        MessageBox.Show($"Sipariş kaydedildi, stoklar ve cari hesap güncellendi.\nToplam: {result.TotalAmount:C}", "Başarılı");
                    }
                }
                else
                {
                    MessageBox.Show("Sipariş başarıyla oluşturuldu (Beklemede).", "Bilgi");
                }

                // Refresh list and clear form
                CreateOrder();
                await Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sipariş kaydetme hatası: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion
    }
}


