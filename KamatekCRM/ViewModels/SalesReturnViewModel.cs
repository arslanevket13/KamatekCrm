using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.Transactions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels;

public sealed class SalesReturnLineSelection : ObservableObject
{
    public required ReturnableSaleLineDto Source { get; init; }
    public required IReadOnlyList<WarehouseLookupDto> Warehouses { get; init; }
    private int _quantity;
    private ReturnDisposition _disposition = ReturnDisposition.Restock;
    private WarehouseLookupDto? _warehouse;

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, Math.Clamp(value, 0, Source.RemainingQuantity)))
                OnPropertyChanged(nameof(EstimatedAmount));
        }
    }
    public ReturnDisposition Disposition { get => _disposition; set => SetProperty(ref _disposition, value); }
    public WarehouseLookupDto? Warehouse { get => _warehouse; set => SetProperty(ref _warehouse, value); }
    public decimal EstimatedAmount => Source.RemainingQuantity == 0 ? 0 : Math.Round(Source.RemainingAmount * Quantity / Source.RemainingQuantity, 2, MidpointRounding.AwayFromZero);
}

public sealed class ReturnPaymentEntryViewModel : ObservableObject
{
    private PaymentMethod _paymentMethod = PaymentMethod.Cash;
    private decimal _amount;
    private string _reference = string.Empty;
    public PaymentMethod PaymentMethod { get => _paymentMethod; set => SetProperty(ref _paymentMethod, value); }
    public decimal Amount { get => _amount; set => SetProperty(ref _amount, Math.Max(0, value)); }
    public string Reference { get => _reference; set => SetProperty(ref _reference, value); }
}

public partial class SalesReturnViewModel : ViewModelBase
{
    private readonly IRetailTransactionService _transactions;
    private readonly ITransactionReadService _readService;
    private readonly IThermalReceiptPrintService _printer;
    private readonly IToastService _toast;
    private readonly IDialogService _dialogs;

    public ObservableCollection<SaleSummaryDto> Sales { get; } = new();
    public ObservableCollection<SalesReturnLineSelection> Lines { get; } = new();
    public ObservableCollection<ReturnPaymentEntryViewModel> Refunds { get; } = new();
    public IReadOnlyList<ReturnDisposition> Dispositions { get; } = Enum.GetValues<ReturnDisposition>();
    public IReadOnlyList<PaymentMethod> PaymentMethods { get; } =
        [PaymentMethod.Cash, PaymentMethod.CreditCard, PaymentMethod.BankTransfer, PaymentMethod.OnAccount];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private SaleSummaryDto? _selectedSale;
    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _summary = "İade edilecek satışı seçin.";
    [ObservableProperty] private bool _isBusy;
    private ReturnableSaleDto? _returnable;
    private Guid _returnAttemptId = Guid.NewGuid();

    public SalesReturnViewModel(
        IRetailTransactionService transactions,
        ITransactionReadService readService,
        IThermalReceiptPrintService printer,
        IToastService toast,
        IDialogService dialogs)
    {
        _transactions = transactions;
        _readService = readService;
        _printer = printer;
        _toast = toast;
        _dialogs = dialogs;
        Refunds.Add(CreateRefundEntry());
        _ = SearchAsync();
    }

