using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.ProjectQuotes;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Services;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.ViewModels;

/// <summary>
/// Proje teklifleri için liste, yaşam döngüsü ve belge orkestrasyonu.
/// Veri erişimi ve iş kuralları Application servislerindedir.
/// </summary>
public partial class QuoteListViewModel : ViewModelBase
{
    private readonly IProjectQuoteReadService _readService;
    private readonly IProjectQuoteCommandService _commandService;
    private readonly IProjectQuoteEditorLauncher _editorLauncher;
    private readonly IDialogService _dialogService;
    private readonly IToastService _toastService;
    private readonly PdfService _pdfService;
    private readonly Dictionary<string, Guid> _operationKeys = new(StringComparer.Ordinal);

    public ObservableCollection<ProjectQuoteListItemDto> Quotes { get; } = [];

    private ICollectionView? _quotesView;
    public ICollectionView? QuotesView
    {
        get => _quotesView;
        private set => SetProperty(ref _quotesView, value);
    }

    private ProjectQuoteListItemDto? _selectedQuote;
    public ProjectQuoteListItemDto? SelectedQuote
    {
        get => _selectedQuote;
        set
        {
            if (SetProperty(ref _selectedQuote, value))
                OnPropertyChanged(nameof(HasSelectedQuote));
        }
    }

