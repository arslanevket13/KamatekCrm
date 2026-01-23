using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    public class StockTransferViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private Warehouse? _sourceWarehouse;
        private Warehouse? _targetWarehouse;
        private Product? _selectedProduct;
        private int _quantity;
        private string _statusMessage = string.Empty;
        private bool _isActionSuccessful;

        public ObservableCollection<Warehouse> Warehouses { get; set; }
        public ObservableCollection<Product> Products { get; set; }

        public Warehouse? SourceWarehouse
        {
            get => _sourceWarehouse;
            set
            {
                if (SetProperty(ref _sourceWarehouse, value))
                {
                    OnPropertyChanged(nameof(AvailableStock));
                }
            }
        }

        public Warehouse? TargetWarehouse
        {
            get => _targetWarehouse;
            set
            {
                if (SetProperty(ref _targetWarehouse, value))
                {
                }
            }
        }

        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value))
                {
                    OnPropertyChanged(nameof(AvailableStock));
                }
            }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                {
                }
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

        public int AvailableStock
        {
            get
            {
                if (SelectedProduct == null || SourceWarehouse == null) return 0;
                
                var inventory = _context.Inventories
                    .FirstOrDefault(i => i.ProductId == SelectedProduct.Id && i.WarehouseId == SourceWarehouse.Id);
                
                return inventory?.Quantity ?? 0;
            }
        }

        public ICommand TransferCommand { get; }

        public StockTransferViewModel()
        {
            _context = new AppDbContext();
            Warehouses = new ObservableCollection<Warehouse>(_context.Warehouses.Where(w => w.IsActive).ToList());
            Products = new ObservableCollection<Product>(_context.Products.ToList());
            
            TransferCommand = new RelayCommand(_ => ExecuteTransfer(), _ => CanExecuteTransfer());
        }

        private bool CanExecuteTransfer()
        {
            return SourceWarehouse != null && 
                   TargetWarehouse != null && 
                   SourceWarehouse.Id != TargetWarehouse.Id &&
                   SelectedProduct != null && 
                   Quantity > 0 && 
                   Quantity <= AvailableStock;
        }

        private void ExecuteTransfer()
        {
            if (SourceWarehouse == null || TargetWarehouse == null || SelectedProduct == null) return;

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // 1. Kaynak Depo Stok Azaltma
                var sourceInventory = _context.Inventories
                    .FirstOrDefault(i => i.ProductId == SelectedProduct.Id && i.WarehouseId == SourceWarehouse.Id);

                if (sourceInventory == null || sourceInventory.Quantity < Quantity)
                {
                    StatusMessage = "Kaynak depoda yeterli stok bulunamadı.";
                    IsActionSuccessful = false;
                    return;
                }

                sourceInventory.Quantity -= Quantity;

                // 2. Hedef Depo Stok Artırma
                var targetInventory = _context.Inventories
                    .FirstOrDefault(i => i.ProductId == SelectedProduct.Id && i.WarehouseId == TargetWarehouse.Id);

                if (targetInventory == null)
                {
                    targetInventory = new Inventory
                    {
                        ProductId = SelectedProduct.Id,
                        WarehouseId = TargetWarehouse.Id,
                        Quantity = Quantity
                    };
                    _context.Inventories.Add(targetInventory);
                }
                else
                {
                    targetInventory.Quantity += Quantity;
                }

                // 3. Stok Hareketi Kaydı (Ledger)
                var stockTransaction = new StockTransaction
                {
                    Date = System.DateTime.Now,
                    ProductId = SelectedProduct.Id,
                    SourceWarehouseId = SourceWarehouse.Id,
                    TargetWarehouseId = TargetWarehouse.Id,
                    Quantity = Quantity,
                    TransactionType = StockTransactionType.Transfer,
                    Description = $"{SourceWarehouse.Name} deposundan {TargetWarehouse.Name} deposuna transfer."
                };
                _context.StockTransactions.Add(stockTransaction);

                _context.SaveChanges();
                transaction.Commit();

                StatusMessage = "Transfer işlemi başarıyla tamamlandı.";
                IsActionSuccessful = true;
                
                // Formu temizle veya güncelle
                Quantity = 0;
                OnPropertyChanged(nameof(AvailableStock));
            }
            catch (System.Exception ex)
            {
                transaction.Rollback();
                StatusMessage = $"Hata oluştu: {ex.Message}";
                IsActionSuccessful = false;
            }
        }
    }
}
