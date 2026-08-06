using System;
using System.Collections.ObjectModel;
using System.Linq;
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
    /// Teslim sekmesi: teslim/ödeme özeti, teslim editörü ve fatura / servis raporu üretimi.
    /// Buton aktifliği AllowedActions'tan gelir (CompleteDelivery, GenerateInvoice,
    /// GenerateServiceReport). PDF üretimi, shell'in sağladığı canlı workflow aggregate'inden
    /// beslenir (GetWorkspaceAsync projeksiyonu üzerinden).
    /// </summary>
    public partial class WorkspaceDeliveryViewModel : WorkspaceTabViewModelBase
    {
        private readonly int _jobId;
        private readonly IServiceJobReadService _readService;
        private readonly IServiceJobCommandService _commandService;
        private readonly PdfService _pdfService;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;
        private readonly Func<Task> _refresh;
        private readonly Func<Window?> _ownerProvider;
        private readonly Func<WorkOrderWorkflowDto?> _workflowProvider;

        public WorkspaceDeliveryViewModel(
            int jobId,
            IServiceJobReadService readService,
            IServiceJobCommandService commandService,
            PdfService pdfService,
            IDialogService dialogService,
            IToastService toastService,
            Func<WorkOrderAction, Task> executeAction,
            Func<Task> refresh,
            Func<Window?> ownerProvider,
            Func<WorkOrderWorkflowDto?> workflowProvider)
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
            _workflowProvider = workflowProvider;
        }

        public bool HasDelivery { get; private set; }
        public bool IsDelivered { get; private set; }
        public string DateDisplay { get; private set; } = "—";
        public string DeliveredByDisplay { get; private set; } = "—";
        public string NoteDisplay { get; private set; } = "—";
        public string SignatureDisplay { get; private set; } = "İmza alınmadı";
        public string PaymentStatusDisplay { get; private set; } = "—";
        public string PaymentMethodDisplay { get; private set; } = "—";
        public string PaidAmountDisplay { get; private set; } = "—";
        public string BalanceDisplay { get; private set; } = "—";
        public string InvoiceNumberDisplay { get; private set; } = "—";
        public string StatusLine { get; private set; } = "Teslim kaydı henüz oluşturulmadı.";

        protected override bool IsRelevantAction(WorkOrderAction action) => action is
            WorkOrderAction.CompleteDelivery;

        /// <summary>BELGELER kartı: fatura / servis raporu butonları (alt satırdan ayrı).</summary>
        public ObservableCollection<WorkspaceActionItem> DocumentActions { get; } = new();
        public bool HasDocumentActions => DocumentActions.Count > 0;

        public override void ApplyActions(IReadOnlyList<WorkOrderActionInfo>? all)
        {
            base.ApplyActions(all); // Actions = CompleteDelivery

            DocumentActions.Clear();
            if (all is not null && ExecuteAction is not null)
            {
                foreach (var info in all.Where(action => action.Action is
                             WorkOrderAction.GenerateInvoice or WorkOrderAction.GenerateServiceReport))
                {
                    DocumentActions.Add(new WorkspaceActionItem(info, ExecuteAction));
                }
            }
            OnPropertyChanged(nameof(HasDocumentActions));
        }

        public void ApplyData(
            JobDeliveryDto? delivery,
            JobStatus jobStatus,
            WorkOrderQuotationDto? quote,
            InstallationOrderDto? installation)
        {
            HasDelivery = delivery is not null;
            IsDelivered = jobStatus == JobStatus.Delivered || delivery is not null;
            StatusLine = IsDelivered
                ? "🚚 İş teslim edildi ve kapandı — fatura / servis raporu PDF üretebilirsiniz."
                : jobStatus == JobStatus.InstallationCompleted
                    ? "Montaj tamamlandı — teslim kaydını oluşturmak için 'Teslim Et' butonunu kullanın."
                    : "Teslim kaydı henüz oluşturulmadı.";

            if (delivery is not null)
            {
                DateDisplay = delivery.DeliveryDate.ToString("dd.MM.yyyy HH:mm");
                DeliveredByDisplay = string.IsNullOrWhiteSpace(delivery.DeliveredBy) ? "—" : delivery.DeliveredBy;
                NoteDisplay = string.IsNullOrWhiteSpace(delivery.DeliveryNote) ? "—" : delivery.DeliveryNote;
                SignatureDisplay = string.IsNullOrWhiteSpace(delivery.CustomerSignature)
                    ? "İmza alınmadı"
                    : "✍️ İmza alındı";
                PaymentStatusDisplay = PaymentStatusLabels.Map(delivery.PaymentStatus);
                PaymentMethodDisplay = PaymentMethodLabels.Map(delivery.PaymentMethod);
                PaidAmountDisplay = $"{delivery.PaidAmount:N2} ₺";
                InvoiceNumberDisplay = string.IsNullOrWhiteSpace(delivery.InvoiceNumber) ? "—" : delivery.InvoiceNumber;
            }
            else
            {
                DateDisplay = "—";
                DeliveredByDisplay = "—";
                PaymentStatusDisplay = "—";
                PaymentMethodDisplay = "—";
                PaidAmountDisplay = "—";
                InvoiceNumberDisplay = "—";
                // Teslim notu / imza montaj tamamlama formundan gelir (ön bilgi)
                NoteDisplay = string.IsNullOrWhiteSpace(installation?.DeliveryNote)
                    ? "—"
                    : installation.DeliveryNote;
                SignatureDisplay = string.IsNullOrWhiteSpace(installation?.CustomerSignature)
                    ? "İmza alınmadı"
                    : "✍️ İmza alındı (montaj formu)";
            }

            decimal total = quote?.TotalAmount ?? 0m;
            decimal paid = delivery?.PaidAmount ?? 0m;
            BalanceDisplay = $"{Math.Max(0m, total - paid):N2} ₺";

            OnPropertyChanged(nameof(HasDelivery));
            OnPropertyChanged(nameof(IsDelivered));
            OnPropertyChanged(nameof(DateDisplay));
            OnPropertyChanged(nameof(DeliveredByDisplay));
            OnPropertyChanged(nameof(NoteDisplay));
            OnPropertyChanged(nameof(SignatureDisplay));
            OnPropertyChanged(nameof(PaymentStatusDisplay));
            OnPropertyChanged(nameof(PaymentMethodDisplay));
            OnPropertyChanged(nameof(PaidAmountDisplay));
            OnPropertyChanged(nameof(BalanceDisplay));
            OnPropertyChanged(nameof(InvoiceNumberDisplay));
            OnPropertyChanged(nameof(StatusLine));
        }

        /// <summary>Teslim kaydını oluşturur / düzenler (CompleteDelivery).</summary>
        public async Task OpenEditorAsync()
        {
            if (!IsActionEnabled(WorkOrderAction.CompleteDelivery, out var reason))
            {
                _toastService.ShowWarning(reason);
                return;
            }

            try
            {
                var vm = new DeliveryEditorViewModel(
                    _jobId, _readService, _commandService, _toastService);
                if (!await vm.InitializeAsync())
                {
                    _toastService.ShowError("Teslim verileri yüklenemedi.");
                    return;
                }

                var window = new DeliveryEditorWindow(vm) { Owner = _ownerProvider() };
                if (window.ShowDialog() == true) await _refresh();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Teslim ekranı açılamadı: {ex.Message}");
            }
        }

        /// <summary>Fatura PDF üretir (kabul edilmiş teklif veya teslim kaydı varsa).</summary>
        public async Task GenerateInvoiceAsync()
        {
            if (!IsActionEnabled(WorkOrderAction.GenerateInvoice, out var reason))
            {
                _toastService.ShowWarning(reason);
                return;
            }

            try
            {
                var workflow = _workflowProvider();
                if (workflow is null)
                {
                    _toastService.ShowError("İş emri verileri yüklenemedi; sayfayı yenileyin.");
                    return;
                }
                var document = await _readService.GetDocumentAsync(_jobId);
                if (document.IsFailure || document.Value is null)
                {
                    _toastService.ShowError(document.Error);
                    return;
                }

                var filePath = await _dialogService.ShowSaveFileDialogAsync(
                    "Faturayı Kaydet", "PDF Dosyası (*.pdf)|*.pdf", $"fatura_is_{_jobId:D6}.pdf");
                if (string.IsNullOrWhiteSpace(filePath)) return;

                _pdfService.GenerateWorkOrderInvoice(workflow, document.Value, filePath);
                _toastService.ShowSuccess("Fatura PDF üretildi.");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Fatura üretilemedi: {ex.Message}");
            }
        }

        /// <summary>Servis raporu PDF üretir.</summary>
        public async Task GenerateServiceReportAsync()
        {
            if (!IsActionEnabled(WorkOrderAction.GenerateServiceReport, out var reason))
            {
                _toastService.ShowWarning(reason);
                return;
            }

            try
            {
                var workflow = _workflowProvider();
                if (workflow is null)
                {
                    _toastService.ShowError("İş emri verileri yüklenemedi; sayfayı yenileyin.");
                    return;
                }
                var document = await _readService.GetDocumentAsync(_jobId);
                if (document.IsFailure || document.Value is null)
                {
                    _toastService.ShowError(document.Error);
                    return;
                }

                var filePath = await _dialogService.ShowSaveFileDialogAsync(
                    "Servis Raporunu Kaydet", "PDF Dosyası (*.pdf)|*.pdf", $"servis_raporu_{_jobId:D6}.pdf");
                if (string.IsNullOrWhiteSpace(filePath)) return;

                _pdfService.GenerateWorkOrderServiceReport(workflow, document.Value, filePath);
                _toastService.ShowSuccess("Servis raporu PDF üretildi.");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Servis raporu üretilemedi: {ex.Message}");
            }
        }
    }
}
