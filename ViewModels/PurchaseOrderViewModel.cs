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
        private readonly AttachmentService _attachmentService;

        #region Properties

        public ObservableCollection<Supplier> Suppliers { get; } = new();
        public ObservableCollection<PurchaseOrder> PurchaseOrders { get; } = new();
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<Attachment> OrderAttachments { get; } = new();

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
                    LoadAttachments();
                }
            }
        }

        private Attachment? _selectedAttachment;
        public Attachment? SelectedAttachment
        {
            get => _selectedAttachment;
            set => SetProperty(ref _selectedAttachment, value);
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
                    ManualProductSearchText = value.ProductName; // Sync text
                }
            }
        }

        private string _manualProductSearchText = string.Empty;
        public string ManualProductSearchText
        {
            get => _manualProductSearchText;
            set => SetProperty(ref _manualProductSearchText, value);
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
        public ICommand UploadInvoiceCommand { get; }

        public ICommand AddAttachmentCommand { get; }
        public ICommand RemoveAttachmentCommand { get; }
        public ICommand OpenAttachmentCommand { get; }

        #endregion

        #region Constructor

        private readonly InvoiceScannerService _invoiceScannerService;

        public ICommand ScanInvoiceCommand { get; }

        public PurchaseOrderViewModel()
        {
            _context = new AppDbContext();
            _attachmentService = new AttachmentService();
            _invoiceScannerService = new InvoiceScannerService();

            CreateOrderCommand = new RelayCommand(_ => CreateOrder(), _ => CanCreateOrder());
            ReceiveGoodsCommand = new RelayCommand(ReceiveGoods, CanReceiveGoods);
            CancelOrderCommand = new RelayCommand(CancelOrder, CanCancelOrder);
            AddSupplierCommand = new RelayCommand(_ => AddSupplier());
            RefreshCommand = new RelayCommand(_ => LoadData());
            
            AddOrderItemCommand = new RelayCommand(AddOrderItem);
            RemoveOrderItemCommand = new RelayCommand(RemoveOrderItem);
            UploadInvoiceCommand = new RelayCommand(UploadInvoice, _ => SelectedOrder != null);
            ScanInvoiceCommand = new RelayCommand(ScanInvoice, _ => SelectedOrder != null && SelectedOrder.Status == PurchaseStatus.Pending);

            AddAttachmentCommand = new RelayCommand(_ => AddAttachment(), _ => SelectedOrder != null && SelectedOrder.Id > 0);
            RemoveAttachmentCommand = new RelayCommand(RemoveAttachment, _ => SelectedAttachment != null);
            OpenAttachmentCommand = new RelayCommand(OpenAttachment);

            LoadData();
        }

        private async void ScanInvoice(object? parameter)
        {
            if (SelectedOrder == null) return;

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PDF Faturalar (*.pdf)|*.pdf",
                Title = "Taranacak Faturayı Seçin"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                IsBusy = true;
                try
                {
                    string filePath = openFileDialog.FileName;
                    
                    // Run OCR/Scanning in background
                    var items = await System.Threading.Tasks.Task.Run(() => 
                        _invoiceScannerService.ExtractItemsFromPdf(filePath, Products.ToList())
                    );

                    if (items.Any())
                    {
                        if (SelectedOrder.Items == null) SelectedOrder.Items = new System.Collections.Generic.List<PurchaseOrderItem>();
                        
                        int matchCount = items.Count(i => i.ProductId > 0);
                        int newCount = items.Count - matchCount;

                        var result = MessageBox.Show(
                            $"{items.Count} adet kalem ayıkladı.\n" +
                            $"{matchCount} tanesi sistemdeki ürünlerle eşleşti.\n" +
                            $"{newCount} tanesi eşleşmedi (manuel kontrol gerekir).\n\n" +
                            "Kalemler sipariş listesine eklensin mi?",
                            "Tarama Tamamlandı",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            foreach (var item in items)
                            {
                                item.PurchaseOrderId = SelectedOrder.Id;
                                
                                // Veritabanına ekle
                                _context.PurchaseOrderItems.Add(item);
                                
                                // UI listesine (bazen Items null gelebilir, view tarafı için refresh gerekebilir)
                                SelectedOrder.Items.Add(item);
                            }
                            await _context.SaveChangesAsync();
                            
                            CalculateOrderTotals();
                            MessageBox.Show("Kalemler eklendi. Lütfen 'Tamamlanmamış' veya eşleşmeyen ürünleri kontrol ediniz.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Faturadan anlamlı bir veri çıkarılamadı veya format desteklenmiyor.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Tarama hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
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
                 if (MessageBox.Show("Tedarikçi Ref. No / Fatura No girilmemiş. Yine de devam etmek istiyor musunuz?", 
                     "Eksik Bilgi", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                     return;
            }

            // Zaten stoka işlenmiş mi kontrol
            if (order.IsProcessedToStock)
            {
                MessageBox.Show("Bu sipariş zaten stoka işlenmiş.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Hedef depo kontrolü
            int targetWarehouseId = order.WarehouseId ?? 1; // Varsayılan: Ana Depo

            var result = MessageBox.Show(
                $"'{order.PONumber}' siparişi teslim alındı olarak işaretlensin mi?\n\nBu işlem stok miktarlarını güncelleyecek ve Ortalama Maliyet (WAC) hesaplayacaktır.\nHedef Depo ID: {targetWarehouseId}",
                "Teslim Alma Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // Sipariş durumunu güncelle
                order.Status = PurchaseStatus.Completed; // Doğrudan tamamlandıya çekiyoruz
                order.ReceivedDate = DateTime.Now;
                order.IsProcessedToStock = true;
                order.ProcessedDate = DateTime.Now;

                // Stok güncelle (Items varsa)
                var dbOrder = _context.PurchaseOrders
                    .Include(po => po.Items)
                    .FirstOrDefault(po => po.Id == order.Id);

                if (dbOrder?.Items != null)
                {
                    foreach (var item in dbOrder.Items)
                    {
                        // Seçili depoya ekle
                        var inventory = _context.Inventories
                            .FirstOrDefault(i => i.ProductId == item.ProductId && i.WarehouseId == targetWarehouseId);

                        if (inventory != null)
                        {
                            // Weighted Average Cost (WAC) Hesaplama
                            decimal oldTotalValue = inventory.Quantity * inventory.AverageCost;
                            decimal newTotalValue = item.Quantity * item.UnitPrice;
                            int newTotalQty = inventory.Quantity + item.Quantity;

                            if (newTotalQty > 0)
                            {
                                inventory.AverageCost = (oldTotalValue + newTotalValue) / newTotalQty;
                            }
                            
                            inventory.Quantity += item.Quantity;
                        }
                        else
                        {
                            _context.Inventories.Add(new Inventory
                            {
                                ProductId = item.ProductId ?? 0,
                                WarehouseId = targetWarehouseId,
                                Quantity = item.Quantity,
                                AverageCost = item.UnitPrice // İlk giriş maliyeti
                            });
                        }

                        // Stok hareketi kaydet
                        _context.StockTransactions.Add(new StockTransaction
                        {
                            Date = DateTime.Now,
                            ProductId = item.ProductId ?? 0,
                            TargetWarehouseId = targetWarehouseId,
                            Quantity = item.Quantity,
                            TransactionType = StockTransactionType.Purchase,
                            UnitCost = item.UnitPrice,
                            Description = $"Satın Alma (WAC) - {order.PONumber} - Ref: {order.SupplierReferenceNo}",
                            ReferenceId = order.PONumber,
                            UserId = AuthService.CurrentUser?.AdSoyad ?? "Sistem"
                        });
                    }
                }

                // Tedarikçi borcunu güncelle (SupplierId FK veya CompanyName ile)
                Supplier? supplier = null;
                if (order.SupplierId.HasValue)
                {
                    supplier = _context.Suppliers.Find(order.SupplierId.Value);
                }
                else
                {
                    supplier = _context.Suppliers.FirstOrDefault(s => s.CompanyName == order.SupplierName);
                }
                
                if (supplier != null)
                {
                    supplier.Balance += order.TotalAmount;
                }

                _context.SaveChanges();
                transaction.Commit();

                MessageBox.Show($"Sipariş teslim alındı.\nStok maliyetleri (WAC) yeniden hesaplandı.\nTedarikçi bakiyesi (Accounts Payable) güncellendi.", "İşlem Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                
                LoadData();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                MessageBox.Show($"Teslim alma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (SelectedOrder == null) return;

            // Manuel Giriş / Yeni Ürün Kontrolü
            if (SelectedProductToAdd == null && !string.IsNullOrWhiteSpace(ManualProductSearchText))
            {
                // Önce bu isimde ürün var mı bakalım (Combobox match etmemiş olabilir)
                var existing = Products.FirstOrDefault(p => p.ProductName.Equals(ManualProductSearchText, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    SelectedProductToAdd = existing;
                }
                else
                {
                    // Ürün bulunamadı, stok kartı açılsın mı?
                    var result = MessageBox.Show(
                        $"'{ManualProductSearchText}' adında bir ürün bulunamadı.\n\n" +
                        "Bu ürün için yeni stok kartı oluşturulsun mu?\n" +
                        "(Hayır derseniz işlem iptal edilir, listeye sadece stoklu ürünler eklenebilir.)", 
                        "Yeni Ürün", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        var newProductInfo = new Product { ProductName = ManualProductSearchText };
                        var addProductWindow = new Views.AddProductWindow(newProductInfo);
                        
                        if (addProductWindow.ShowDialog() == true)
                        {
                            // Listeyi yenile
                            Products.Clear();
                            foreach (var p in _context.Products.OrderBy(p => p.ProductName)) Products.Add(p);

                            // Yeni ürünü bul ve seç
                            var createdProduct = Products.FirstOrDefault(p => p.ProductName.Equals(ManualProductSearchText, StringComparison.OrdinalIgnoreCase));
                            if (createdProduct != null)
                            {
                                SelectedProductToAdd = createdProduct;
                            }
                            else
                            {
                                // İsim değişmiş olabilir, son ekleneni alabiliriz ama şimdilik kullanıcıya bırakalım
                                MessageBox.Show("Yeni oluşturulan ürün listede bulunamadı, lütfen manuel seçin.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                                return;
                            }
                        }
                        else
                        {
                             return; // Kullanıcı iptal etti
                        }
                    }
                    else
                    {
                        return; // İptal
                    }
                }
            }

            if (SelectedProductToAdd == null) return;
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

        private void LoadAttachments()
        {
            OrderAttachments.Clear();
            if (SelectedOrder == null || SelectedOrder.Id <= 0) return;

            try
            {
                var attachments = _attachmentService.GetAttachments(AttachmentEntityType.PurchaseOrder, SelectedOrder.Id);
                foreach (var att in attachments)
                    OrderAttachments.Add(att);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private void AddAttachment()
        {
            if (SelectedOrder == null || SelectedOrder.Id <= 0) return;

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Tüm Dosyalar (*.*)|*.*|PDF Dosyaları (*.pdf)|*.pdf|Resim Dosyaları (*.jpg;*.png)|*.jpg;*.png",
                Title = "Dosya Seçin",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    foreach (var file in openFileDialog.FileNames)
                    {
                        var attachment = _attachmentService.UploadFile(
                            AttachmentEntityType.PurchaseOrder,
                            SelectedOrder.Id,
                            file,
                            $"Sipariş eki: {System.IO.Path.GetFileName(file)}"
                        );
                        OrderAttachments.Add(attachment);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Dosya ekleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RemoveAttachment(object? parameter)
        {
            if (parameter is not Attachment attachment) return;

            if (MessageBox.Show($"'{attachment.FileName}' dosyasını silmek istediğinize emin misiniz?", "Onay", 
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                if (_attachmentService.DeleteAttachment(attachment.Id))
                {
                    OrderAttachments.Remove(attachment);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Silme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenAttachment(object? parameter)
        {
            if (parameter is not Attachment attachment) return;

            try
            {
                _attachmentService.OpenFile(attachment);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dosya açma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        #endregion
    }
}
