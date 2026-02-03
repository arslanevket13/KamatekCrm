using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Models;
using KamatekCrm.Repositories;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Views;

namespace KamatekCrm.ViewModels
{
    public class PurchaseOrderViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public PurchaseOrderViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;

            LoadDataCommand = new RelayCommand(async _ => await LoadData());
            CreateOrderCommand = new RelayCommand(_ => CreateOrder());
            AddManualItemCommand = new RelayCommand(_ => AddManualItem());
            UploadPdfCommand = new RelayCommand(_ => UploadPdf());
            SaveOrderCommand = new RelayCommand(async _ => await SaveOrder(), _ => CurrentOrderItems.Any() && SelectedSupplier != null);

            // Init
            _ = LoadData();
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

        public ICommand LoadDataCommand { get; }
        public ICommand CreateOrderCommand { get; }
        public ICommand AddManualItemCommand { get; }
        public ICommand UploadPdfCommand { get; }
        public ICommand SaveOrderCommand { get; }

        #endregion

        #region Methods

        private async Task LoadData()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                // Load Products
                var products = await _unitOfWork.Context.Products.OrderBy(p => p.ProductName).ToListAsync();
                ProductList = new ObservableCollection<Product>(products);

                // Load Suppliers
                var suppliers = await _unitOfWork.Context.Suppliers.OrderBy(s => s.CompanyName).ToListAsync();
                Suppliers = new ObservableCollection<Supplier>(suppliers);

                // Load Orders (Recent 50?)
                var orders = await _unitOfWork.Context.PurchaseOrders
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

        private async Task SaveOrder()
        {
           await SaveOrderInternal(false);
        }

        // New Command for "Save & Receive" (Stock Update)
        public ICommand SaveAndReceiveCommand => new RelayCommand(async _ => await SaveOrderInternal(true), _ => CurrentOrderItems.Any() && SelectedSupplier != null);

        private async Task SaveOrderInternal(bool autoReceive)
        {
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
                var order = new PurchaseOrder
                {
                    SupplierId = SelectedSupplier.Id,
                    OrderDate = DateTime.Now,
                    Status = autoReceive ? Enums.PurchaseStatus.Completed : Enums.PurchaseStatus.Pending,
                    Items = new ObservableCollection<PurchaseOrderItem>(CurrentOrderItems)
                };

                _unitOfWork.Context.PurchaseOrders.Add(order);

                if (autoReceive)
                {
                    // Update Stocks
                    foreach (var item in CurrentOrderItems)
                    {
                        // Find product by ID if selected from manual, OR by Name if from PDF
                        Product? product = null;
                        
                        if (item.ProductId > 0)
                        {
                            product = await _unitOfWork.Context.Products.FindAsync(item.ProductId);
                        }
                        else
                        {
                             // Try find by name
                             product = await _unitOfWork.Context.Products.FirstOrDefaultAsync(p => p.ProductName.ToLower() == item.ProductName.ToLower());
                        }

                        if (product != null)
                        {
                            product.TotalStockQuantity += item.Quantity;
                            product.PurchasePrice = item.UnitPrice; // Update latest cost
                        }
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                
                string msg = autoReceive ? "Sipariş kaydedildi ve STOKLARA İŞLENDİ." : "Sipariş başarıyla oluşturuldu (Beklemede).";
                MessageBox.Show(msg, "Bilgi");
                
                // Refresh list and clear form
                CreateOrder();
                await LoadData();
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
