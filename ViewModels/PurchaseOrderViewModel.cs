using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
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
    /// Satın Alma Yönetimi ViewModel
    /// Tedarikçi ve PO işlemleri
    /// </summary>
    public class PurchaseOrderViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;

        #region Properties

        public ObservableCollection<Supplier> Suppliers { get; } = new();
        public ObservableCollection<PurchaseOrder> PurchaseOrders { get; } = new();
        public ObservableCollection<Product> Products { get; } = new();

        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value))
                {
                    LoadPurchaseOrders();
                }
            }
        }

        private PurchaseOrder? _selectedOrder;
        public PurchaseOrder? SelectedOrder
        {
            get => _selectedOrder;
            set
            {
                if (SetProperty(ref _selectedOrder, value))
                {
                    CalculateOrderTotals();
                }
            }
        }

        // Yeni PO Formu
        private string _newSupplierName = string.Empty;
        public string NewSupplierName
        {
            get => _newSupplierName;
            set => SetProperty(ref _newSupplierName, value);
        }

        private DateTime _expectedDate = DateTime.Today.AddDays(7);
        public DateTime ExpectedDate
        {
            get => _expectedDate;
            set => SetProperty(ref _expectedDate, value);
        }

        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        // Tedarikçi Borç Özeti
        private decimal _totalSupplierDebt;
        public decimal TotalSupplierDebt
        {
            get => _totalSupplierDebt;
            set => SetProperty(ref _totalSupplierDebt, value);
        }

        // Seçili Sipariş Özet Bilgileri
        private decimal _orderSubTotal;
        public decimal OrderSubTotal
        {
            get => _orderSubTotal;
            set => SetProperty(ref _orderSubTotal, value);
        }

        private decimal _orderTaxAmount;
        public decimal OrderTaxAmount
        {
            get => _orderTaxAmount;
            set => SetProperty(ref _orderTaxAmount, value);
        }

        private decimal _orderDiscountAmount;
        public decimal OrderDiscountAmount
        {
            get => _orderDiscountAmount;
            set => SetProperty(ref _orderDiscountAmount, value);
        }

        private decimal _orderGrandTotal;
        public decimal OrderGrandTotal
        {
            get => _orderGrandTotal;
            set => SetProperty(ref _orderGrandTotal, value);
        }

        // Kalem Ekleme
        private Product? _selectedProductToAdd;
        public Product? SelectedProductToAdd
        {
            get => _selectedProductToAdd;
            set
            {
                if (SetProperty(ref _selectedProductToAdd, value) && value != null)
                {
                    UnitPriceToAdd = value.PurchasePrice; // Alış fiyatını getir
                }
            }
        }

        private int _quantityToAdd = 1;
        public int QuantityToAdd
        {
            get => _quantityToAdd;
            set => SetProperty(ref _quantityToAdd, value);
        }

        private decimal _unitPriceToAdd;
        public decimal UnitPriceToAdd
        {
            get => _unitPriceToAdd;
            set => SetProperty(ref _unitPriceToAdd, value);
        }

        private int _taxRateToAdd = 20;
        public int TaxRateToAdd
        {
            get => _taxRateToAdd;
            set => SetProperty(ref _taxRateToAdd, value);
        }

        private decimal _discountRateToAdd;
        public decimal DiscountRateToAdd
        {
            get => _discountRateToAdd;
            set => SetProperty(ref _discountRateToAdd, value);
        }

        #endregion

        #region Commands

        public ICommand CreateOrderCommand { get; }
        public ICommand ReceiveGoodsCommand { get; }
        public ICommand CancelOrderCommand { get; }
        public ICommand AddSupplierCommand { get; }
        public ICommand RefreshCommand { get; }
        
        // Yeni Komutlar
        public ICommand AddOrderItemCommand { get; }
        public ICommand RemoveOrderItemCommand { get; }

        #endregion

        #region Constructor

        public PurchaseOrderViewModel()
        {
            _context = new AppDbContext();

            CreateOrderCommand = new RelayCommand(_ => CreateOrder(), _ => CanCreateOrder());
            ReceiveGoodsCommand = new RelayCommand(ReceiveGoods, CanReceiveGoods);
            CancelOrderCommand = new RelayCommand(CancelOrder, CanCancelOrder);
            AddSupplierCommand = new RelayCommand(_ => AddSupplier());
            RefreshCommand = new RelayCommand(_ => LoadData());
            
            AddOrderItemCommand = new RelayCommand(AddOrderItem);
            RemoveOrderItemCommand = new RelayCommand(RemoveOrderItem);
            UploadInvoiceCommand = new RelayCommand(UploadInvoice, _ => SelectedOrder != null);

            LoadData();
        }

        #endregion

        #region Methods

        private void LoadData()
        {
            IsBusy = true;
            try
            {
                // Tedarikçiler
                Suppliers.Clear();
                foreach (var s in _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.CompanyName))
                {
                    Suppliers.Add(s);
                }

                // Toplam Borç
                TotalSupplierDebt = _context.Suppliers.Select(s => s.Balance).AsEnumerable().Sum();

                // Ürünler (PO için)
                Products.Clear();
                foreach (var p in _context.Products.OrderBy(p => p.ProductName))
                {
                    Products.Add(p);
                }

                LoadPurchaseOrders();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void LoadPurchaseOrders()
        {
            PurchaseOrders.Clear();

            var query = _context.PurchaseOrders
                .Include(po => po.Items)
                .OrderByDescending(po => po.OrderDate)
                .AsQueryable();

            if (SelectedSupplier != null)
            {
                query = query.Where(po => po.SupplierName == SelectedSupplier.CompanyName);
            }

            foreach (var po in query.Take(50))
            {
                PurchaseOrders.Add(po);
            }
        }

        /// <summary>
        /// Seçili sipariş için özet hesaplamaları yap
        /// </summary>
        private void CalculateOrderTotals()
        {
            if (SelectedOrder?.Items == null || !SelectedOrder.Items.Any())
            {
                OrderSubTotal = 0;
                OrderTaxAmount = 0;
                OrderDiscountAmount = 0;
                OrderGrandTotal = 0;
                return;
            }

            // Her bir kalemin finansal değerlerinin güncel olduğundan emin ol (ViewModel tarafında yapılan değişiklikler için)
            foreach (var item in SelectedOrder.Items)
            {
                RecalculateItemFinancials(item);
            }

            OrderSubTotal = SelectedOrder.Items.Sum(i => i.SubTotal);
            OrderDiscountAmount = SelectedOrder.Items.Sum(i => i.DiscountAmount);
            OrderTaxAmount = SelectedOrder.Items.Sum(i => i.TaxAmount);
            OrderGrandTotal = SelectedOrder.Items.Sum(i => i.LineTotal);
        }

        /// <summary>
        /// Bir kalemin finansal değerlerini hesapla
        /// </summary>
        public void RecalculateItemFinancials(PurchaseOrderItem item)
        {
            if (item == null) return;

            item.SubTotal = item.Quantity * item.UnitPrice;
            item.DiscountAmount = item.SubTotal * (item.DiscountRate / 100m);
            item.TaxAmount = (item.SubTotal - item.DiscountAmount) * (item.TaxRate / 100m);
            item.LineTotal = item.SubTotal - item.DiscountAmount + item.TaxAmount;
        }

        private bool CanCreateOrder()
        {
            return !string.IsNullOrWhiteSpace(NewSupplierName);
        }

        private void CreateOrder()
        {
            try
            {
                // PO numarası oluştur
                var today = DateTime.Today;
                var todayOrders = _context.PurchaseOrders.Count(po => po.OrderDate.Date == today);
                var poNumber = $"PO-{today:yyyyMMdd}-{(todayOrders + 1):D3}";

                var order = new PurchaseOrder
                {
                    PONumber = poNumber,
                    SupplierName = NewSupplierName,
                    SupplierContact = SelectedSupplier?.ContactInfo,
                    OrderDate = DateTime.Now,
                    ExpectedDate = ExpectedDate,
                    Status = PurchaseStatus.Pending,
                    Notes = Notes
                };

                _context.PurchaseOrders.Add(order);
                _context.SaveChanges();

                MessageBox.Show($"Satın alma emri oluşturuldu: {poNumber}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);

                // Formu temizle
                NewSupplierName = string.Empty;
                Notes = string.Empty;
                ExpectedDate = DateTime.Today.AddDays(7);

                LoadPurchaseOrders();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanReceiveGoods(object? parameter)
        {
            return parameter is PurchaseOrder po && 
                   po.Status != PurchaseStatus.Received && 
                   po.Status != PurchaseStatus.Cancelled &&
                   AuthService.CanApprovePurchase;
        }

        public ICommand UploadInvoiceCommand { get; }

        // ... in constructor:
        // UploadInvoiceCommand = new RelayCommand(UploadInvoice, _ => SelectedOrder != null);
        
        // ... (Please ensure this is placed in constructor in the actual implementation)

        private void UploadInvoice(object? parameter)
        {
             if (SelectedOrder == null) return;
             
             // Open File Dialog
             var openFileDialog = new Microsoft.Win32.OpenFileDialog
             {
                 Filter = "Fatura Dosyaları (*.pdf;*.jpg;*.png)|*.pdf;*.jpg;*.png|Tüm Dosyalar (*.*)|*.*",
                 Title = "Fatura Yükle"
             };

             if (openFileDialog.ShowDialog() == true)
             {
                 try
                 {
                     string sourceFile = openFileDialog.FileName;
                     string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                     string targetDir = System.IO.Path.Combine(appDataPath, "Kamatek", "Invoices");
                     
                     if (!System.IO.Directory.Exists(targetDir))
                         System.IO.Directory.CreateDirectory(targetDir);

                     string extension = System.IO.Path.GetExtension(sourceFile);
                     string targetFileName = $"INV_{SelectedOrder.PONumber}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
                     string targetPath = System.IO.Path.Combine(targetDir, targetFileName);
                     
                     System.IO.File.Copy(sourceFile, targetPath);
                     
                     SelectedOrder.InvoiceDocumentPath = targetPath;
                     _context.SaveChanges();
                     
                     MessageBox.Show("Fatura başarıyla yüklendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                     OnPropertyChanged(nameof(SelectedOrder));
                 }
                 catch (Exception ex)
                 {
                     MessageBox.Show($"Dosya yükleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                 }
             }
        }

        private void ReceiveGoods(object? parameter)
        {
            if (parameter is not PurchaseOrder order) return;

            // Fatura No Kontrolü
            if (string.IsNullOrWhiteSpace(order.SupplierReferenceNo))
            {
                 // Eğer fatura no girmeden işlem yapmaya çalışıyorsa uyar ama engelleme (opsiyonel olabilir)
                 if (MessageBox.Show("Tedarikçi Ref. No / Fatura No girilmemiş. Yine de devam etmek istiyor musunuz?", 
                     "Eksik Bilgi", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                     return;
            }

            var result = MessageBox.Show(
                $"'{order.PONumber}' siparişi teslim alındı olarak işaretlensin mi?\n\nBu işlem stok miktarlarını otomatik olarak güncelleyecektir.",
                "Teslim Alma Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // Sipariş durumunu güncelle
                order.Status = PurchaseStatus.Received;
                order.ReceivedDate = DateTime.Now;

                // Stok güncelle (Items varsa)
                var dbOrder = _context.PurchaseOrders
                    .Include(po => po.Items)
                    .FirstOrDefault(po => po.Id == order.Id);

                if (dbOrder?.Items != null)
                {
                    foreach (var item in dbOrder.Items)
                    {
                        // Ana depoya ekle (Warehouse ID 1)
                        var inventory = _context.Inventories
                            .FirstOrDefault(i => i.ProductId == item.ProductId && i.WarehouseId == 1);

                        if (inventory != null)
                        {
                            inventory.Quantity += item.Quantity;
                        }
                        else
                        {
                            _context.Inventories.Add(new Inventory
                            {
                                ProductId = item.ProductId ?? 0,
                                WarehouseId = 1,
                                Quantity = item.Quantity
                            });
                        }

                        // Stok hareketi kaydet
                        _context.StockTransactions.Add(new StockTransaction
                        {
                            Date = DateTime.Now,
                            ProductId = item.ProductId ?? 0,
                            TargetWarehouseId = 1,
                            Quantity = item.Quantity,
                            TransactionType = StockTransactionType.Purchase,
                            UnitCost = item.UnitPrice,
                            Description = $"Satın Alma - {order.PONumber} - Ref: {order.SupplierReferenceNo}",
                            ReferenceId = order.PONumber
                        });
                    }
                }

                // Tedarikçi borcunu güncelle
                var supplier = _context.Suppliers.FirstOrDefault(s => s.CompanyName == order.SupplierName);
                if (supplier != null)
                {
                    supplier.Balance += order.TotalAmount;
                }

                // Kasa kaydı (Gider)
                _context.CashTransactions.Add(new CashTransaction
                {
                    Date = DateTime.Now,
                    Amount = order.TotalAmount,
                    TransactionType = CashTransactionType.Expense,
                    Description = $"Satın Alma - {order.PONumber} ({order.SupplierReferenceNo})",
                    Category = "Malzeme Alımı",
                    ReferenceNumber = order.PONumber,
                    CreatedBy = AuthService.CurrentUser?.AdSoyad ?? "Sistem",
                    CreatedAt = DateTime.Now
                });

                _context.SaveChanges();
                transaction.Commit();

                MessageBox.Show("Malzeme teslim alındı, stok ve muhasebe kayıtları güncellendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanCancelOrder(object? parameter)
        {
            return parameter is PurchaseOrder po && 
                   po.Status == PurchaseStatus.Pending &&
                   AuthService.CanDeleteRecords;
        }

        private void CancelOrder(object? parameter)
        {
            if (parameter is not PurchaseOrder order) return;

            order.Status = PurchaseStatus.Cancelled;
            _context.SaveChanges();
            LoadPurchaseOrders();
        }

        private void AddSupplier()
        {
            // Basit bir input dialog yerine direkt ekleme
            if (string.IsNullOrWhiteSpace(NewSupplierName)) return;

            var existing = _context.Suppliers.FirstOrDefault(s => s.CompanyName == NewSupplierName);
            if (existing != null)
            {
                MessageBox.Show("Bu isimde tedarikçi zaten mevcut.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var supplier = new Supplier
            {
                CompanyName = NewSupplierName,
                IsActive = true
            };

            _context.Suppliers.Add(supplier);
            _context.SaveChanges();

            MessageBox.Show($"Tedarikçi eklendi: {NewSupplierName}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadData();
        }

        private void AddOrderItem(object? parameter)
        {
            if (SelectedOrder == null || SelectedProductToAdd == null) return;
            if (SelectedOrder.Status != PurchaseStatus.Pending)
            {
                MessageBox.Show("Sadece 'Bekleyen' durumundaki siparişlere ürün eklenebilir.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var item = new PurchaseOrderItem
                {
                    PurchaseOrderId = SelectedOrder.Id,
                    ProductId = SelectedProductToAdd.Id,
                    ProductName = SelectedProductToAdd.ProductName,
                    Quantity = QuantityToAdd,
                    UnitPrice = UnitPriceToAdd,
                    TaxRate = TaxRateToAdd,
                    DiscountRate = DiscountRateToAdd
                };

                // Finansal hesaplama
                RecalculateItemFinancials(item);

                _context.PurchaseOrderItems.Add(item);
                _context.SaveChanges();

                // UI listesine ekle
                if (SelectedOrder.Items == null) SelectedOrder.Items = new System.Collections.Generic.List<PurchaseOrderItem>();
                SelectedOrder.Items.Add(item);

                CalculateOrderTotals();

                // Reset inputs
                SelectedProductToAdd = null;
                QuantityToAdd = 1;
                UnitPriceToAdd = 0;
                DiscountRateToAdd = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveOrderItem(object? parameter)
        {
            if (parameter is not PurchaseOrderItem item) return;
            if (SelectedOrder?.Status != PurchaseStatus.Pending)
            {
                 MessageBox.Show("Sadece 'Bekleyen' durumundaki siparişlerden ürün silinebilir.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
            }

            if (MessageBox.Show("Bu kalemi silmek istediğinize emin misiniz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                _context.PurchaseOrderItems.Remove(item);
                _context.SaveChanges();

                SelectedOrder.Items.Remove(item);
                CalculateOrderTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
