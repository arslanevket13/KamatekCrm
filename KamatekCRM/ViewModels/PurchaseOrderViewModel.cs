using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Views;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.ApplicationCore.DTOs.Transactions;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.ViewModels
{
    public partial class PurchaseOrderViewModel : ViewModelBase
    {
        private readonly IPurchasingCommandService _purchasingCommands;
        private readonly ITransactionReadService _transactionReads;
        private readonly IApplicationAuthorizationService _authorizationService;
        private readonly IDialogService _dialogs;
        private Guid _purchaseAttemptId = Guid.NewGuid();

        public PurchaseOrderViewModel(
            IPurchasingCommandService purchasingCommands,
            ITransactionReadService transactionReads,
            IApplicationAuthorizationService authorizationService,
            IDialogService dialogs)
        {
            _purchasingCommands = purchasingCommands;
            _transactionReads = transactionReads;
            _authorizationService = authorizationService;
            _dialogs = dialogs;

            // Init
            _ = Refresh();
        }

        #region Properties

        private ObservableCollection<PurchaseHistoryDto> _orders = new();
        public ObservableCollection<PurchaseHistoryDto> Orders
        {
            get => _orders;
            set => SetProperty(ref _orders, value);
        }

        private ObservableCollection<PurchaseProductLookupDto> _productList = new();
        public ObservableCollection<PurchaseProductLookupDto> ProductList
        {
            get => _productList;
            set => SetProperty(ref _productList, value);
        }

        private ObservableCollection<SupplierLookupDto> _suppliers = new();
        public ObservableCollection<SupplierLookupDto> Suppliers
        {
            get => _suppliers;
            set => SetProperty(ref _suppliers, value);
        }

        // Selection for New Order
        private SupplierLookupDto? _selectedSupplier;
        public SupplierLookupDto? SelectedSupplier
        {
            get => _selectedSupplier;
            set => SetProperty(ref _selectedSupplier, value);
        }

        private ObservableCollection<WarehouseLookupDto> _warehouses = new();
        public ObservableCollection<WarehouseLookupDto> Warehouses
        {
            get => _warehouses;
            set => SetProperty(ref _warehouses, value);
        }

        private WarehouseLookupDto? _selectedWarehouse;
        public WarehouseLookupDto? SelectedWarehouse
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
        private PurchaseProductLookupDto? _selectedProduct;
        public PurchaseProductLookupDto? SelectedProduct
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
                var result = await _transactionReads.GetPurchasingWorkspaceAsync(historyTake: 50);
                if (result.IsFailure || result.Value is null)
                    throw new InvalidOperationException(result.Error);
                ProductList = new ObservableCollection<PurchaseProductLookupDto>(result.Value.Products);
                Suppliers = new ObservableCollection<SupplierLookupDto>(result.Value.Suppliers);
                Warehouses = new ObservableCollection<WarehouseLookupDto>(result.Value.Warehouses);
                SelectedWarehouse = Warehouses.FirstOrDefault();
                Orders = new ObservableCollection<PurchaseHistoryDto>(result.Value.RecentOrders);
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync($"Veri yükleme hatası: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CreateOrder()
        {
            ResetOrderForm();
            await _dialogs.ShowMessageAsync("Yeni sipariş formu hazırlandı.", "Bilgi");
        }

        private void ResetOrderForm()
        {
            _purchaseAttemptId = Guid.NewGuid();
            CurrentOrderItems.Clear();
            SelectedSupplier = null;
            SelectedProduct = null;
            Quantity = 1;
            UnitPrice = 0;
        }

        [RelayCommand]
        private async Task AddManualItem()
        {
            if (SelectedProduct == null)
            {
                await _dialogs.ShowWarningAsync("Lütfen bir ürün seçin.");
                return;
            }
            if (Quantity <= 0)
            {
                await _dialogs.ShowWarningAsync("Miktar 0'dan büyük olmalı.");
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
        private async Task UploadPdf()
        {
            var filePath = await _dialogs.ShowOpenFileDialogAsync("Fatura Yükle (PDF)", "PDF Dosyaları (*.pdf)|*.pdf");

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                IsBusy = true;
                try
                {
                    var parser = new Services.PdfInvoiceParserService();
                    var items = parser.Parse(filePath);

                    if (items.Count == 0)
                    {
                        await _dialogs.ShowWarningAsync("PDF'ten okunabilen uygun kalem bulunamadı.");
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
                        await _dialogs.ShowMessageAsync($"{vm.ParsedItems.Count} kalem başarıyla eklendi.", "Aktarım Tamamlandı");
                    }
                }
                catch (Exception ex)
                {
                    await _dialogs.ShowErrorAsync($"Hata: {ex.Message}", "PDF Okuma Hatası");
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
        private async Task CreateAndAddProduct()
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
                    ProductList.Insert(0, new PurchaseProductLookupDto(
                        product.Id,
                        product.ProductName,
                        product.SKU,
                        product.Barcode,
                        product.Unit,
                        product.PurchasePrice,
                        product.VatRate));

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

                    await _dialogs.ShowMessageAsync(
                        $"✅ '{product.ProductName}' ürünü oluşturuldu ve siparişe eklendi.",
                        "Ürün Oluşturuldu");
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
                    await _dialogs.ShowWarningAsync(authorization.Error, "Yetkisiz işlem");
                    return;
                }
            }

            if (SelectedSupplier == null)
            {
                await _dialogs.ShowWarningAsync("Tedarikçi seçmelisiniz.");
                return;
            }
            if (!CurrentOrderItems.Any())
            {
                await _dialogs.ShowWarningAsync("Sepette ürün yok.");
                return;
            }

            IsBusy = true;
            try
            {
                var total = CurrentOrderItems.Sum(item => item.LineTotal > 0 ? item.LineTotal : item.Quantity * item.UnitPrice);
                var paymentMethod = ParsePaymentMethod(SelectedPaymentMethod);
                var command = new CreatePurchaseCommand(
                    SelectedSupplier.Id,
                    $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                    DateTime.UtcNow,
                    CurrentOrderItems.Select(item => new PurchaseLineInput(
                        item.ProductId,
                        item.ProductName,
                        item.Quantity,
                        item.UnitPrice,
                        item.DiscountAmount,
                        item.TaxRate,
                        item.LineTotal > 0 ? item.LineTotal : item.Quantity * item.UnitPrice)).ToList(),
                    null,
                    App.CurrentUser?.AdSoyad ?? "Sistem",
                    _purchaseAttemptId.ToString(),
                    autoReceive,
                    autoReceive ? SelectedWarehouse?.Id : null,
                    autoReceive ? new[] { new PaymentAllocationInput(paymentMethod, total) } : null);
                var result = await _purchasingCommands.CreatePurchaseAsync(command);
                if (result.IsFailure || result.Value is null)
                {
                    await _dialogs.ShowErrorAsync(result.Error, "Satın alma başarısız");
                    return;
                }

                await _dialogs.ShowMessageAsync(
                    autoReceive
                        ? $"Sipariş, stok ve finans kayıtları tek işlemde tamamlandı.\nToplam: {result.Value.TotalAmount:C}"
                        : "Sipariş başarıyla oluşturuldu (Beklemede).",
                    "Başarılı");

                // Refresh list and clear form
                ResetOrderForm();
                await Refresh();
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync($"Sipariş kaydetme hatası: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static PaymentMethod ParsePaymentMethod(string value) =>
            value.Contains("Cari", StringComparison.OrdinalIgnoreCase) ? PaymentMethod.OnAccount :
            value.Contains("Kart", StringComparison.OrdinalIgnoreCase) ? PaymentMethod.CreditCard :
            value.Contains("Havale", StringComparison.OrdinalIgnoreCase) ? PaymentMethod.BankTransfer :
            PaymentMethod.Cash;

        #endregion
    }
}


