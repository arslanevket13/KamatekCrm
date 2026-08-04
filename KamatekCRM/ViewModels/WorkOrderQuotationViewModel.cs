using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Teklif kalemi satırı — miktar/fiyat/iskonto/KDV düzenlenebilir, ara toplam anında hesaplanır.
    /// </summary>
    public sealed class QuotationItemRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int? SourceId { get; }
        public int? ProductId { get; }
        public string ProductName { get; }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set { _quantity = Math.Max(0, value); OnChanged(); }
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set { _unitPrice = Math.Max(0m, value); OnChanged(); }
        }

        private decimal _discountPercent;
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set { _discountPercent = Math.Max(0m, value); OnChanged(); }
        }

        private decimal _taxPercent;
        public decimal TaxPercent
        {
            get => _taxPercent;
            set { _taxPercent = Math.Max(0m, value); OnChanged(); }
        }

        public decimal LineTotal => Math.Round(Quantity * UnitPrice * (1m - DiscountPercent / 100m), 2);

        public QuotationItemRow(QuotationItemDto item)
        {
            SourceId = item.Id;
            ProductId = item.ProductId;
            ProductName = item.ProductName;
            _quantity = item.Quantity;
            _unitPrice = item.UnitPrice;
            _discountPercent = item.DiscountPercent;
            _taxPercent = item.TaxPercent;
        }

        private void OnChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LineTotal)));
    }

    /// <summary>
    /// İş emri teklif düzenleme ekranı: malzeme, miktar, birim fiyat, iskonto, KDV,
    /// işçilik, nakliye, açıklamalar, garanti, teslim süresi ve ödeme şartları.
    /// </summary>
    public partial class WorkOrderQuotationViewModel : ViewModelBase
    {
        private readonly int _jobId;
        private readonly IServiceJobReadService _readService;
        private readonly IServiceJobCommandService _commandService;
        private readonly PdfService _pdfService;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;

        private WorkOrderQuotationDto _quotation = null!;
        private ServiceJobDocumentDto _jobDocument = null!;

        public event Action? RequestClose;
        public event Action? RequestCloseWithSuccess;

        public ObservableCollection<QuotationItemRow> Items { get; } = new();

        public string QuotationNumber { get; private set; } = string.Empty;
        public string StatusDisplay { get; private set; } = string.Empty;
        public bool IsEditable { get; private set; } = true;

        private string _description = string.Empty;
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private string _warranty = string.Empty;
        public string Warranty { get => _warranty; set => SetProperty(ref _warranty, value); }

        private string _deliveryTime = string.Empty;
        public string DeliveryTime { get => _deliveryTime; set => SetProperty(ref _deliveryTime, value); }

        private string _paymentTerms = string.Empty;
        public string PaymentTerms { get => _paymentTerms; set => SetProperty(ref _paymentTerms, value); }

        private decimal _laborCost;
        public decimal LaborCost { get => _laborCost; set { SetProperty(ref _laborCost, value); RecalculateTotals(); } }

        private decimal _shippingCost;
        public decimal ShippingCost { get => _shippingCost; set { SetProperty(ref _shippingCost, value); RecalculateTotals(); } }

        private decimal _discountAmount;
        public decimal DiscountAmount { get => _discountAmount; set { SetProperty(ref _discountAmount, value); RecalculateTotals(); } }

        private decimal _taxRate = 20m;
        public decimal TaxRate { get => _taxRate; set { SetProperty(ref _taxRate, value); RecalculateTotals(); } }

        public decimal Subtotal => Items.Sum(i => i.LineTotal);
        public decimal NetTotal => Subtotal - DiscountAmount + LaborCost + ShippingCost;
        public decimal TaxAmount => Math.Round(NetTotal * TaxRate / 100m, 2);
        public decimal TotalAmount => Math.Round(NetTotal + TaxAmount, 2);

        public WorkOrderQuotationViewModel(
            int jobId,
            IServiceJobReadService readService,
            IServiceJobCommandService commandService,
            PdfService pdfService,
            IDialogService dialogService,
            IToastService toastService)
        {
            _jobId = jobId;
            _readService = readService;
            _commandService = commandService;
            _pdfService = pdfService;
            _dialogService = dialogService;
            _toastService = toastService;

            Items.CollectionChanged += (_, _) => RecalculateTotals();
            foreach (var item in Items) item.PropertyChanged += (_, _) => RecalculateTotals();
        }

        public async Task<bool> InitializeAsync()
        {
            var workflow = await _readService.GetWorkOrderWorkflowAsync(_jobId);
            if (workflow.IsFailure || workflow.Value is null || workflow.Value.Quotation is null)
            {
                _toastService.ShowError(workflow.Error ?? "Bu iş emri için teklif bulunamadı.");
                return false;
            }

            var document = await _readService.GetDocumentAsync(_jobId);
            if (document.IsFailure || document.Value is null)
            {
                _toastService.ShowError(document.Error);
                return false;
            }

            _quotation = workflow.Value.Quotation;
            _jobDocument = document.Value;

            QuotationNumber = _quotation.QuotationNumber;
            StatusDisplay = _quotation.Status switch
            {
                QuotationStatus.Draft => "📝 Taslak",
                QuotationStatus.Sent => "✉️ Gönderildi",
                QuotationStatus.Accepted => "✅ Kabul Edildi",
                QuotationStatus.Rejected => "❌ Reddedildi",
                QuotationStatus.Cancelled => "🚫 İptal Edildi",
                QuotationStatus.Expired => "⏳ Süresi Doldu",
                _ => _quotation.Status.ToString()
            };

            IsEditable = _quotation.Status is QuotationStatus.Draft or QuotationStatus.Sent;

            Description = _quotation.Description ?? string.Empty;
            Warranty = _quotation.Warranty ?? string.Empty;
            DeliveryTime = _quotation.DeliveryTime ?? string.Empty;
            PaymentTerms = _quotation.PaymentTerms ?? string.Empty;
            LaborCost = _quotation.LaborCost;
            ShippingCost = _quotation.ShippingCost;
            DiscountAmount = _quotation.DiscountAmount;
            TaxRate = _quotation.TaxRate;

            Items.Clear();
            foreach (var item in _quotation.Items)
            {
                var row = new QuotationItemRow(item);
                row.PropertyChanged += (_, _) => RecalculateTotals();
                Items.Add(row);
            }

            OnPropertyChanged(nameof(IsEditable));
            OnPropertyChanged(nameof(QuotationNumber));
            OnPropertyChanged(nameof(StatusDisplay));
            RecalculateTotals();
            return true;
        }

        private void RecalculateTotals()
        {
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(NetTotal));
            OnPropertyChanged(nameof(TaxAmount));
            OnPropertyChanged(nameof(TotalAmount));
            SaveCommand.NotifyCanExecuteChanged();
        }

        private bool CanSave() => IsEditable && Items.Any(i => i.Quantity > 0);

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task Save()
        {
            if (!IsEditable)
            {
                _toastService.ShowWarning("Bu teklif düzenlenemez; durum kilidi aktif.");
                return;
            }

            var request = new UpdateWorkOrderQuotationRequest(
                _quotation.Id,
                Description,
                Warranty,
                DeliveryTime,
                PaymentTerms,
                LaborCost,
                ShippingCost,
                DiscountAmount,
                TaxRate,
                Items.Select(i => new QuotationItemInput(
                    i.SourceId, i.ProductId, i.ProductName, i.Quantity,
                    i.UnitPrice, i.DiscountPercent, i.TaxPercent)).ToList());

            var result = await _commandService.UpdateQuotationAsync(request);
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            _toastService.ShowSuccess($"Teklif güncellendi — Genel Toplam: {result.Value!.TotalAmount:N2} ₺");
            RequestCloseWithSuccess?.Invoke();
        }

        [RelayCommand]
        private async Task Accept()
        {
            if (_quotation.Status == QuotationStatus.Accepted)
            {
                _toastService.ShowInfo("Teklif zaten kabul edilmiş durumda.");
                return;
            }

            var result = await _commandService.AcceptQuotationAsync(_quotation.Id, "Kullanıcı");
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            _toastService.ShowSuccess("Teklif kabul edildi. 'Montaj Yapılacak' aşaması artık kullanılabilir.");
            RequestCloseWithSuccess?.Invoke();
        }

        [RelayCommand]
        private async Task Reject()
        {
            string? reason = await _dialogService.ShowInputAsync("Red gerekçesi:", "Teklif Reddi", "Fiyat uygun bulunmadı.");
            if (reason is null) return;

            var result = await _commandService.RejectQuotationAsync(_quotation.Id, reason.Trim(), "Kullanıcı");
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            _toastService.ShowSuccess("Teklif reddedildi.");
            RequestCloseWithSuccess?.Invoke();
        }

        [RelayCommand]
        private async Task PrintPdf()
        {
            var filePath = await _dialogService.ShowSaveFileDialogAsync(
                "Fiyat Teklifini Kaydet",
                "PDF Dosyası (*.pdf)|*.pdf",
                $"FiyatTeklifi_{_jobId:D6}.pdf");
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                _pdfService.GenerateWorkOrderQuotationPdf(_quotation, _jobDocument, filePath);
                Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
                _toastService.ShowSuccess("Fiyat teklifi PDF olarak oluşturuldu.");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"PDF oluşturulurken hata: {ex.Message}");
            }
        }

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke();
    }
}
