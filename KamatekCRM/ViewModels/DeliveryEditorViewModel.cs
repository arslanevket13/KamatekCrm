using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>Ödeme durumu seçenek kutusu (ComboBox için etiket + değer).</summary>
    public sealed class PaymentStatusOption
    {
        public PaymentStatus Value { get; }
        public string Label { get; }

        public PaymentStatusOption(PaymentStatus value)
        {
            Value = value;
            Label = PaymentStatusLabels.Map(value);
        }
    }

    /// <summary>Ödeme yöntemi seçenek kutusu (ComboBox için etiket + değer).</summary>
    public sealed class PaymentMethodOption
    {
        public PaymentMethod Value { get; }
        public string Label { get; }

        public PaymentMethodOption(PaymentMethod value)
        {
            Value = value;
            Label = PaymentMethodLabels.Map(value);
        }
    }

    /// <summary>
    /// Teslim V2 editörü (Paket 7): teslim eden, teslim notu, müşteri imzası ve ödeme bilgileri
    /// (durum, yöntem, tahsilat, fatura no) tek ekranda düzenlenir. "İşi Teslim Et" ödeme
    /// tutarlılığı doğrulamasından sonra işi Delivered durumuna alır. Teslim edilmiş kayıtlar
    /// görüntüleme modunda açılır.
    /// </summary>
    public partial class DeliveryEditorViewModel : ViewModelBase
    {
        private readonly int _jobId;
        private readonly IServiceJobReadService _readService;
        private readonly IServiceJobCommandService _commandService;
        private readonly IToastService _toastService;

        public event Action? RequestClose;
        public event Action? RequestCloseWithSuccess;

        /// <summary>Pencere açılışında code-behind tarafından atanır.</summary>
        public System.Windows.Window? OwnerWindow { get; set; }

        public ObservableCollection<PaymentStatusOption> PaymentStatusOptions { get; } = new();
        public ObservableCollection<PaymentMethodOption> PaymentMethodOptions { get; } = new();

        public string HeaderTitle { get; private set; } = "Teslim";
        public string HeaderSubtitle { get; private set; } = string.Empty;

        // ── Teslim formu ──
        private string _deliveredBy = string.Empty;
        public string DeliveredBy { get => _deliveredBy; set => SetProperty(ref _deliveredBy, value); }

        private string _deliveryNote = string.Empty;
        public string DeliveryNote { get => _deliveryNote; set => SetProperty(ref _deliveryNote, value); }

        private string _customerSignature = string.Empty;
        public string CustomerSignature { get => _customerSignature; set => SetProperty(ref _customerSignature, value); }

        private PaymentStatus _paymentStatus;
        public PaymentStatus PaymentStatus
        {
            get => _paymentStatus;
            set { SetProperty(ref _paymentStatus, value); RefreshCheck(); }
        }

        private PaymentMethod _paymentMethod;
        public PaymentMethod PaymentMethod { get => _paymentMethod; set => SetProperty(ref _paymentMethod, value); }

        private decimal _paidAmount;
        public decimal PaidAmount
        {
            get => _paidAmount;
            set { SetProperty(ref _paidAmount, Math.Max(0m, value)); RefreshCheck(); }
        }

        private string _invoiceNumber = string.Empty;
        public string InvoiceNumber { get => _invoiceNumber; set => SetProperty(ref _invoiceNumber, value); }

        // ── Doğrulama göstergeleri ──
        public bool IsDelivered { get; private set; }
        public bool IsReadyToComplete { get; private set; }
        public string CompletionCheckSummary { get; private set; } = string.Empty;
        public string CompletionError { get; private set; } = string.Empty;

        /// <summary>Teklif genel toplamı (bakiye hesabı için).</summary>
        public decimal QuotationTotal { get; private set; }
        public string QuotationTotalDisplay => $"{QuotationTotal:N2} ₺";

        public DeliveryEditorViewModel(
            int jobId,
            IServiceJobReadService readService,
            IServiceJobCommandService commandService,
            IToastService toastService)
        {
            _jobId = jobId;
            _readService = readService;
            _commandService = commandService;
            _toastService = toastService;

            foreach (PaymentStatus value in Enum.GetValues<PaymentStatus>())
            {
                PaymentStatusOptions.Add(new PaymentStatusOption(value));
            }
            foreach (PaymentMethod value in Enum.GetValues<PaymentMethod>())
            {
                PaymentMethodOptions.Add(new PaymentMethodOption(value));
            }

            PaymentStatus = PaymentStatus.Unpaid;
            PaymentMethod = PaymentMethod.Cash;
        }

        public async Task<bool> InitializeAsync()
        {
            var workflow = await _readService.GetWorkOrderWorkflowAsync(_jobId);
            if (workflow.IsFailure || workflow.Value is null)
            {
                _toastService.ShowError(workflow.Error ?? "İş emri verileri yüklenemedi.");
                return false;
            }

            var document = await _readService.GetDocumentAsync(_jobId);
            if (document.IsFailure || document.Value is null)
            {
                _toastService.ShowError(document.Error);
                return false;
            }

            HeaderTitle = $"Teslim — {document.Value.CustomerName}";
            HeaderSubtitle = $"İş #{_jobId:D6} • {document.Value.Description}";
            QuotationTotal = workflow.Value.Quotation?.TotalAmount ?? 0m;

            var delivery = workflow.Value.Delivery;
            if (delivery is not null)
            {
                IsDelivered = true;
                DeliveredBy = delivery.DeliveredBy ?? string.Empty;
                DeliveryNote = delivery.DeliveryNote ?? string.Empty;
                CustomerSignature = delivery.CustomerSignature ?? string.Empty;
                PaymentStatus = delivery.PaymentStatus;
                PaymentMethod = delivery.PaymentMethod;
                PaidAmount = delivery.PaidAmount;
                InvoiceNumber = delivery.InvoiceNumber ?? string.Empty;
            }
            else
            {
                // İlk teslim: montaj tamamlama formundaki teslim notu/imza ön doldurulur
                var installation = workflow.Value.Installation;
                if (installation is not null)
                {
                    DeliveryNote = installation.DeliveryNote ?? string.Empty;
                    CustomerSignature = installation.CustomerSignature ?? string.Empty;
                }
            }

            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
            OnPropertyChanged(nameof(IsDelivered));
            OnPropertyChanged(nameof(QuotationTotalDisplay));
            RefreshCheck();
            return true;
        }

        private void RefreshCheck()
        {
            bool statusConsistent = PaymentStatus == PaymentStatus.Unpaid
                ? PaidAmount == 0m
                : PaidAmount > 0m;

            IsReadyToComplete = !IsDelivered && statusConsistent;

            CompletionCheckSummary = IsDelivered
                ? "✅ İş teslim edildi — kayıt görüntüleme modunda."
                : IsReadyToComplete
                    ? "✅ Teslim kaydı tamamlanmaya hazır."
                    : "Teslim için ödeme bilgileri tutarlı olmalı:";

            var missing = new System.Collections.Generic.List<string>();
            if (PaymentStatus == PaymentStatus.Unpaid && PaidAmount > 0m)
                missing.Add("tahsilat bekleniyor iken tutar girilemez");
            if (PaymentStatus != PaymentStatus.Unpaid && PaidAmount <= 0m)
                missing.Add("kısmi/ödenmiş durumda tahsilat tutarı girilmeli");
            CompletionError = string.Join(" • ", missing);

            OnPropertyChanged(nameof(IsReadyToComplete));
            OnPropertyChanged(nameof(CompletionCheckSummary));
            OnPropertyChanged(nameof(CompletionError));
            SaveCommand.NotifyCanExecuteChanged();
        }

        private bool CanSave() => IsReadyToComplete;

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task Save()
        {
            var request = new CompleteDeliveryRequest(
                _jobId,
                string.IsNullOrWhiteSpace(DeliveredBy) ? null : DeliveredBy.Trim(),
                string.IsNullOrWhiteSpace(DeliveryNote) ? null : DeliveryNote.Trim(),
                string.IsNullOrWhiteSpace(CustomerSignature) ? null : CustomerSignature,
                PaymentStatus,
                PaymentMethod,
                PaidAmount,
                string.IsNullOrWhiteSpace(InvoiceNumber) ? null : InvoiceNumber.Trim(),
                App.CurrentUser?.Username ?? "Sistem");

            var result = await _commandService.CompleteDeliveryAsync(request);
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                RefreshCheck();
                return;
            }

            _toastService.ShowSuccess("İş teslim edildi; ödeme bilgileri kaydedildi.");
            RequestCloseWithSuccess?.Invoke();
        }

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke();
    }
}
