using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;
using KamatekCrm.Services;
using KamatekCrm.Views;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Teklif sekmesi: teklif durumu kartı, kalemler ve teklif işlemleri.
    /// Buton aktifliği AllowedActions'tan gelir (EditQuotation, SendQuotation,
    /// AcceptQuotation, RejectQuotation, ReviseQuotation, PlanInstallation, CreateQuotation).
    /// </summary>
    public partial class WorkspaceQuotationViewModel : WorkspaceTabViewModelBase
    {
        private readonly int _jobId;
        private readonly IServiceJobReadService _readService;
        private readonly IServiceJobCommandService _commandService;
        private readonly PdfService _pdfService;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;
        private readonly Func<Task> _refresh;
        private readonly Func<Window?> _ownerProvider;

        public WorkspaceQuotationViewModel(
            int jobId,
            IServiceJobReadService readService,
            IServiceJobCommandService commandService,
            PdfService pdfService,
            IDialogService dialogService,
            IToastService toastService,
            Func<WorkOrderAction, Task> executeAction,
            Func<Task> refresh,
            Func<Window?> ownerProvider)
            : base(executeAction)
        {
            _jobId = jobId;
            _readService = readService;
            _commandService = commandService;
            _pdfService = pdfService;
            _dialogService = dialogService;
            _toastService = toastService;
            _refresh = refresh;
            _ownerProvider = ownerProvider;
        }

        private int _quotationId;

        public bool HasQuotation { get; private set; }
        public string NumberDisplay { get; private set; } = "—";
        public string RevisionDisplay { get; private set; } = "—";
        public string StatusDisplay { get; private set; } = "—";
        public string IssuedDisplay { get; private set; } = "—";
        public string ValidUntilDisplay { get; private set; } = "—";
        public string SentDisplay { get; private set; } = "—";
        public string AcceptedDisplay { get; private set; } = "—";
        public string RejectedDisplay { get; private set; } = "—";
        public string RejectionReason { get; private set; } = "—";
        public string LaborDisplay { get; private set; } = "—";
        public string ShippingDisplay { get; private set; } = "—";
        public string DiscountDisplay { get; private set; } = "—";
        public string TaxDisplay { get; private set; } = "—";
        public string TotalDisplay { get; private set; } = "—";

        public ObservableCollection<QuotationItemDto> Items { get; } = new();

        protected override bool IsRelevantAction(WorkOrderAction action) => action is
            WorkOrderAction.CreateQuotation or
            WorkOrderAction.EditQuotation or
            WorkOrderAction.SendQuotation or
            WorkOrderAction.AcceptQuotation or
            WorkOrderAction.RejectQuotation or
            WorkOrderAction.ReviseQuotation or
            WorkOrderAction.PlanInstallation;

        public void ApplyData(WorkOrderQuotationDto? quote)
        {
            Items.Clear();
            HasQuotation = quote is not null;
            if (quote is null)
            {
                _quotationId = 0;
                OnPropertyChanged(nameof(HasQuotation));
                return;
            }

            _quotationId = quote.Id;
            NumberDisplay = quote.QuotationNumber;
            RevisionDisplay = $"Revizyon {quote.RevisionNumber}";
            StatusDisplay = QuotationStatusLabels.Map(quote.Status);
            IssuedDisplay = quote.IssuedDate.ToString("dd.MM.yyyy");
            ValidUntilDisplay = quote.ValidUntil?.ToString("dd.MM.yyyy") ?? "—";
            SentDisplay = quote.SentDate?.ToString("dd.MM.yyyy HH:mm") ?? "Gönderilmedi";
            AcceptedDisplay = quote.AcceptedAt?.ToString("dd.MM.yyyy HH:mm") ?? "Kabul edilmedi";
            RejectedDisplay = quote.RejectedAt?.ToString("dd.MM.yyyy HH:mm") ?? "Reddedilmedi";
            RejectionReason = string.IsNullOrWhiteSpace(quote.RejectionReason) ? "—" : quote.RejectionReason;
            LaborDisplay = $"{quote.LaborCost:N2} ₺";
            ShippingDisplay = $"{quote.ShippingCost:N2} ₺";
            DiscountDisplay = $"{quote.DiscountAmount:N2} ₺";
            TaxDisplay = $"{quote.TaxAmount:N2} ₺";
            TotalDisplay = $"{quote.TotalAmount:N2} ₺";
            foreach (var item in quote.Items) Items.Add(item);

            OnPropertyChanged(nameof(HasQuotation));
            OnPropertyChanged(nameof(NumberDisplay));
            OnPropertyChanged(nameof(RevisionDisplay));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(IssuedDisplay));
            OnPropertyChanged(nameof(ValidUntilDisplay));
            OnPropertyChanged(nameof(SentDisplay));
            OnPropertyChanged(nameof(AcceptedDisplay));
            OnPropertyChanged(nameof(RejectedDisplay));
            OnPropertyChanged(nameof(RejectionReason));
            OnPropertyChanged(nameof(LaborDisplay));
            OnPropertyChanged(nameof(ShippingDisplay));
            OnPropertyChanged(nameof(DiscountDisplay));
            OnPropertyChanged(nameof(TaxDisplay));
            OnPropertyChanged(nameof(TotalDisplay));
        }

        /// <summary>
        /// Teklifi müşteriye gönderilmiş olarak işaretler (Draft → Sent). "Teklifi Gönder"
        /// action'ının gerçek uygulaması: onay sonrası komut servisi çağrılır, iş tazelenir.
        /// </summary>
        public async Task SendQuotationAsync()
        {
            if (!HasQuotation || _quotationId <= 0)
            {
                _toastService.ShowWarning("Bu iş emri için teklif oluşturulmamış.");
                return;
            }

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                "Teklif müşteriye gönderilmiş olarak işaretlenecek. Devam edilsin mi?", "Teklifi Gönder");
            if (!confirmed) return;

            var result = await _commandService.SendQuotationAsync(
                _quotationId, App.CurrentUser?.Username ?? "Sistem");
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            _toastService.ShowSuccess("Teklif gönderildi — müşteri cevabı (kabul/ret) artık kaydedilebilir.");
            await _refresh();
        }

        /// <summary>
        /// Teklif editörünü açar. EditQuotation / Accept / Reject / Revise işlemlerinin
        /// tam akışı (kabul, ret, revizyon, PDF) editör içinde yürütülür.
        /// </summary>
        public async Task OpenEditorAsync()
        {
            if (!HasQuotation)
            {
                _toastService.ShowWarning("Bu iş emri için teklif oluşturulmamış.");
                return;
            }

            try
            {
                var vm = new WorkOrderQuotationViewModel(
                    _jobId, _readService, _commandService, _pdfService, _dialogService, _toastService);
                if (!await vm.InitializeAsync())
                {
                    _toastService.ShowError("Teklif verileri yüklenemedi.");
                    return;
                }

                var window = new WorkOrderQuotationWindow(vm) { Owner = _ownerProvider() };
                if (window.ShowDialog() == true) await _refresh();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Teklif ekranı açılamadı: {ex.Message}");
            }
        }
    }
}