    public bool HasSelectedQuote => SelectedQuote is not null;

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value)) QuotesView?.Refresh();
        }
    }

    private QuoteStatus? _statusFilter;
    public QuoteStatus? StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (!SetProperty(ref _statusFilter, value)) return;
            QuotesView?.Refresh();
            OnPropertyChanged(nameof(StatusFilterDisplay));
        }
    }

    public string StatusFilterDisplay => StatusFilter.HasValue
        ? ProjectQuoteLifecyclePolicy.Display(StatusFilter.Value)
        : "Tümü";

    public int TotalQuoteCount => Quotes.Count;
    public int DraftCount => Quotes.Count(quote => quote.QuoteStatus == QuoteStatus.Draft);
    public int SentCount => Quotes.Count(quote => quote.QuoteStatus == QuoteStatus.Sent);
    public int ApprovedCount => Quotes.Count(quote => quote.QuoteStatus == QuoteStatus.Approved);
    public int RejectedCount => Quotes.Count(quote => quote.QuoteStatus == QuoteStatus.Rejected);
    public decimal TotalApprovedAmount => Quotes
        .Where(quote => quote.QuoteStatus == QuoteStatus.Approved)
        .Sum(quote => quote.TotalBudget);
    public decimal TotalPendingAmount => Quotes
        .Where(quote => quote.QuoteStatus == QuoteStatus.Sent)
        .Sum(quote => quote.TotalBudget);
    public string TotalApprovedAmountDisplay => $"₺{TotalApprovedAmount:N2}";
    public string TotalPendingAmountDisplay => $"₺{TotalPendingAmount:N2}";

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isActionSuccessful;
    public bool IsActionSuccessful
    {
        get => _isActionSuccessful;
        set => SetProperty(ref _isActionSuccessful, value);
    }

    public QuoteListViewModel(
        IProjectQuoteReadService readService,
        IProjectQuoteCommandService commandService,
        IProjectQuoteEditorLauncher editorLauncher,
        IDialogService dialogService,
        IToastService toastService,
        PdfService pdfService)
    {
        _readService = readService;
        _commandService = commandService;
        _editorLauncher = editorLauncher;
        _dialogService = dialogService;
        _toastService = toastService;
        _pdfService = pdfService;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private void FilterAll() => StatusFilter = null;

    [RelayCommand]
    private void FilterDraft() => StatusFilter = QuoteStatus.Draft;

    [RelayCommand]
    private void FilterSent() => StatusFilter = QuoteStatus.Sent;

    [RelayCommand]
    private void FilterApproved() => StatusFilter = QuoteStatus.Approved;

    [RelayCommand]
    private void FilterRejected() => StatusFilter = QuoteStatus.Rejected;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var expiry = await _commandService.ExpireOverdueAsync();
            if (expiry.IsFailure)
            {
                SetFailure(expiry.Error);
                return;
            }

            var result = await _readService.GetListAsync();
            if (result.IsFailure || result.Value is null)
            {
                SetFailure(result.Error);
                return;
            }

            var selectedId = SelectedQuote?.Id;
            Quotes.Clear();
            foreach (var quote in result.Value) Quotes.Add(quote);
            QuotesView = CollectionViewSource.GetDefaultView(Quotes);
            QuotesView.Filter = FilterQuotes;
            SelectedQuote = selectedId.HasValue
                ? Quotes.FirstOrDefault(quote => quote.Id == selectedId.Value)
                : null;
            NotifyKpiChanged();
            StatusMessage = expiry.Value!.ExpiredCount > 0
                ? $"{Quotes.Count} teklif yüklendi; {expiry.Value.ExpiredCount} teklifin süresi doldu."
                : $"{Quotes.Count} teklif yüklendi.";
            IsActionSuccessful = true;
        }
        catch (Exception exception)
        {
            SetFailure($"Teklifler yüklenemedi: {exception.Message}");
        }
    }

    private bool FilterQuotes(object value)
    {
        if (value is not ProjectQuoteListItemDto quote) return false;
        if (StatusFilter.HasValue && quote.QuoteStatus != StatusFilter.Value) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        var search = SearchText.Trim();
        return quote.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               quote.ProjectCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               quote.CustomerName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               (quote.QuoteNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    [RelayCommand]
    private async Task NewQuote()
    {
        try
        {
            _editorLauncher.ShowNew();
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            SetFailure($"Yeni teklif açılamadı: {exception.Message}");
        }
    }

    [RelayCommand]
    private async Task EditQuote()
    {
        if (SelectedQuote is null) return;
        try
        {
            _editorLauncher.ShowEdit(SelectedQuote.Id);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            SetFailure($"Teklif düzenlenemedi: {exception.Message}");
        }
    }

    [RelayCommand]
    private async Task DuplicateQuote()
    {
        if (SelectedQuote is null) return;
        if (!await _dialogService.ShowConfirmationAsync(
                $"'{SelectedQuote.Title}' teklifinden yeni bir taslak oluşturulsun mu?",
                "Teklifi Kopyala")) return;

        var operationName = $"{SelectedQuote.Id}:duplicate";
        var result = await _commandService.DuplicateAsync(new DuplicateProjectQuoteCommand(
            OperationKey(operationName), SelectedQuote.Id, SelectedQuote.RevisionNumber));
        if (result.IsFailure || result.Value is null)
        {
            SetFailure(result.Error);
            return;
        }

        CompleteOperation(operationName);
        _toastService.ShowSuccess($"Taslak oluşturuldu: {result.Value.QuoteNumber}");
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteQuote()
    {
        if (SelectedQuote is null) return;
        if (!await _dialogService.ShowConfirmationAsync(
                $"'{SelectedQuote.Title}' taslağı silinsin mi? Gönderilmiş teklifler korunur.",
                "Taslak Teklifi Sil")) return;

        var operationName = $"{SelectedQuote.Id}:delete";
        var result = await _commandService.DeleteDraftAsync(new DeleteProjectQuoteCommand(
            OperationKey(operationName), SelectedQuote.Id, SelectedQuote.RevisionNumber,
            SelectedQuote.QuoteStatus));
        if (result.IsFailure)
        {
            SetFailure(result.Error);
            return;
        }

        CompleteOperation(operationName);
        _toastService.ShowSuccess("Taslak teklif silindi.");
        SelectedQuote = null;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ExportPdf()
    {
        if (SelectedQuote is null) return;
        var export = await _readService.GetExportAsync(SelectedQuote.Id);
        if (export.IsFailure || export.Value is null)
        {
            SetFailure(export.Error);
            return;
        }

        var filePath = await _dialogService.ShowSaveFileDialogAsync(
            "Proje Teklifini Kaydet",
            "PDF Belgeleri (.pdf)|*.pdf",
            $"Teklif_{SanitizeFileName(SelectedQuote.Title)}_{DateTime.UtcNow:yyyyMMdd}.pdf");
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            var project = ToProject(export.Value);
            _pdfService.GenerateProjectQuote(
                project,
                ProjectScopeService.Deserialize(project.ProjectScopeJson),
                filePath);
            if (await _dialogService.ShowConfirmationAsync(
                    "PDF oluşturuldu. Şimdi açmak ister misiniz?", "Teklif PDF'i Hazır"))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath)
                {
                    UseShellExecute = true
                });
            SetSuccess("PDF oluşturuldu.");
        }
        catch (Exception exception)
        {
            SetFailure($"PDF oluşturulamadı: {exception.Message}");
        }
    }

    [RelayCommand]
    private async Task MarkAsSent() => await ChangeStatusAsync(
        QuoteStatus.Sent,
        null,
        "Teklif müşteriye gönderildi olarak işaretlenecek ve 30 gün geçerli olacak. Devam edilsin mi?");

    [RelayCommand]
    private async Task MarkAsApproved() => await ChangeStatusAsync(
        QuoteStatus.Approved,
        null,
        "Müşteri onayı kaydedilecek. Bu işlem teklifi iş emrine dönüştürülebilir hâle getirir. Devam edilsin mi?");

    [RelayCommand]
    private async Task MarkAsRejected()
    {
        if (SelectedQuote is null) return;
        var reason = await _dialogService.ShowInputAsync(
            "Müşterinin ret nedenini girin:", "Teklif Reddedildi", "Müşteri tarafından reddedildi");
        if (string.IsNullOrWhiteSpace(reason)) return;
        await ChangeStatusAsync(QuoteStatus.Rejected, reason, null);
    }

    private async Task ChangeStatusAsync(QuoteStatus targetStatus, string? reason, string? confirmation)
    {
        if (SelectedQuote is null) return;
        if (confirmation is not null &&
            !await _dialogService.ShowConfirmationAsync(confirmation, "Teklif Durumu")) return;

        var operationName = $"{SelectedQuote.Id}:status-{targetStatus}";
        var result = await _commandService.ChangeStatusAsync(new ChangeProjectQuoteStatusCommand(
            OperationKey(operationName), SelectedQuote.Id, SelectedQuote.RevisionNumber,
            SelectedQuote.QuoteStatus, targetStatus, reason));
        if (result.IsFailure)
        {
            SetFailure(result.Error);
            return;
        }

        CompleteOperation(operationName);
        _toastService.ShowSuccess(
            $"Teklif durumu: {ProjectQuoteLifecyclePolicy.Display(targetStatus)}");
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ConvertToWorkOrder()
    {
        if (SelectedQuote is null) return;
        if (!await _dialogService.ShowConfirmationAsync(
                "Onaylı tekliften kurulum iş emri oluşturulsun mu? Aynı teklif için ikinci iş emri oluşturulmaz.",
                "İş Emri Oluştur")) return;

        var operationName = $"{SelectedQuote.Id}:work-order";
        var result = await _commandService.ConvertApprovedToWorkOrderAsync(
            new ConvertApprovedQuoteToWorkOrderCommand(
                OperationKey(operationName), SelectedQuote.Id, SelectedQuote.RevisionNumber,
                SelectedQuote.QuoteStatus));
        if (result.IsFailure || result.Value is null)
        {
            SetFailure(result.Error);
            return;
        }

        CompleteOperation(operationName);
        _toastService.ShowSuccess(result.Value.WasAlreadyApplied
            ? $"Bu teklifin iş emri zaten mevcut: #{result.Value.WorkOrderId}"
            : $"Kurulum iş emri oluşturuldu: #{result.Value.WorkOrderId}");
        await RefreshAsync();
    }

    private Guid OperationKey(string operation)
    {
        if (_operationKeys.TryGetValue(operation, out var key)) return key;
        key = Guid.NewGuid();
        _operationKeys[operation] = key;
        return key;
    }

    private void CompleteOperation(string operation) => _operationKeys.Remove(operation);

    private void SetSuccess(string message)
    {
        StatusMessage = message;
        IsActionSuccessful = true;
    }

    private void SetFailure(string message)
    {
        StatusMessage = message;
        IsActionSuccessful = false;
        _toastService.ShowError(message);
    }

    private void NotifyKpiChanged()
    {
        OnPropertyChanged(nameof(TotalQuoteCount));
        OnPropertyChanged(nameof(DraftCount));
        OnPropertyChanged(nameof(SentCount));
        OnPropertyChanged(nameof(ApprovedCount));
        OnPropertyChanged(nameof(RejectedCount));
        OnPropertyChanged(nameof(TotalApprovedAmount));
        OnPropertyChanged(nameof(TotalPendingAmount));
        OnPropertyChanged(nameof(TotalApprovedAmountDisplay));
        OnPropertyChanged(nameof(TotalPendingAmountDisplay));
    }

    private static ServiceProject ToProject(ProjectQuoteExportDto export)
    {
        var source = export.Quote;
        return new ServiceProject
        {
            Id = source.Id,
            Title = source.Title,
            Name = source.Title,
            CustomerId = source.CustomerId,
            ProjectCode = source.ProjectCode,
            ProjectScopeJson = source.ProjectScopeJson,
            TotalBudget = source.TotalBudget,
            TotalCost = source.TotalCost,
            TotalProfit = source.TotalProfit,
            DiscountPercent = source.DiscountPercent,
            CreatedDate = source.CreatedDate,
            PipelineStage = source.PipelineStage,
            Status = source.Status,
            TotalUnitCount = source.TotalUnitCount,
            SurveyNotes = source.SurveyNotes,
            QuoteItemsJson = source.QuoteItemsJson,
            QuoteNumber = source.QuoteNumber,
            QuoteStatus = source.QuoteStatus,
            RevisionNumber = source.RevisionNumber,
            SentDate = source.SentDate,
            ValidUntil = source.ValidUntil,
            ApprovedDate = source.ApprovedDate,
            RejectedDate = source.RejectedDate,
            RejectionReason = source.RejectionReason,
            KdvRate = source.KdvRate,
            Notes = source.Notes,
            PaymentTerms = source.PaymentTerms,
            RevisionsJson = source.RevisionsJson,
            Customer = export.Customer is null ? null : new Customer
            {
                Id = export.Customer.Id,
                CustomerCode = export.Customer.CustomerCode,
                FullName = export.Customer.FullName,
                PhoneNumber = export.Customer.PhoneNumber,
                Email = export.Customer.Email,
                City = export.Customer.City,
                District = export.Customer.District,
                Neighborhood = export.Customer.Neighborhood,
                Street = export.Customer.Street,
                BuildingNo = export.Customer.BuildingNo,
                ApartmentNo = export.Customer.ApartmentNo
            }
        };
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return string.Concat(value.Trim().Select(character => invalid.Contains(character) ? '_' : character));
    }

    public static string GetStatusColor(QuoteStatus status) => status switch
    {
        QuoteStatus.Draft => "#9E9E9E",
        QuoteStatus.Sent => "#2196F3",
        QuoteStatus.Approved => "#4CAF50",
        QuoteStatus.Rejected => "#F44336",
        QuoteStatus.Expired => "#FF9800",
        QuoteStatus.Revised => "#9C27B0",
        _ => "#757575"
    };

    public static string GetStatusText(QuoteStatus status) =>
        ProjectQuoteLifecyclePolicy.Display(status);
}
