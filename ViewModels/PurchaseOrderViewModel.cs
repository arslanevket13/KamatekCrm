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
            set => SetProperty(ref _selectedOrder, value);
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

        #endregion

        #region Commands

        public ICommand CreateOrderCommand { get; }
        public ICommand ReceiveGoodsCommand { get; }
        public ICommand CancelOrderCommand { get; }
        public ICommand AddSupplierCommand { get; }
        public ICommand RefreshCommand { get; }

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
                TotalSupplierDebt = _context.Suppliers.Sum(s => s.Balance);

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

        private void ReceiveGoods(object? parameter)
        {
            if (parameter is not PurchaseOrder order) return;

            var result = MessageBox.Show(
                $"'{order.PONumber}' siparişi teslim alındı olarak işaretlensin mi?\n\nBu işlem stok miktarlarını otomatik olarak güncelleyecektir.",
                "Teslim Alma Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

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
                            Description = $"Satın Alma - {order.PONumber}",
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
                    Description = $"Satın Alma - {order.PONumber}",
                    Category = "Malzeme Alımı",
                    ReferenceNumber = order.PONumber,
                    CreatedBy = AuthService.CurrentUser?.AdSoyad ?? "Sistem",
                    CreatedAt = DateTime.Now
                });

                _context.SaveChanges();

                MessageBox.Show("Malzeme teslim alındı ve stok güncellendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
            }
            catch (Exception ex)
            {
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

        #endregion
    }
}
