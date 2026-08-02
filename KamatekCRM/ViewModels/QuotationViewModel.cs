using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.Quotes;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Services;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.ViewModels;

public partial class QuotationViewModel : ViewModelBase
{
    private readonly IStandardQuoteReadService _readService;
    private readonly IStandardQuoteCommandService _commandService;
    private readonly IDialogService _dialogService;
    private readonly IToastService _toastService;
    private readonly PdfService _pdfService;
    private CancellationTokenSource? _searchCancellation;
    private Guid _saveOperationId = Guid.NewGuid();
    private int? _lastSavedQuoteId;
    private QuoteStatus _lastSavedStatus = QuoteStatus.Draft;
    private bool _isDirty = true;
    private int? _sourceServiceJobId;
    private StandardQuotePricingResult _pricing = EmptyPricing();

    public ObservableCollection<Customer> Customers { get; } = [];
    public ObservableCollection<StandardQuoteLineViewModel> QuoteLines { get; } = [];
    public ObservableCollection<Product> SearchResults { get; } = [];

    private Customer? _selectedCustomer;
    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value)) MarkDirty();
        }
    }

    private DateTime _quoteDate = DateTime.Today;
    public DateTime QuoteDate
    {
        get => _quoteDate;
        set
        {
            if (SetProperty(ref _quoteDate, value)) MarkDirty();
        }
    }

    private DateTime _validUntil = DateTime.Today.AddDays(15);
    public DateTime ValidUntil
    {
        get => _validUntil;
        set
        {
            if (SetProperty(ref _validUntil, value)) MarkDirty();
        }
    }

    private string _termsAndConditions = string.Empty;
    public string TermsAndConditions
    {
        get => _termsAndConditions;
        set
        {
            if (SetProperty(ref _termsAndConditions, value)) MarkDirty();
        }
    }

    private bool _isSidebarOpen;
    public bool IsSidebarOpen
    {
        get => _isSidebarOpen;
        set => SetProperty(ref _isSidebarOpen, value);
    }

    private string _searchTerm = string.Empty;
    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (!SetProperty(ref _searchTerm, value)) return;
            _ = DebouncedSearchAsync(value);
        }
    }

    private Product? _selectedProduct;
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
    }

    private int _newQuantity = 1;
    public int NewQuantity
    {
        get => _newQuantity;
        set => SetProperty(ref _newQuantity, Math.Max(1, value));
    }

    private decimal _newDiscount;
    public decimal NewDiscount
    {
        get => _newDiscount;
        set => SetProperty(ref _newDiscount, Math.Clamp(value, 0, 100));
    }

    public decimal SubTotal => _pricing.SubTotal;
    public decimal TotalDiscount => _pricing.TotalDiscount;
    public decimal SubTotalAfterDiscount => _pricing.NetTotal;
    public decimal TotalTax => _pricing.TotalTax;
    public decimal GrandTotal => _pricing.GrandTotal;
    public decimal TotalCost => _pricing.TotalCost;
    public decimal TotalProfit => _pricing.TotalProfit;
    public decimal ProfitMarginPercent => _pricing.ProfitMarginPercent;
    public string ProfitDisplay => TotalProfit >= 0
        ? $"📈 Kar: ₺{TotalProfit:N2} (%{ProfitMarginPercent:N1})"
        : $"📉 Zarar: ₺{Math.Abs(TotalProfit):N2} (%{ProfitMarginPercent:N1})";
    public System.Windows.Media.Brush ProfitColor => TotalProfit >= 0
        ? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#4CAF50")!
        : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#F44336")!;

    private string _quoteNumber = "Numara kayıtta oluşturulur";
    public string QuoteNumber
    {
        get => _quoteNumber;
        private set => SetProperty(ref _quoteNumber, value);
    }

    private string _selectedCurrency = "TRY";
    public string SelectedCurrency
    {
        get => _selectedCurrency;
        set
        {
            if (SetProperty(ref _selectedCurrency, "TRY")) MarkDirty();
        }
    }

    public string[] Currencies { get; } = ["TRY"];

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public QuotationViewModel(
        IStandardQuoteReadService readService,
        IStandardQuoteCommandService commandService,
        IDialogService dialogService,
        IToastService toastService,
        PdfService pdfService)
    {
        _readService = readService;
        _commandService = commandService;
        _dialogService = dialogService;
        _toastService = toastService;
        _pdfService = pdfService;
        if (!DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            _ = RefreshAsync();
    }

    public async Task SelectCustomerByIdAsync(int customerId)
    {
        if (Customers.Count == 0) await RefreshAsync();
        SelectedCustomer = Customers.FirstOrDefault(customer => customer.Id == customerId);
        if (SelectedCustomer is null)
            _toastService.ShowWarning("Teklif oluşturulacak müşteri bulunamadı.");
    }

    public async Task InitializeFromServiceJobAsync(int serviceJobId, int customerId)
    {
        _sourceServiceJobId = serviceJobId;
        await SelectCustomerByIdAsync(customerId);
    }

    private async Task RefreshAsync()
    {
        try
        {
            var selectedId = SelectedCustomer?.Id;
            var result = await _readService.GetWorkspaceAsync();
            if (result.IsFailure || result.Value is null)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            Customers.Clear();
            foreach (var customer in result.Value.Customers)
                Customers.Add(new Customer
                {
                    Id = customer.Id,
                    CustomerCode = customer.CustomerCode,
                    FullName = customer.FullName,
                    PhoneNumber = customer.PhoneNumber,
                    Email = customer.Email,
                    City = customer.City,
                    District = customer.District,
                    Neighborhood = customer.Neighborhood,
                    Street = customer.Street,
                    BuildingNo = customer.BuildingNo,
                    ApartmentNo = customer.ApartmentNo
                });
            if (selectedId.HasValue)
                SelectedCustomer = Customers.FirstOrDefault(customer => customer.Id == selectedId.Value);
        }
        catch (Exception exception)
        {
            _toastService.ShowError($"Teklif çalışma alanı yüklenemedi: {exception.Message}");
        }
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarOpen = !IsSidebarOpen;

    private async Task DebouncedSearchAsync(string searchText)
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        var cancellationToken = _searchCancellation.Token;
        try
        {
            await Task.Delay(300, cancellationToken);
            if (searchText.Trim().Length < 2)
            {
                SearchResults.Clear();
                return;
            }

            var result = await _readService.SearchProductsAsync(searchText, 20, cancellationToken);
            if (result.IsFailure || result.Value is null)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            SearchResults.Clear();
            foreach (var product in result.Value) SearchResults.Add(ToProduct(product));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _toastService.ShowError($"Ürün araması başarısız: {exception.Message}");
        }
    }

    [RelayCommand]
    private void AddProduct()
    {
        if (SelectedProduct is null) return;
        AddOrIncrement(SelectedProduct, NewQuantity, NewDiscount);
        IsSidebarOpen = false;
    }

    [RelayCommand]
    private void AddProductDirect(Product? product)
    {
        if (product is not null) AddOrIncrement(product, 1, 0);
    }

    private void AddOrIncrement(Product product, int quantity, decimal discountPercent)
    {
        var existing = QuoteLines.FirstOrDefault(line => line.ProductId == product.Id);
        if (existing is not null)
        {
            existing.Quantity += Math.Max(1, quantity);
            return;
        }

        QuoteLines.Add(new StandardQuoteLineViewModel(UpdateTotals)
        {
            ProductId = product.Id,
            ProductName = product.ProductName,
            ProductCode = product.SKU,
            Quantity = Math.Max(1, quantity),
            Unit = string.IsNullOrWhiteSpace(product.Unit) ? "Adet" : product.Unit,
            UnitPrice = product.SalePrice,
            PurchasePrice = product.PurchasePrice,
            DiscountPercent = Math.Clamp(discountPercent, 0, 100),
            TaxPercent = Math.Clamp(product.VatRate, 0, 100),
            CurrentStockQuantity = product.TotalStockQuantity
        });
        UpdateTotals();
    }

    [RelayCommand]
    private void IncreaseLineQuantity(StandardQuoteLineViewModel? line)
    {
        if (line is not null) line.Quantity++;
    }

    [RelayCommand]
    private void DecreaseLineQuantity(StandardQuoteLineViewModel? line)
    {
        if (line is null) return;
        if (line.Quantity > 1) line.Quantity--;
        else
        {
            QuoteLines.Remove(line);
            UpdateTotals();
        }
    }

    [RelayCommand]
    private void RemoveLine(StandardQuoteLineViewModel? line)
    {
        if (line is not null && QuoteLines.Remove(line)) UpdateTotals();
    }

    private void UpdateTotals()
    {
        var result = StandardQuotePricingPolicy.Calculate(QuoteLines.Select(line =>
            (line.ProductId, line.Quantity, line.UnitPrice, line.PurchasePrice,
                line.DiscountPercent, line.TaxPercent)).ToList());
        _pricing = result.Value ?? EmptyPricing();
        for (var index = 0; index < QuoteLines.Count && index < _pricing.Lines.Count; index++)
            QuoteLines[index].SetCalculatedTotal(_pricing.Lines[index].LineTotal);
        MarkDirty();
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
    private async Task SaveQuoteAsync() => await SaveAsync(QuoteStatus.Draft);

    [RelayCommand]
    private async Task SaveAndSendAsync()
    {
        if (!await _dialogService.ShowConfirmationAsync(
                "Teklif kaydedilecek ve uygulama dışında müşteriye iletildi olarak işaretlenecek. Devam edilsin mi?",
                "Kaydet ve Gönderildi İşaretle")) return;
        await SaveAsync(QuoteStatus.Sent);
    }

    private async Task SaveAsync(QuoteStatus status)
    {
        if (IsBusy) return;
        if (_lastSavedQuoteId.HasValue)
        {
            _toastService.ShowWarning(
                $"Bu teklif zaten {QuoteNumber} numarasıyla kaydedildi. Yeni teklif için ekranı yeniden açın.");
            return;
        }
        if (SelectedCustomer is null)
        {
            _toastService.ShowWarning("Lütfen müşteri seçin.");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _commandService.SaveAsync(new SaveStandardQuoteCommand(
                _saveOperationId,
                SelectedCustomer.Id,
                QuoteDate,
                ValidUntil,
                "TRY",
                TermsAndConditions,
                status,
                QuoteLines.Select(line => new StandardQuoteLineInput(
                    line.ProductId, line.Quantity, line.UnitPrice, line.DiscountPercent)).ToList(),
                _sourceServiceJobId));
            if (result.IsFailure || result.Value is null)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            _lastSavedQuoteId = result.Value.QuoteId;
            _lastSavedStatus = result.Value.Status;
            QuoteNumber = result.Value.QuoteNumber;
            _pricing = result.Value.Pricing;
            _isDirty = false;
            _saveOperationId = Guid.NewGuid();
            UpdateTotalsDisplayOnly();
            _toastService.ShowSuccess(result.Value.WasAlreadyApplied
                ? $"Teklif daha önce kaydedilmişti: {QuoteNumber}"
                : $"Teklif kaydedildi: {QuoteNumber}");
        }
        catch (Exception exception)
        {
            _toastService.ShowError($"Teklif kaydedilemedi: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportPdfAsync() => await ExportToPdfAsync();

    [RelayCommand]
    private async Task ExportToPdfAsync()
    {
        if (IsBusy || QuoteLines.Count == 0) return;
        var filePath = await _dialogService.ShowSaveFileDialogAsync(
            "Standart Teklifi Kaydet", "PDF Dosyası (*.pdf)|*.pdf",
            $"Teklif_{DateTime.Now:yyyyMMddHHmmss}.pdf");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        IsBusy = true;
        try
        {
            var quote = CreateDocument();
            await Task.Run(() => _pdfService.GenerateStandardQuote(quote, filePath));
            _toastService.ShowSuccess("Teklif PDF'i oluşturuldu.");
            if (await _dialogService.ShowConfirmationAsync(
                    "PDF oluşturuldu. Şimdi açmak ister misiniz?", "Teklif PDF'i Hazır"))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath)
                {
                    UseShellExecute = true
                });
        }
        catch (Exception exception)
        {
            _toastService.ShowError($"PDF oluşturulamadı: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Quote CreateDocument()
    {
        var quote = new Quote
        {
            Id = _lastSavedQuoteId ?? 0,
            QuoteNumber = _lastSavedQuoteId.HasValue && !_isDirty ? QuoteNumber : "ÖNİZLEME",
            CustomerId = SelectedCustomer?.Id,
            Customer = SelectedCustomer,
            Date = QuoteDate,
            ValidUntil = ValidUntil,
            Status = _lastSavedQuoteId.HasValue ? _lastSavedStatus : QuoteStatus.Draft,
            Currency = "TRY",
            SubTotal = SubTotal,
            TotalDiscount = TotalDiscount,
            TotalTax = TotalTax,
            GrandTotal = GrandTotal,
            TermsAndConditions = TermsAndConditions
        };
        foreach (var source in QuoteLines)
            quote.Lines.Add(new QuoteLine
            {
                ProductId = source.ProductId,
                ProductName = source.ProductName,
                ProductCode = source.ProductCode,
                Quantity = source.Quantity,
                Unit = source.Unit,
                PurchasePrice = source.PurchasePrice,
                UnitPrice = source.UnitPrice,
                DiscountPercent = source.DiscountPercent,
                TaxPercent = source.TaxPercent,
                LineTotal = source.LineTotal
            });
        return quote;
    }

    private void MarkDirty() => _isDirty = true;

    private void UpdateTotalsDisplayOnly()
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

    private static Product ToProduct(StandardQuoteProductDto source) => new()
    {
        Id = source.Id,
        ProductName = source.ProductName,
        SKU = source.Sku,
        Unit = source.Unit,
        SalePrice = source.SalePrice,
        PurchasePrice = source.PurchasePrice,
        VatRate = (int)source.TaxPercent,
        TotalStockQuantity = source.StockQuantity,
        ImagePath = source.ImagePath
    };

    private static StandardQuotePricingResult EmptyPricing() =>
        new(0, 0, 0, 0, 0, 0, 0, 0, []);
}

public sealed class StandardQuoteLineViewModel : ViewModelBase
{
    private readonly Action _changed;
    private int _quantity = 1;
    private decimal _unitPrice;
    private decimal _discountPercent;
    private decimal _lineTotal;

    public StandardQuoteLineViewModel(Action changed) => _changed = changed;

    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string ProductCode { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal PurchasePrice { get; init; }
    public decimal TaxPercent { get; init; }
    public int CurrentStockQuantity { get; init; }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, Math.Max(1, value))) _changed();
        }
    }

    public decimal UnitPrice
    {
        get => _unitPrice;
        set
        {
            if (SetProperty(ref _unitPrice, Math.Max(0, value))) _changed();
        }
    }

    public decimal DiscountPercent
    {
        get => _discountPercent;
        set
        {
            if (SetProperty(ref _discountPercent, Math.Clamp(value, 0, 100))) _changed();
        }
    }

    public decimal LineTotal
    {
        get => _lineTotal;
        private set => SetProperty(ref _lineTotal, value);
    }

    public void SetCalculatedTotal(decimal value) => LineTotal = value;
}
