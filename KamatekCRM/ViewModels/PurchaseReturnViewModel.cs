using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.Transactions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.ViewModels;

public sealed class PurchaseReturnLineSelection : ObservableObject
{
    public required ReturnablePurchaseLineDto Source { get; init; }
    public required IReadOnlyList<WarehouseLookupDto> Warehouses { get; init; }
    private int _quantity;
    private WarehouseLookupDto? _warehouse;
    public int Quantity { get => _quantity; set { if (SetProperty(ref _quantity, Math.Clamp(value, 0, Source.RemainingQuantity))) OnPropertyChanged(nameof(EstimatedAmount)); } }
    public WarehouseLookupDto? Warehouse { get => _warehouse; set => SetProperty(ref _warehouse, value); }
    public decimal EstimatedAmount => Source.RemainingQuantity == 0 ? 0 : Math.Round(Source.RemainingAmount * Quantity / Source.RemainingQuantity, 2, MidpointRounding.AwayFromZero);
}

public partial class PurchaseReturnViewModel : ViewModelBase
{
    private readonly IPurchasingCommandService _commands;
    private readonly ITransactionReadService _readService;
    private readonly IToastService _toast;
    private readonly IDialogService _dialogs;
    public ObservableCollection<PurchaseHistoryDto> Orders { get; } = new();
    public ObservableCollection<PurchaseReturnLineSelection> Lines { get; } = new();
    public IReadOnlyList<PaymentMethod> SettlementMethods { get; } =
        [PaymentMethod.Cash, PaymentMethod.CreditCard, PaymentMethod.BankTransfer, PaymentMethod.OnAccount];

    [ObservableProperty] private PurchaseHistoryDto? _selectedOrder;
    [ObservableProperty] private PaymentMethod _settlementMethod = PaymentMethod.OnAccount;
    [ObservableProperty] private string _settlementReference = string.Empty;
    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _summary = "Satın alma seçin.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _legacySettlementOverride;
    private ReturnablePurchaseDto? _returnable;
    private Guid _returnAttemptId = Guid.NewGuid();

    public PurchaseReturnViewModel(
        IPurchasingCommandService commands,
        ITransactionReadService readService,
        IToastService toast,
        IDialogService dialogs)
    {
        _commands = commands;
        _readService = readService;
        _toast = toast;
        _dialogs = dialogs;
        _ = LoadOrdersAsync();
    }

    partial void OnSelectedOrderChanged(PurchaseHistoryDto? value)
    {
        if (value is not null) _ = LoadReturnableAsync(value.PurchaseOrderId);
    }

    [RelayCommand]
    private async Task LoadOrdersAsync()
    {
        var result = await _readService.GetPurchaseHistoryAsync();
        if (result.IsFailure || result.Value is null) { _toast.ShowError(result.Error); return; }
        Orders.Clear();
        foreach (var row in result.Value) Orders.Add(row);
    }

    private async Task LoadReturnableAsync(int orderId)
    {
        var result = await _commands.GetReturnablePurchaseAsync(orderId);
        Lines.Clear();
        if (result.IsFailure || result.Value is null)
        {
            Summary = result.Error;
            _returnable = null;
            return;
        }
        _returnable = result.Value;
        LegacySettlementOverride = false;
        var warehouseResult = await _readService.GetActiveWarehousesAsync(includeQuarantine: true);
        if (warehouseResult.IsFailure || warehouseResult.Value is null) { _toast.ShowError(warehouseResult.Error); return; }
        var warehouses = warehouseResult.Value;
        foreach (var item in result.Value.Lines.Where(item => item.RemainingQuantity > 0))
        {
            var row = new PurchaseReturnLineSelection { Source = item, Warehouses = warehouses, Warehouse = warehouses.FirstOrDefault(value => value.Id == result.Value.OriginalWarehouseId) ?? warehouses.FirstOrDefault() };
            row.PropertyChanged += (_, _) => RefreshSummary();
            Lines.Add(row);
        }
        RefreshSummary();
        if (result.Value.RequiresLegacySettlementOverride)
            Summary += " · Eski kaydın ödeme yöntemi bilinmiyor; yönetici onayı zorunlu.";
    }

    [RelayCommand]
    private async Task CancelOrderAsync()
    {
        if (SelectedOrder is null || string.IsNullOrWhiteSpace(Reason)) { _toast.ShowWarning("Satın alma ve iptal nedeni seçilmelidir."); return; }
        var confirmed = await _dialogs.ShowConfirmationAsync("Bekleyen satın alma iptal edilsin mi?", "Satın Alma İptali");
        if (!confirmed) return;
        var result = await _commands.CancelPurchaseAsync(new CancelPurchaseCommand(SelectedOrder.PurchaseOrderId, Reason, App.CurrentUser?.Username ?? "Sistem"));
        if (result.IsFailure) _toast.ShowError(result.Error); else { _toast.ShowSuccess("Satın alma iptal edildi."); await LoadOrdersAsync(); }
    }

    [RelayCommand]
    private async Task CompleteReturnAsync()
    {
        if (_returnable is null || SelectedOrder is null) return;
        var selected = Lines.Where(item => item.Quantity > 0).ToList();
        if (selected.Count == 0 || selected.Any(item => item.Warehouse is null)) { _toast.ShowWarning("İade miktarı ve kaynak depo seçilmelidir."); return; }
        if (string.IsNullOrWhiteSpace(Reason)) { _toast.ShowWarning("İade nedeni zorunludur."); return; }
        var total = selected.Sum(item => item.EstimatedAmount);
        var cashIn = SettlementMethod == PaymentMethod.OnAccount ? 0m : total;
        var supplierOffset = SettlementMethod == PaymentMethod.OnAccount ? total : 0m;
        var stockSources = string.Join("\n", selected.GroupBy(item => item.Warehouse!.Name)
            .Select(group => $"• {group.Key}: {group.Sum(item => item.Quantity)} adet"));
        var confirmed = await _dialogs.ShowConfirmationAsync(
            $"Stok kaynakları:\n{stockSources}\n\nTedarikçi iadesi: {total:C}\nCari mahsup: {supplierOffset:C}\nKasa girişi: {cashIn:C}\nYöntem: {SettlementMethod}\n\nDevam edilsin mi?",
            "Tedarikçi İadesi");
        if (!confirmed) return;
        var result = await _commands.ReturnPurchaseAsync(new ReturnPurchaseCommand(
            SelectedOrder.PurchaseOrderId,
            selected.Select(item => new PurchaseReturnLineInput(item.Source.PurchaseOrderItemId, item.Quantity, item.Warehouse!.Id)).ToList(),
            SettlementMethod,
            SettlementReference,
            Reason,
            Notes,
            App.CurrentUser?.Username ?? "Sistem",
            _returnAttemptId.ToString(),
            LegacySettlementOverride));
        if (result.IsFailure) { _toast.ShowError(result.Error); return; }
        _toast.ShowSuccess($"Tedarikçi iadesi tamamlandı: {result.Value?.ReturnNumber}");
        _returnAttemptId = Guid.NewGuid();
        await LoadOrdersAsync();
        await LoadReturnableAsync(SelectedOrder.PurchaseOrderId);
    }

    private void RefreshSummary()
    {
        var selected = Lines.Where(item => item.Quantity > 0).ToList();
        Summary = selected.Count == 0 ? "İade miktarlarını girin." : $"{selected.Sum(item => item.Quantity)} adet · {selected.Sum(item => item.EstimatedAmount):C}";
    }
}
