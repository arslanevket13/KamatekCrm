using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Shared.Repositories;
using KamatekCrm.Infrastructure.Repositories;
using KamatekCrm.Services;
using KamatekCrm.Services.Domain;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPurchasingDomainService _purchasingService;
        private readonly IToastService _toastService;

        public PurchasingViewModel(IUnitOfWork unitOfWork, IPurchasingDomainService purchasingService, IToastService toastService)
        {
            _unitOfWork = unitOfWork;
            _purchasingService = purchasingService;
            _toastService = toastService;

            OrderItems = new ObservableCollection<PurchasingLineItem>();
            OrderItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(GrandTotal));

            SidebarItem = new PurchasingLineItem();
            SidebarSearchResults = new ObservableCollection<Product>();

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
        private ObservableCollection<Supplier> _suppliers = new();
        public ObservableCollection<Supplier> Suppliers
        {
            get => _suppliers;
            set => SetProperty(ref _suppliers, value);
        }

        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set => SetProperty(ref _selectedSupplier, value);
        }

        private ObservableCollection<Warehouse> _warehouses = new();
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

        private ObservableCollection<Product>? _sidebarSearchResults;
        public ObservableCollection<Product>? SidebarSearchResults
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

        private ObservableCollection<PurchaseOrder> _historyOrders = new();
        public ObservableCollection<PurchaseOrder> HistoryOrders
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
                var context = ((UnitOfWork)_unitOfWork).Context;
                var suppliers = await context.Suppliers
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.CompanyName)
                    .ToListAsync();
                Suppliers = new ObservableCollection<Supplier>(suppliers);

                var warehouses = await context.Warehouses
                    .Where(w => w.IsActive)
                    .OrderBy(w => w.Name)
                    .ToListAsync();
                Warehouses = new ObservableCollection<Warehouse>(warehouses);
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
            IsHistoryOpen = !IsHistoryOpen;
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

                var results = await ((UnitOfWork)_unitOfWork).Context.Products
                    .Where(p => EF.Functions.ILike(p.ProductName, $"%{query}%") || 
                                EF.Functions.ILike(p.SKU, $"%{query}%") || 
                                EF.Functions.ILike(p.Barcode, $"%{query}%"))
                    .Take(10)
                    .ToListAsync(token);

                SidebarSearchResults = new ObservableCollection<Product>(results);
                IsShowingSearchResults = results.Any();
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
        private void SelectSearchResult(Product? product)
        {
            if (product == null) return;
            
            SidebarItem!.ProductId = product.Id;
            SidebarItem.IsNewProduct = false;
            SidebarItem.ProductName = product.ProductName;
            SidebarItem.Sku = product.SKU;
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
            if (SelectedSupplier == null)
            {
                _toastService.ShowWarning("Lütfen bir tedarikçi seçin.");
                return;
            }
            if (!OrderItems.Any())
            {
                _toastService.ShowWarning("Lütfen listeye en az bir ürün ekleyin.");
                return;
            }
            if (string.IsNullOrWhiteSpace(InvoiceNumber))
            {
                InvoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
            }

            IsBusy = true;
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var order = new PurchaseOrder
                {
                    SupplierId = SelectedSupplier.Id,
                    InvoiceNumber = InvoiceNumber,
                    OrderDate = InvoiceDate,
                    Date = DateTime.UtcNow,
                    Status = PurchaseStatus.Pending,
                    Items = new List<PurchaseOrderItem>()
                };

                foreach (var line in OrderItems)
                {
                    int productId;
                    if (line.IsNewProduct)
                    {
                        var newProd = new Product
                        {
                            ProductName = line.ProductName,
                            SKU = string.IsNullOrWhiteSpace(line.Sku) ? $"SKU-{DateTime.Now.Ticks.ToString()[^6..]}" : line.Sku,
                            Barcode = line.Barcode,
                            Unit = string.IsNullOrWhiteSpace(line.Unit) ? "Adet" : line.Unit,
                            VatRate = line.VatRate,
                            PurchasePrice = line.UnitPrice,
                            TotalStockQuantity = 0, // Will be updated by DomainService
                            AverageCost = 0,
                            ProductCategoryType = ProductCategoryType.Other // Default
                        };
                        ((UnitOfWork)_unitOfWork).Context.Products.Add(newProd);
                        await _unitOfWork.SaveChangesAsync(); // To get the new ID
                        productId = newProd.Id;
                    }
                    else
                    {
                        productId = line.ProductId!.Value;
                        // Opsiyonel: Ürünün mevcut KDV oranını da güncelleyebiliriz
                        // var existingProduct = await ((UnitOfWork)_unitOfWork).Context.Products.FindAsync(productId);
                        // if(existingProduct != null) { existingProduct.VatRate = line.VatRate; }
                    }

                    var poItem = new PurchaseOrderItem
                    {
                        ProductId = productId,
                        ProductName = line.ProductName,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        TaxRate = line.VatRate,
                        TaxAmount = (line.Quantity * line.UnitPrice) * line.VatRate / 100m,
                        LineTotal = line.LineTotal,
                        SubTotal = line.Quantity * line.UnitPrice
                    };
                    order.Items.Add(poItem);
                    order.TotalAmount += poItem.LineTotal;
                }

                var context = ((UnitOfWork)_unitOfWork).Context;
                context.PurchaseOrders.Add(order);
                await _unitOfWork.SaveChangesAsync(); // Save PO to get PO Id

                // Stok İşlemi İçin Domain Service Çağrısı
                var warehouseId = SelectedWarehouse?.Id ?? (await context.Warehouses.FirstOrDefaultAsync(w => w.IsActive))?.Id ?? 1;

                var result = await _purchasingService.CompletePurchaseOrderAsync(new PurchaseCompletionRequest
                {
                    PurchaseOrderId = order.Id,
                    WarehouseId = warehouseId,
                    CreatedBy = App.CurrentUser?.AdSoyad
                });

                if (result.Success)
                {
                    // Tedarikçi Cari Borç & Kasa Entegrasyonu
                    var dbSupplier = await context.Suppliers.FindAsync(SelectedSupplier.Id);
                    if (dbSupplier != null)
                    {
                        if (SelectedPaymentMethod.Contains("Cari Borç"))
                        {
                            dbSupplier.Balance += order.TotalAmount;
                        }
                        else
                        {
                            var cashExp = new CashTransaction
                            {
                                Date = DateTime.UtcNow,
                                Amount = order.TotalAmount,
                                TransactionType = SelectedPaymentMethod.Contains("Kart") ? CashTransactionType.Expense : CashTransactionType.Expense,
                                Description = $"Satın Alma Faturası Peşin Ödeme: {InvoiceNumber} ({SelectedSupplier.CompanyName})",
                                Category = "Malzeme / Demirbaş",
                                CreatedBy = App.CurrentUser?.AdSoyad ?? "Sistem",
                                CreatedAt = DateTime.UtcNow
                            };
                            context.CashTransactions.Add(cashExp);
                        }
                    }

                    await _unitOfWork.SaveChangesAsync();
                    await _unitOfWork.CommitAsync();
                    _toastService.ShowSuccess("İşlem başarıyla tamamlandı, stoklar ve cari hesap güncellendi.");
                    
                    // Reset Form
                    OrderItems.Clear();
                    SelectedSupplier = null;
                    InvoiceNumber = "";
                    InvoiceDate = DateTime.Today;
                    OnPropertyChanged(nameof(GrandTotal));
                }
                else
                {
                    await _unitOfWork.RollbackAsync();
                    _toastService.ShowError($"Stoklara işlenirken hata oluştu: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                _toastService.ShowError($"Kritik hata: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadHistoryAsync()
        {
            try
            {
                var orders = await ((UnitOfWork)_unitOfWork).Context.PurchaseOrders
                    .Include(o => o.Supplier)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(20)
                    .ToListAsync();
                HistoryOrders = new ObservableCollection<PurchaseOrder>(orders);
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Geçmiş yüklenemedi: {ex.Message}");
            }
        }

        #endregion

    }
}




