using KamatekCrm.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Services.Domain;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    public partial class StockTransferViewModel : ViewModelBase
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IInventoryDomainService _inventoryDomainService;
        private Warehouse? _sourceWarehouse;
        private Warehouse? _targetWarehouse;
        private Product? _selectedProduct;
        private int _quantity;
        private int _availableStock;
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
                    RefreshAvailableStockAsync().SafeFireAndForget("StockTransferViewModel.RefreshAvailableStockAsync");
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
                    TransferCommand?.NotifyCanExecuteChanged();
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
                    RefreshAvailableStockAsync().SafeFireAndForget("StockTransferViewModel.RefreshAvailableStockAsync");
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
                    TransferCommand?.NotifyCanExecuteChanged();
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
            get => _availableStock;
            private set
            {
                if (SetProperty(ref _availableStock, value))
                {
                    TransferCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        private async Task RefreshAvailableStockAsync()
        {
            var product = SelectedProduct;
            var warehouse = SourceWarehouse;
            if (product == null || warehouse == null)
            {
                AvailableStock = 0;
                return;
            }

            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var qty = await context.Inventories.AsNoTracking()
                    .Where(i => i.ProductId == product.Id && i.WarehouseId == warehouse.Id)
                    .Select(i => (int?)i.Quantity)
                    .FirstOrDefaultAsync();

                if (SelectedProduct?.Id == product.Id && SourceWarehouse?.Id == warehouse.Id)
                {
                    AvailableStock = qty ?? 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StockTransfer.AvailableStock] {ex.Message}");
                AvailableStock = 0;
            }
        }
        
        public StockTransferViewModel(IInventoryDomainService inventoryDomainService, IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _inventoryDomainService = inventoryDomainService;
            _dbContextFactory = dbContextFactory;
            
            using var context = _dbContextFactory.CreateDbContext();
            Warehouses = new ObservableCollection<Warehouse>(context.Warehouses.Where(w => w.IsActive).ToList());
            Products = new ObservableCollection<Product>(context.Products.ToList());
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

        [RelayCommand(CanExecute = nameof(CanExecuteTransfer))]
        private void Transfer()
        {
            ExecuteTransfer();
        }

        [RelayCommand]
        private void Close(Window? window)
        {
            window?.Close();
        }

        private void ExecuteTransfer()
        {
            if (SourceWarehouse == null || TargetWarehouse == null || SelectedProduct == null) return;
            
            if (SourceWarehouse.Id == TargetWarehouse.Id)
            {
                StatusMessage = "Kaynak ve hedef depo aynı olamaz.";
                IsActionSuccessful = false;
                return;
            }

            if (Quantity <= 0)
            {
                StatusMessage = "Transfer miktarı sıfırdan büyük olmalıdır.";
                IsActionSuccessful = false;
                return;
            }

            int currentStock = AvailableStock;
            if (Quantity > currentStock)
            {
                StatusMessage = $"Yetersiz stok! Kaynak depoda sadece {currentStock} adet mevcut.";
                IsActionSuccessful = false;
                return;
            }

            var request = new KamatekCrm.Services.Domain.TransferRequest
            {
                ProductId = SelectedProduct.Id,
                SourceWarehouseId = SourceWarehouse.Id,
                TargetWarehouseId = TargetWarehouse.Id,
                Quantity = Quantity,
                Description = $"{SourceWarehouse.Name} deposundan {TargetWarehouse.Name} deposuna transfer."
            };

            var result = _inventoryDomainService.TransferStock(request);

            if (result.Success)
            {
                StatusMessage = "Transfer işlemi başarıyla tamamlandı.";
                IsActionSuccessful = true;
                
                // Formu temizle ve önbellekteki mevcut stoğu asenkron güncelle
                Quantity = 0;
                RefreshAvailableStockAsync().SafeFireAndForget("StockTransferViewModel.RefreshAvailableStockAsync");
            }
            else
            {
                StatusMessage = result.ErrorMessage;
                IsActionSuccessful = false;
            }
        }
    }
}
