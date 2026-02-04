using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
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

            // Domain Service'e delege et
            var inventoryService = new KamatekCrm.Services.Domain.InventoryDomainService();
            var request = new KamatekCrm.Services.Domain.TransferRequest
            {
                ProductId = SelectedProduct.Id,
                SourceWarehouseId = SourceWarehouse.Id,
                TargetWarehouseId = TargetWarehouse.Id,
                Quantity = Quantity,
                Description = $"{SourceWarehouse.Name} deposundan {TargetWarehouse.Name} deposuna transfer."
            };

            var result = inventoryService.TransferStock(request);

            if (result.Success)
            {
                StatusMessage = "Transfer işlemi başarıyla tamamlandı.";
                IsActionSuccessful = true;
                
                // Formu temizle
                Quantity = 0;
                OnPropertyChanged(nameof(AvailableStock));
            }
            else
            {
                StatusMessage = result.ErrorMessage;
                IsActionSuccessful = false;
            }
        }
    }
}