    partial void OnSelectedSaleChanged(SaleSummaryDto? value)
    {
        if (value is not null) _ = LoadSelectedSaleAsync(value.SalesOrderId);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _transactions.SearchSalesAsync(new SaleSearchQuery(SearchText, DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow.AddDays(1)));
            if (result.IsFailure || result.Value is null) { _toast.ShowError(result.Error); return; }
            Sales.Clear();
            foreach (var item in result.Value) Sales.Add(item);
        }
        finally { IsBusy = false; }
    }

    private async Task LoadSelectedSaleAsync(int salesOrderId)
    {
        IsBusy = true;
        try
        {
            var result = await _transactions.GetReturnableSaleAsync(salesOrderId);
            if (result.IsFailure || result.Value is null) { _toast.ShowError(result.Error); return; }
            _returnable = result.Value;
            var warehouseResult = await _readService.GetActiveWarehousesAsync(includeQuarantine: false);
            if (warehouseResult.IsFailure || warehouseResult.Value is null) { _toast.ShowError(warehouseResult.Error); return; }
            var warehouses = warehouseResult.Value;
            Lines.Clear();
            foreach (var item in result.Value.Lines.Where(item => item.RemainingQuantity > 0))
            {
                var row = new SalesReturnLineSelection { Source = item, Warehouses = warehouses, Warehouse = warehouses.FirstOrDefault(item => item.Id == result.Value.OriginalWarehouseId) ?? warehouses.FirstOrDefault() };
                row.PropertyChanged += (_, _) => RefreshSummary();
                Lines.Add(row);
            }
            RefreshSummary();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void AddRefund() => Refunds.Add(CreateRefundEntry());

    [RelayCommand]
    private void RemoveRefund(ReturnPaymentEntryViewModel? item)
    {
        if (item is not null && Refunds.Count > 1) Refunds.Remove(item);
    }

    [RelayCommand]
    private void FillRefundTotal()
    {
        var total = Lines.Sum(item => item.EstimatedAmount);
        Refunds.Clear();
        var entry = CreateRefundEntry();
        entry.Amount = total;
        Refunds.Add(entry);
    }

    [RelayCommand]
    private async Task CompleteReturnAsync()
    {
        if (_returnable is null || SelectedSale is null) return;
        var selectedLines = Lines.Where(item => item.Quantity > 0).ToList();
        if (selectedLines.Count == 0) { _toast.ShowWarning("İade miktarı seçin."); return; }
        if (selectedLines.Any(item => item.Disposition == ReturnDisposition.Restock && item.Warehouse is null)) { _toast.ShowWarning("Satılabilir iade için hedef depo seçin."); return; }
        var estimated = selectedLines.Sum(item => item.EstimatedAmount);
        if (Refunds.Sum(item => item.Amount) != estimated) { _toast.ShowWarning("Para iadesi toplamı seçilen kalemlerin toplamına eşit olmalıdır."); return; }
        if (string.IsNullOrWhiteSpace(Reason)) { _toast.ShowWarning("İade nedeni zorunludur."); return; }

        var external = Refunds.Where(item => item.PaymentMethod != PaymentMethod.OnAccount).Sum(item => item.Amount);
        var stockTargets = string.Join("\n", selectedLines.GroupBy(item => item.Disposition == ReturnDisposition.Quarantine ? "İade / Karantina" : item.Warehouse!.Name)
            .Select(group => $"• {group.Key}: {group.Sum(item => item.Quantity)} adet"));
        var confirmed = await _dialogs.ShowConfirmationAsync(
            $"Stok hedefleri:\n{stockTargets}\n\nİade toplamı: {estimated:C}\nGerçek para çıkışı: {external:C}\nCari alacak: {estimated - external:C}\n\nİşlem tamamlandıktan sonra değiştirilemez. Devam edilsin mi?",
            "İade Onayı");
        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var command = new ReturnSaleCommand(
                SelectedSale.SalesOrderId,
                selectedLines.Select(item => new SalesReturnLineInput(item.Source.SalesOrderItemId, item.Quantity, item.Disposition, item.Warehouse?.Id ?? 0)).ToList(),
                Refunds.Where(item => item.Amount > 0).Select(item => new PaymentAllocationInput(item.PaymentMethod, item.Amount, item.Reference)).ToList(),
                Reason,
                Notes,
                App.CurrentUser?.Username ?? "Sistem",
                _returnAttemptId.ToString());
            var result = await _transactions.ReturnSaleAsync(command);
            if (result.IsFailure || result.Value is null) { _toast.ShowError(result.Error); return; }
            var receipt = await _readService.GetSalesReturnReceiptAsync(result.Value.ReturnId);
            if (receipt.IsFailure || receipt.Value is null)
            {
                _toast.ShowWarning($"İade tamamlandı ancak fiş verisi alınamadı: {receipt.Error}");
            }
            try
            {
                if (receipt.Value is not null)
                    await _printer.PrintSalesReturnReceiptAsync(receipt.Value);
            }
            catch (Exception printException)
            {
                _toast.ShowWarning($"İade tamamlandı ancak fiş yazdırılamadı: {printException.Message}");
            }
            _toast.ShowSuccess($"İade tamamlandı: {result.Value.ReturnNumber}");
            _returnAttemptId = Guid.NewGuid();
            Reason = Notes = string.Empty;
            await SearchAsync();
            await LoadSelectedSaleAsync(SelectedSale.SalesOrderId);
        }
        finally { IsBusy = false; }
    }

    private ReturnPaymentEntryViewModel CreateRefundEntry()
    {
        var entry = new ReturnPaymentEntryViewModel();
        entry.PropertyChanged += (_, _) => RefreshSummary();
        return entry;
    }

    private void RefreshSummary()
    {
        var selected = Lines.Where(item => item.Quantity > 0).ToList();
        Summary = selected.Count == 0
            ? "İade miktarlarını girin."
            : $"{selected.Sum(item => item.Quantity)} adet · Tahmini {selected.Sum(item => item.EstimatedAmount):C} · Ödeme dağılımı {Refunds.Sum(item => item.Amount):C}";
    }
}
