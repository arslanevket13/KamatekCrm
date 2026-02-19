using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Proje & Teklif penceresi ViewModel - Basit TabControl yapısı
    /// </summary>
    public class ProjectQuoteViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;

        #region Properties - Tab 1: Keşif & Yapı

        private Customer? _selectedCustomer;
        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set => SetProperty(ref _selectedCustomer, value);
        }

        public ObservableCollection<Customer> Customers { get; } = new();

        private string _projectName = string.Empty;
        public string ProjectName
        {
            get => _projectName;
            set => SetProperty(ref _projectName, value);
        }

        private int _blockCount = 1;
        public int BlockCount
        {
            get => _blockCount;
            set
            {
                if (SetProperty(ref _blockCount, Math.Max(1, value)))
                    OnPropertyChanged(nameof(TotalUnitCount));
            }
        }

        private int _floorCount = 1;
        public int FloorCount
        {
            get => _floorCount;
            set
            {
                if (SetProperty(ref _floorCount, Math.Max(1, value)))
                    OnPropertyChanged(nameof(TotalUnitCount));
            }
        }

        private int _flatsPerFloor = 1;
        public int FlatsPerFloor
        {
            get => _flatsPerFloor;
            set
            {
                if (SetProperty(ref _flatsPerFloor, Math.Max(1, value)))
                    OnPropertyChanged(nameof(TotalUnitCount));
            }
        }

        /// <summary>
        /// Otomatik hesaplanan toplam birim sayısı
        /// </summary>
        public int TotalUnitCount => BlockCount * FloorCount * FlatsPerFloor;

        #endregion

        #region Properties - Tab 2: Teklif Hazırla

        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<QuoteItem> QuoteItems { get; } = new();

        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set => SetProperty(ref _selectedProduct, value);
        }

        private QuoteItem? _selectedQuoteItem;
        public QuoteItem? SelectedQuoteItem
        {
            get => _selectedQuoteItem;
            set => SetProperty(ref _selectedQuoteItem, value);
        }

        private int _qtyPerUnit = 1;
        public int QtyPerUnit
        {
            get => _qtyPerUnit;
            set => SetProperty(ref _qtyPerUnit, Math.Max(1, value));
        }

        /// <summary>
        /// Toplam teklif tutarı
        /// </summary>
        public decimal TotalAmount => QuoteItems.Sum(q => q.TotalPrice);

        #endregion

        #region Commands

        public ICommand AddToQuoteCommand { get; }
        public ICommand RemoveFromQuoteCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        public ProjectQuoteViewModel(AppDbContext context)
        {
            _context = context;

            AddToQuoteCommand = new RelayCommand(_ => AddToQuote(), _ => SelectedProduct != null);
            RemoveFromQuoteCommand = new RelayCommand(_ => RemoveFromQuote(), _ => SelectedQuoteItem != null);
            SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
            CancelCommand = new RelayCommand(CloseWindow);

            LoadData();
        }

        private void LoadData()
        {
            // Müşterileri yükle
            var customers = _context.Customers.OrderBy(c => c.FullName).ToList();
            Customers.Clear();
            foreach (var c in customers)
                Customers.Add(c);

            // Ürünleri yükle
            var products = _context.Products.OrderBy(p => p.ProductName).ToList();
            Products.Clear();
            foreach (var p in products)
                Products.Add(p);
        }

        /// <summary>
        /// Seçili ürünü teklife ekle (Çarpan mantığı ile)
        /// </summary>
        private void AddToQuote()
        {
            if (SelectedProduct == null) return;

            // Toplam miktar = Birim başına adet × Toplam birim sayısı
            int totalQty = QtyPerUnit * TotalUnitCount;

            // Aynı ürün zaten varsa miktarını güncelle
            var existing = QuoteItems.FirstOrDefault(q => q.ProductId == SelectedProduct.Id);
            if (existing != null)
            {
                existing.QtyPerUnit += QtyPerUnit;
                existing.TotalQuantity += totalQty;
                existing.TotalPrice = existing.TotalQuantity * existing.UnitPrice;
            }
            else
            {
                QuoteItems.Add(new QuoteItem
                {
                    ProductId = SelectedProduct.Id,
                    ProductName = SelectedProduct.ProductName,
                    UnitPrice = SelectedProduct.SalePrice,
                    QtyPerUnit = QtyPerUnit,
                    TotalQuantity = totalQty,
                    TotalPrice = totalQty * SelectedProduct.SalePrice
                });
            }

            OnPropertyChanged(nameof(TotalAmount));
        }

        /// <summary>
        /// Seçili kalemi tekliften kaldır
        /// </summary>
        private void RemoveFromQuote()
        {
            if (SelectedQuoteItem == null) return;
            QuoteItems.Remove(SelectedQuoteItem);
            OnPropertyChanged(nameof(TotalAmount));
        }

        private bool CanSave()
        {
            return SelectedCustomer != null
                && !string.IsNullOrWhiteSpace(ProjectName)
                && QuoteItems.Any();
        }

        /// <summary>
        /// Projeyi veritabanına kaydet
        /// </summary>
        private void Save()
        {
            try
            {
                // Proje kodu oluştur
                var year = DateTime.Now.Year;
                var count = _context.ServiceProjects.Count(p => p.CreatedDate.Year == year) + 1;
                var projectCode = $"PRJ-{year}-{count:D3}";

                var project = new ServiceProject
                {
                    ProjectCode = projectCode,
                    Title = ProjectName,
                    CustomerId = SelectedCustomer!.Id,
                    CreatedDate = DateTime.Now,
                    TotalUnitCount = TotalUnitCount,
                    TotalBudget = TotalAmount,
                    SurveyNotes = $"Blok: {BlockCount}, Kat: {FloorCount}, Daire/Kat: {FlatsPerFloor}",
                    QuoteItemsJson = System.Text.Json.JsonSerializer.Serialize(QuoteItems.ToList())
                };

                _context.ServiceProjects.Add(project);
                _context.SaveChanges();

                MessageBox.Show(
                    $"Proje başarıyla kaydedildi!\n\nProje Kodu: {projectCode}\nToplam: {TotalAmount:C}",
                    "Başarılı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Pencereyi kapat
                CloseWindow(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Kayıt sırasında hata oluştu: {ex.Message}",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CloseWindow(object? parameter)
        {
            if (parameter is Window window)
                window.Close();
        }
    }

    /// <summary>
    /// Teklif kalemi (In-memory model)
    /// </summary>
    public class QuoteItem : ViewModelBase
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }

        private int _qtyPerUnit;
        public int QtyPerUnit
        {
            get => _qtyPerUnit;
            set => SetProperty(ref _qtyPerUnit, value);
        }

        private int _totalQuantity;
        public int TotalQuantity
        {
            get => _totalQuantity;
            set => SetProperty(ref _totalQuantity, value);
        }

        private decimal _totalPrice;
        public decimal TotalPrice
        {
            get => _totalPrice;
            set => SetProperty(ref _totalPrice, value);
        }
    }
}
