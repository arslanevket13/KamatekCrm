using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.DTOs.Transactions;

namespace KamatekCrm.ViewModels
{
    public class PurchasingLineItem : ObservableObject
    {
        public int? ProductId { get; set; }
        public bool IsNewProduct { get; set; }

        private string _productName = "";
        public string ProductName
        {
            get => _productName;
            set => SetProperty(ref _productName, value);
        }

        private string _sku = "";
        public string Sku
        {
            get => _sku;
            set => SetProperty(ref _sku, value);
        }

        private string _barcode = "";
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

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                    UpdateTotal();
            }
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetProperty(ref _unitPrice, value))
                    UpdateTotal();
            }
        }

        private int _vatRate = 20;
        public int VatRate
        {
            get => _vatRate;
            set
            {
                if (SetProperty(ref _vatRate, value))
                    UpdateTotal();
            }
        }

        private decimal _lineTotal;
        public decimal LineTotal
        {
            get => _lineTotal;
            set => SetProperty(ref _lineTotal, value);
        }

        public void UpdateTotal()
        {
            var baseTotal = Quantity * UnitPrice;
            LineTotal = baseTotal + (baseTotal * VatRate / 100m);
        }
    }

    public partial class PurchasingViewModel : ViewModelBase
    {
        private readonly IPurchasingCommandService _purchasingCommands;
        private readonly ITransactionReadService _transactionReads;
        private readonly PurchaseReturnViewModel _purchaseReturnViewModel;
        private readonly IToastService _toastService;
        private Guid _purchaseAttemptId = Guid.NewGuid();

        public PurchasingViewModel(
            IPurchasingCommandService purchasingCommands,
            ITransactionReadService transactionReads,
            PurchaseReturnViewModel purchaseReturnViewModel,
            IToastService toastService)
        {
            _purchasingCommands = purchasingCommands;
            _transactionReads = transactionReads;
            _purchaseReturnViewModel = purchaseReturnViewModel;
            _toastService = toastService;

            OrderItems = new ObservableCollection<PurchasingLineItem>();
            OrderItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(GrandTotal));

            SidebarItem = new PurchasingLineItem();
            SidebarSearchResults = new ObservableCollection<PurchaseProductLookupDto>();

            // Commands

            
                // Init
            _ = InitializeAsync();
        }

        #region Properties
        
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsNotBusy));
                }
            }
        }

        public bool IsNotBusy => !IsBusy;

        // Header Properties
        private ObservableCollection<SupplierLookupDto> _suppliers = new();
        public ObservableCollection<SupplierLookupDto> Suppliers
        {
            get => _suppliers;
            set => SetProperty(ref _suppliers, value);
        }

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

        private string _invoiceNumber = "";
        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set => SetProperty(ref _invoiceNumber, value);
        }

        private DateTime _invoiceDate = DateTime.Today;
        public DateTime InvoiceDate
        {
            get => _invoiceDate;
            set => SetProperty(ref _invoiceDate, value);
        }

        // Main List
        public ObservableCollection<PurchasingLineItem> OrderItems { get; }

        public decimal GrandTotal => OrderItems.Sum(x => x.LineTotal);

        // Sidebar Properties
        private bool _isSidebarOpen;
        public bool IsSidebarOpen
        {
            get => _isSidebarOpen;
            set => SetProperty(ref _isSidebarOpen, value);
        }

        private string _sidebarSearchQuery = "";
        private CancellationTokenSource? _searchCts;
        public string SidebarSearchQuery
        {
            get => _sidebarSearchQuery;
            set
            {
                if (SetProperty(ref _sidebarSearchQuery, value))
                {
                    _ = PerformSearchDebounced(value);
                }
            }
        }

        private bool _isShowingSearchResults;
        public bool IsShowingSearchResults
        {
            get => _isShowingSearchResults;
            set => SetProperty(ref _isShowingSearchResults, value);
        }

        private ObservableCollection<PurchaseProductLookupDto>? _sidebarSearchResults;
        public ObservableCollection<PurchaseProductLookupDto>? SidebarSearchResults
        {
            get => _sidebarSearchResults;
            set => SetProperty(ref _sidebarSearchResults, value);
        }

        private PurchasingLineItem? _sidebarItem;
        public PurchasingLineItem? SidebarItem
        {
            get => _sidebarItem;
            set => SetProperty(ref _sidebarItem, value);
        }

        // History
        private bool _isHistoryOpen;
        public bool IsHistoryOpen
        {
            get => _isHistoryOpen;
            set
            {
                if (SetProperty(ref _isHistoryOpen, value) && value)
                {
                    _ = LoadHistoryAsync();
                }
            }
        }

        private ObservableCollection<PurchaseHistoryDto> _historyOrders = new();
        public ObservableCollection<PurchaseHistoryDto> HistoryOrders
        {
            get => _historyOrders;
            set => SetProperty(ref _historyOrders, value);
        }

        #endregion


        #region Commands

        
        
        
        
        
        
        
        

        #endregion


        #region Methods

        private async Task InitializeAsync()
        {
            IsBusy = true;
            try
            {
                var result = await _transactionReads.GetPurchasingWorkspaceAsync(historyTake: 0);
                if (result.IsFailure || result.Value is null) throw new InvalidOperationException(result.Error);
                Suppliers = new ObservableCollection<SupplierLookupDto>(result.Value.Suppliers);
                Warehouses = new ObservableCollection<WarehouseLookupDto>(result.Value.Warehouses);
                SelectedWarehouse = Warehouses.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Veriler yüklenirken hata oluştu: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ResetSidebar()
        {
            SidebarSearchQuery = "";
            SidebarSearchResults?.Clear();
            IsShowingSearchResults = false;
            SidebarItem = new PurchasingLineItem { IsNewProduct = true };
        }

        [RelayCommand]
        private void OpenSidebar(PurchasingLineItem? item = null)
        {
            if (item != null)
            {
                SidebarItem = item;
                IsSidebarOpen = true;
            }
            else
            {
                ResetSidebar();
                IsSidebarOpen = true;
            }
        }

        [RelayCommand]
        private void CloseSidebar()
        {
            IsSidebarOpen = false;
        }

        [RelayCommand]
        private void OpenHistory()
        {
            var window = new Views.PurchaseReturnWindow
            {
                Owner = System.Windows.Application.Current?.MainWindow,
                DataContext = _purchaseReturnViewModel
            };
            window.ShowDialog();
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
                        _toastService.ShowWarning("PDF'ten okunabilen uygun kalem bulunamadı.");
                        return;
                    }

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
                            OrderItems.Add(new PurchasingLineItem
                            {
                                ProductName = item.ProductName,
                                Quantity = item.Quantity,
                                UnitPrice = item.UnitPrice,
                                VatRate = (int)item.TaxRate,
                                LineTotal = item.LineTotal,
                                IsNewProduct = true
                            });
                        }
                        OnPropertyChanged(nameof(GrandTotal));
                        _toastService.ShowSuccess($"{vm.ParsedItems.Count} kalem PDF faturadan başarıyla eklendi.");
                    }
                }
                catch (Exception ex)
                {
                    _toastService.ShowError($"PDF Okuma Hatası: {ex.Message}");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async Task PerformSearchDebounced(string query)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    SidebarSearchResults.Clear();
                    IsShowingSearchResults = false;
                    SidebarItem.IsNewProduct = true;
                    return;
                }

                await Task.Delay(300, token); // Debounce

                var result = await _transactionReads.SearchPurchaseProductsAsync(query, 10, token);
                if (result.IsFailure || result.Value is null) throw new InvalidOperationException(result.Error);
                SidebarSearchResults = new ObservableCollection<PurchaseProductLookupDto>(result.Value);
                IsShowingSearchResults = result.Value.Any();
            }
            catch (TaskCanceledException)
            {
                // Ignored
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
            }
        }

        [RelayCommand]
        private void SelectSearchResult(PurchaseProductLookupDto? product)
        {
            if (product == null) return;
            
            SidebarItem!.ProductId = product.Id;
            SidebarItem.IsNewProduct = false;
            SidebarItem.ProductName = product.ProductName;
            SidebarItem.Sku = product.Sku;
            SidebarItem.Barcode = product.Barcode;
            SidebarItem.Unit = product.Unit;
            SidebarItem.UnitPrice = product.PurchasePrice;
            SidebarItem.VatRate = product.VatRate;
            SidebarItem.Quantity = 1;
            SidebarItem.UpdateTotal();

            IsShowingSearchResults = false;
        }

        [RelayCommand]
        private void CreateNewProduct()
        {
            SidebarItem.IsNewProduct = true;
            IsShowingSearchResults = false;
            SidebarItem.ProductName = SidebarSearchQuery;
        }

        [RelayCommand]
        private void AddLineItem()
        {
            if (SidebarItem == null) return;

            if (string.IsNullOrWhiteSpace(SidebarItem.ProductName))
            {
                _toastService.ShowWarning("Ürün adı girilmelidir.");
                return;
            }
            if (SidebarItem.Quantity <= 0)
            {
                _toastService.ShowWarning("Miktar 0'dan büyük olmalıdır.");
                return;
            }

            OrderItems.Add(SidebarItem);
            OnPropertyChanged(nameof(GrandTotal));
            ResetSidebar();
            IsSidebarOpen = false;
        }

        [RelayCommand]
        private void RemoveLineItem(PurchasingLineItem? item)
        {
            if (item != null)
            {
                OrderItems.Remove(item);
                OnPropertyChanged(nameof(GrandTotal));
            }
        }

        [RelayCommand]
        private async Task SaveAndReceive()
        {
            if (SelectedSupplier is null || SelectedWarehouse is null)
            {
                _toastService.ShowWarning("Tedarikçi ve teslim deposu seçilmelidir.");
                return;
            }
            if (OrderItems.Count == 0)
            {
                _toastService.ShowWarning("En az bir satın alma kalemi ekleyin.");
                return;
            }

            IsBusy = true;
            try
            {
                var paymentMethod = ParsePaymentMethod(SelectedPaymentMethod);
                var command = new CreatePurchaseCommand(
                    SelectedSupplier.Id,
                    string.IsNullOrWhiteSpace(InvoiceNumber) ? $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}" : InvoiceNumber,
                    InvoiceDate,
                    OrderItems.Select(item => new PurchaseLineInput(
                        item.ProductId,
                        item.ProductName,
                        item.Quantity,
                        item.UnitPrice,
                        0,
                        item.VatRate,
                        item.LineTotal,
                        item.Sku,
                        item.Barcode,
                        item.Unit)).ToList(),
                    null,
                    App.CurrentUser?.AdSoyad ?? "Sistem",
                    _purchaseAttemptId.ToString(),
                    true,
                    SelectedWarehouse.Id,
                    new[] { new PaymentAllocationInput(paymentMethod, GrandTotal) });
                var result = await _purchasingCommands.CreatePurchaseAsync(command);
                if (result.IsFailure)
                {
                    _toastService.ShowError(result.Error);
                    return;
                }

                _toastService.ShowSuccess("Sipariş, stok, cari ve kasa kayıtları tek işlemde tamamlandı.");
                OrderItems.Clear();
                _purchaseAttemptId = Guid.NewGuid();
                SelectedSupplier = null;
                InvoiceNumber = string.Empty;
                InvoiceDate = DateTime.Today;
                OnPropertyChanged(nameof(GrandTotal));
                await LoadHistoryAsync();
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

        private async Task LoadHistoryAsync()
        {
            try
            {
                var result = await _transactionReads.GetPurchaseHistoryAsync(20);
                if (result.IsFailure || result.Value is null) throw new InvalidOperationException(result.Error);
                HistoryOrders = new ObservableCollection<PurchaseHistoryDto>(result.Value);
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Geçmiş yüklenemedi: {ex.Message}");
            }
        }

        #endregion

    }
}




