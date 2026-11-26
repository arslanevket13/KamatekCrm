using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Services;
using KamatekCrm.Settings;
using KamatekCrm.Shared.Helpers;
using KamatekCrm.Shared.Services;
using Serilog;

namespace KamatekCrm.ViewModels;

public sealed record BarcodeLabelTemplateOption(BarcodeLabelTemplate Template, string DisplayName);

public partial class BarcodeLabelPrintViewModel : ObservableObject
{
    private readonly IDialogService? _dialogService;
    private readonly IToastService? _toastService;

    public string ProductName { get; }
    public string Sku { get; }
    public string Barcode { get; }
    public decimal SalePrice { get; }
    public string Currency { get; }
    public string CompanyName { get; }

    public string EffectiveBarcode => !string.IsNullOrWhiteSpace(Barcode) ? Barcode : Sku;
    public string BarcodeFormatLabel => ProductCodeHelper.IsValidEan13(EffectiveBarcode) ? "EAN-13" : "Code 128";
    public string PricePreview => $"{(Currency.Equals("USD", StringComparison.OrdinalIgnoreCase) ? "$" : Currency.Equals("EUR", StringComparison.OrdinalIgnoreCase) ? "€" : "₺")}{SalePrice:N2}";

    public IReadOnlyList<BarcodeLabelTemplateOption> Templates { get; } =
    [
        new(BarcodeLabelTemplate.Thermal50x30, "Termal 50x30 mm"),
        new(BarcodeLabelTemplate.Thermal58x40, "Termal 58x40 mm"),
        new(BarcodeLabelTemplate.A4Grid24, "A4 24'lü Etiket (3x8)")
    ];

    private int _quantity = 1;
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(ValidationMessage));
                OnPropertyChanged(nameof(HasValidationError));
                PrintPdfCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private BarcodeLabelTemplateOption _selectedTemplate;
    public BarcodeLabelTemplateOption SelectedTemplate
    {
        get => _selectedTemplate;
        set => SetProperty(ref _selectedTemplate, value);
    }

    private bool _showPrice = true;
    public bool ShowPrice
    {
        get => _showPrice;
        set => SetProperty(ref _showPrice, value);
    }

    private bool _showCompanyName = true;
    public bool ShowCompanyName
    {
        get => _showCompanyName;
        set => SetProperty(ref _showCompanyName, value);
    }

    private bool _isGenerating;
    public bool IsGenerating
    {
        get => _isGenerating;
        private set
        {
            if (SetProperty(ref _isGenerating, value))
            {
                PrintPdfCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ValidationMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(EffectiveBarcode))
                return "Yazdırılacak SKU veya Barkod bulunamadı.";
            if (Quantity is < 1 or > 100)
                return "Etiket adedi 1 ile 100 arasında olmalıdır.";
            return string.Empty;
        }
    }

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    public event Action<bool>? RequestClose;

    public BarcodeLabelPrintViewModel(
        string productName,
        string sku,
        string barcode,
        decimal salePrice,
        string currency,
        IDialogService? dialogService,
        IToastService? toastService)
    {
        ProductName = string.IsNullOrWhiteSpace(productName) ? "İsimsiz Ürün" : productName.Trim();
        Sku = ProductCodeHelper.Normalize(sku);
        Barcode = ProductCodeHelper.Normalize(barcode);
        SalePrice = salePrice;
        Currency = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency.Trim();
        CompanyName = AppSettings.CompanyName;
        _dialogService = dialogService;
        _toastService = toastService;
        _selectedTemplate = Templates[0];
    }

    [RelayCommand]
    private void DecreaseQuantity()
    {
        if (Quantity > 1) Quantity--;
    }

    [RelayCommand]
    private void IncreaseQuantity()
    {
        if (Quantity < 100) Quantity++;
    }

    private bool CanPrintPdf() =>
        !IsGenerating &&
        Quantity is >= 1 and <= 100 &&
        !string.IsNullOrWhiteSpace(EffectiveBarcode);

    [RelayCommand(CanExecute = nameof(CanPrintPdf))]
    private async Task PrintPdfAsync()
    {
        if (!CanPrintPdf()) return;

        IsGenerating = true;
        try
        {
            var defaultName = BarcodeLabelPdfService.BuildDefaultFileName(Sku, ProductName, Quantity);
            string? filePath = null;
            if (_dialogService != null)
            {
                filePath = await _dialogService.ShowSaveFileDialogAsync(
                    "Barkod Etiketi PDF Kaydet",
                    "PDF Dosyası (*.pdf)|*.pdf",
                    defaultName);
            }

            if (string.IsNullOrWhiteSpace(filePath)) return;

            var request = new BarcodeLabelRequest(
                ProductName,
                Sku,
                Barcode,
                SalePrice,
                Currency,
                Quantity,
                SelectedTemplate.Template,
                ShowPrice,
                ShowCompanyName,
                CompanyName);

            await Task.Run(() => BarcodeLabelPdfService.GeneratePdf(request, filePath));

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });

            _toastService?.ShowSuccess("Etiket Hazır", $"{Quantity} adet barkod etiketi PDF olarak oluşturuldu.");
            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Barkod etiketi PDF oluşturulurken hata oluştu.");
            _toastService?.ShowError("Etiket Hatası", $"Etiket oluşturulamadı: {ex.Message}");
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}
