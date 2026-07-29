using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    public partial class QuotationViewModel : ViewModelBase
    {
        private AppDbContext? _context;
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext> _dbContextFactory;

        // Properties
        public ObservableCollection<Customer> Customers { get; set; } = new ObservableCollection<Customer>();
        public ObservableCollection<QuoteLine> QuoteLines { get; set; } = new ObservableCollection<QuoteLine>();
        public ObservableCollection<Product> SearchResults { get; set; } = new ObservableCollection<Product>();

        private Customer? _selectedCustomer;
        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                _selectedCustomer = value;
                OnPropertyChanged();
            }
        }

        private DateTime _quoteDate = DateTime.Now;
        public DateTime QuoteDate
        {
            get => _quoteDate;
            set
            {
                _quoteDate = value;
                OnPropertyChanged();
            }
        }

        private DateTime _validUntil = DateTime.Now.AddDays(15);
        public DateTime ValidUntil
        {
            get => _validUntil;
            set
            {
                _validUntil = value;
                OnPropertyChanged();
            }
        }

        private string _termsAndConditions = string.Empty;
        public string TermsAndConditions
        {
            get => _termsAndConditions;
            set
            {
                _termsAndConditions = value;
                OnPropertyChanged();
            }
        }

        private bool _isSidebarOpen;
        public bool IsSidebarOpen
        {
            get => _isSidebarOpen;
            set
            {
                _isSidebarOpen = value;
                OnPropertyChanged();
            }
        }

        // Sidebar Add Product Properties
        private string _searchTerm = string.Empty;
        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                _searchTerm = value;
                OnPropertyChanged();
                DebounceSearch();
            }
        }

        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                _selectedProduct = value;
                OnPropertyChanged();
            }
        }

        private int _newQuantity = 1;
        public int NewQuantity
        {
            get => _newQuantity;
            set
            {
                _newQuantity = value;
                OnPropertyChanged();
            }
        }

        private decimal _newDiscount;
        public decimal NewDiscount
        {
            get => _newDiscount;
            set
            {
                _newDiscount = value;
                OnPropertyChanged();
            }
        }

        // Totals & Profit Calculations
        public decimal SubTotal => QuoteLines.Sum(l => l.Quantity * l.UnitPrice);
        public decimal TotalDiscount => QuoteLines.Sum(l => (l.Quantity * l.UnitPrice) * (l.DiscountPercent / 100m));
        public decimal SubTotalAfterDiscount => SubTotal - TotalDiscount;
        public decimal TotalTax => QuoteLines.Sum(l => ((l.Quantity * l.UnitPrice) - ((l.Quantity * l.UnitPrice) * (l.DiscountPercent / 100m))) * (l.TaxPercent / 100m));
        public decimal GrandTotal => SubTotalAfterDiscount + TotalTax;
        public decimal TotalCost => QuoteLines.Sum(l => l.Quantity * l.PurchasePrice);
        public decimal TotalProfit => SubTotalAfterDiscount - TotalCost;

        public decimal ProfitMarginPercent => TotalCost > 0 ? Math.Round((TotalProfit / TotalCost) * 100, 1) : 0;

        public string ProfitDisplay => TotalProfit >= 0
            ? $"📈 Kar: ₺{TotalProfit:N2} (%{ProfitMarginPercent:F1})"
            : $"📉 Zarar: ₺{Math.Abs(TotalProfit):N2} (%{ProfitMarginPercent:F1})";

        public System.Windows.Media.Brush ProfitColor => TotalProfit >= 0
            ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#4CAF50")!
            : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#F44336")!;

        private string _quoteNumber = $"TKLF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
        public string QuoteNumber
        {
            get => _quoteNumber;
            set => SetProperty(ref _quoteNumber, value);
        }

        private string _selectedCurrency = "TRY";
        public string SelectedCurrency
        {
            get => _selectedCurrency;
            set => SetProperty(ref _selectedCurrency, value);
        }

        public string[] Currencies => new[] { "TRY", "USD", "EUR" };

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        // Commands

        public QuotationViewModel(Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
            // Designer time initialization avoidance
            if (DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
                return;

            _context = _dbContextFactory.CreateDbContext();

            QuoteLines.CollectionChanged += (s, e) => UpdateTotals();
            _ = Refresh();
        }

        public async Task SelectCustomerByIdAsync(int customerId)
        {
            if (!Customers.Any())
            {
                await Refresh();
            }

            var customer = Customers.FirstOrDefault(c => c.Id == customerId);
            if (customer != null)
            {
                SelectedCustomer = customer;
            }
            else
            {
                using var ctx = await _dbContextFactory.CreateDbContextAsync();
                var dbCustomer = await ctx.Customers.FindAsync(customerId);
                if (dbCustomer != null)
                {
                    Customers.Add(dbCustomer);
                    SelectedCustomer = dbCustomer;
                }
            }
        }

        private async Task Refresh()
        {
            try
            {
                Customers.Clear();
                var customers = await _context.Customers.ToListAsync();
                foreach (var c in customers) Customers.Add(c);
            }
            catch { /* handle error */ }
        }

        [RelayCommand]
        private void ToggleSidebar()
        {
            IsSidebarOpen = !IsSidebarOpen;
        }

        [RelayCommand]
        private async Task SaveQuoteAsync()
        {
            if (SelectedCustomer == null)
            {
                System.Windows.MessageBox.Show("Lütfen önce bir müşteri seçin.", "Uyarı", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            await SaveDraftAsync(QuoteStatus.Sent);
            System.Windows.MessageBox.Show("Teklif başarıyla kaydedildi.", "Başarılı", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task ExportPdfAsync()
        {
            await ExportToPdfAsync();
        }

        // --- Debounce Logic for Search ---
        private System.Timers.Timer? _debounceTimer;
        private void DebounceSearch()
        {
            if (_debounceTimer == null)
            {
                _debounceTimer = new System.Timers.Timer(300);
                _debounceTimer.AutoReset = false;
                _debounceTimer.Elapsed += async (s, e) => await PerformSearchAsync();
            }
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private async Task PerformSearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchTerm)) return;
            var term = SearchTerm.ToLower();

            try
            {
                var products = await _context.Products
                    .Where(p => p.ProductName.ToLower().Contains(term) || p.SKU.ToLower().Contains(term))
                    .Take(20)
                    .ToListAsync();

                // Marshal back to UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SearchResults.Clear();
                    foreach (var p in products) SearchResults.Add(p);
                });
            }
            catch { /* Handle error */ }
        }

        [RelayCommand]
        private void AddProduct()
        {
            if (SelectedProduct == null) return;

            var existingLine = QuoteLines.FirstOrDefault(l => l.ProductId == SelectedProduct.Id);
            if (existingLine != null)
            {
                existingLine.Quantity += NewQuantity;
                existingLine.LineTotal = CalculateLineTotal(existingLine);
            }
            else
            {
                var line = new QuoteLine
                {
                    ProductId = SelectedProduct.Id,
                    Product = SelectedProduct,
                    ProductName = SelectedProduct.ProductName,
                    ProductCode = SelectedProduct.SKU,
                    Quantity = NewQuantity,
                    Unit = SelectedProduct.Unit ?? "Adet",
                    UnitPrice = SelectedProduct.SalePrice,
                    PurchasePrice = SelectedProduct.PurchasePrice,
                    DiscountPercent = NewDiscount,
                    TaxPercent = SelectedProduct.VatRate,
                    CurrentStockQuantity = SelectedProduct.TotalStockQuantity
                };
                line.LineTotal = CalculateLineTotal(line);
                QuoteLines.Add(line);
            }

            UpdateTotals();
            IsSidebarOpen = false; // Close sidebar after adding
        }

        [RelayCommand]
        private void AddProductDirect(Product? product)
        {
            if (product == null) return;

            var existingLine = QuoteLines.FirstOrDefault(l => l.ProductId == product.Id);
            if (existingLine != null)
            {
                existingLine.Quantity += 1;
                existingLine.LineTotal = CalculateLineTotal(existingLine);
            }
            else
            {
                var line = new QuoteLine
                {
                    ProductId = product.Id,
                    Product = product,
                    ProductName = product.ProductName,
                    ProductCode = product.SKU,
                    Quantity = 1,
                    Unit = product.Unit ?? "Adet",
                    UnitPrice = product.SalePrice,
                    PurchasePrice = product.PurchasePrice,
                    DiscountPercent = 0,
                    TaxPercent = product.VatRate,
                    CurrentStockQuantity = product.TotalStockQuantity
                };
                line.LineTotal = CalculateLineTotal(line);
                QuoteLines.Add(line);
            }

            UpdateTotals();
        }

        [RelayCommand]
        private void IncreaseLineQuantity(QuoteLine? line)
        {
            if (line == null) return;
            line.Quantity += 1;
            line.LineTotal = CalculateLineTotal(line);
            UpdateTotals();
        }

        [RelayCommand]
        private void DecreaseLineQuantity(QuoteLine? line)
        {
            if (line == null) return;
            if (line.Quantity > 1)
            {
                line.Quantity -= 1;
                line.LineTotal = CalculateLineTotal(line);
                UpdateTotals();
            }
            else
            {
                QuoteLines.Remove(line);
                UpdateTotals();
            }
        }

        [RelayCommand]
        private void RemoveLine(QuoteLine line)
        {
            if (line != null)
            {
                QuoteLines.Remove(line);
                UpdateTotals();
            }
        }

        private decimal CalculateLineTotal(QuoteLine line)
        {
            var total = line.Quantity * line.UnitPrice;
            var discount = total * (line.DiscountPercent / 100m);
            var totalAfterDiscount = total - discount;
            var tax = totalAfterDiscount * (line.TaxPercent / 100m);
            return totalAfterDiscount + tax;
        }

        private void UpdateTotals()
        {
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(TotalDiscount));
            OnPropertyChanged(nameof(SubTotalAfterDiscount));
            OnPropertyChanged(nameof(TotalTax));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(TotalCost));
            OnPropertyChanged(nameof(TotalProfit));
            OnPropertyChanged(nameof(ProfitMarginPercent));
            OnPropertyChanged(nameof(ProfitDisplay));
            OnPropertyChanged(nameof(ProfitColor));
        }

        [RelayCommand]
        private async Task ExportToPdfAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF Dosyası (*.pdf)|*.pdf",
                    FileName = $"Teklif_{DateTime.Now:yyyyMMddHHmmss}.pdf",
                    Title = "PDF Olarak Kaydet"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var filePath = saveFileDialog.FileName;

                    var quote = new Quote
                    {
                        QuoteNumber = $"TKLF-{DateTime.UtcNow:yyyyMMddHHmmss}",
                        CustomerId = SelectedCustomer?.Id,
                        Customer = SelectedCustomer,
                        Date = QuoteDate.ToUniversalTime(),
                        ValidUntil = ValidUntil.ToUniversalTime(),
                        SubTotal = SubTotal,
                        TotalDiscount = TotalDiscount,
                        TotalTax = TotalTax,
                        GrandTotal = GrandTotal,
                        TermsAndConditions = TermsAndConditions,
                        Lines = QuoteLines.ToList()
                    };

                    await Task.Run(() =>
                    {
                        var pdfService = new KamatekCrm.Services.PdfService();
                        pdfService.GenerateStandardQuote(quote, filePath);
                    });

                    System.Windows.MessageBox.Show("PDF başarıyla oluşturuldu.", "Başarılı", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"PDF oluşturulurken hata oluştu:\n{ex.Message}", "Hata", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SaveDraftAsync(QuoteStatus status)
        {
            if (SelectedCustomer == null) return;
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var quote = new Quote
                {
                    QuoteNumber = $"TKLF-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    CustomerId = SelectedCustomer.Id,
                    Date = QuoteDate.ToUniversalTime(),
                    ValidUntil = ValidUntil.ToUniversalTime(),
                    Status = status,
                    SubTotal = SubTotal,
                    TotalDiscount = TotalDiscount,
                    TotalTax = TotalTax,
                    GrandTotal = GrandTotal,
                    TermsAndConditions = TermsAndConditions,
                    Lines = QuoteLines.ToList()
                };

                // Clear navigation properties to prevent EF from tracking them as new entities
                foreach (var line in quote.Lines)
                {
                    line.Product = null;
                }

                _context.Quotes.Add(quote);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                // Show success (Toast message usually)
                // Clear form or close window
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Serilog.Log.Error(ex, "Teklif kaydedilirken hata oluştu");
            }
        }

        [RelayCommand]
        private async Task SaveAndSendAsync()
        {
            await SaveDraftAsync(QuoteStatus.Sent);
        }
    }
}

