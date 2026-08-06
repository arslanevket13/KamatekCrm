using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    public enum WorkOrderStageState
    {
        Pending,
        Current,
        Completed,
        Skipped
    }

    /// <summary>Süreç göstergesindeki tek aşama (Talep → Keşif → Teklif → Montaj → Teslim → Kapandı).</summary>
    public sealed class WorkOrderStageItem
    {
        public string Name { get; }
        public string Glyph { get; }
        public int TargetTabIndex { get; }
        public WorkOrderStageState State { get; set; }

        /// <summary>Adıma tıklandığında ilgili sekmeyi açar (shell tarafından bağlanır).</summary>
        public ICommand? NavigateCommand { get; set; }

        public WorkOrderStageItem(string name, string glyph, int targetTabIndex)
        {
            Name = name;
            Glyph = glyph;
            TargetTabIndex = targetTabIndex;
        }
    }

    /// <summary>
    /// İş Emri Çalışma Alanı — shell. Yalnızca:
    ///  • iş kimliğini ve başlık künyesini tutar,
    ///  • merkezi projeksiyonu (GetWorkspaceAsync) yükler ve uygular,
    ///  • aktif sekmeyi ve süreç göstergesini yönetir,
    ///  • AllowedActions / NextAction'ı application katmanından alır ve işlem dağıtıcısıdır,
    ///  • refresh'i koordine eder.
    /// Sekme verileri ve komutları ayrı sekme ViewModel'lerinde yaşar (OverviewTab,
    /// DiscoveryTab, QuotationTab, InstallationTab, DeliveryTab, TimelineTab).
    /// </summary>
    public partial class WorkOrderWorkspaceViewModel : ViewModelBase
    {
        private readonly int _jobId;
        private readonly ServiceJobRowDto _job;
        private readonly IServiceJobReadService _readService;
        private readonly IServiceJobCommandService _commandService;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;
        private readonly Func<Task>? _openGeneralEditor;

        private WorkOrderWorkspaceDto? _workspace;

        public event Action? RequestClose;

        // ── Sekme ViewModel'leri ──
        public WorkspaceOverviewViewModel OverviewTab { get; }
        public WorkspaceDiscoveryViewModel DiscoveryTab { get; }
        public WorkspaceQuotationViewModel QuotationTab { get; }
        public WorkspaceInstallationViewModel InstallationTab { get; }
        public WorkspaceDeliveryViewModel DeliveryTab { get; }
        public WorkspaceDocumentsViewModel DocumentsTab { get; }
        public WorkspaceTimelineViewModel TimelineTab { get; }

        [ObservableProperty]
        private int _activeTabIndex;

        // ── Başlık künyesi (GetWorkspaceAsync'ten tazelenir) ──
        public int JobId => _jobId;
        public string JobNumber { get; private set; }
        public string CustomerFullName { get; private set; }
        public string CustomerPhone { get; private set; }
        public string Description { get; private set; }
        public string WorkOrderTypeDisplay { get; private set; }
        public string PriorityDisplay { get; private set; }
        public string StatusDisplay { get; private set; }
        public string AssignedTechnician { get; private set; }
        public string ScheduledDateDisplay { get; private set; }
        public string CreatedDateDisplay { get; private set; }
        public string SlaStatusDisplay { get; private set; }

        // ── Süreç göstergesi ──
        public ObservableCollection<WorkOrderStageItem> Stages { get; } = new();
        public string CurrentStageName { get; private set; } = "—";
        public bool IsCancelled { get; private set; }

        // ── Sıradaki işlem / uyarılar (application katmanından) ──
        public WorkOrderNextActionInfo? NextAction { get; private set; }
        public bool HasNextAction => NextAction?.Action != null;
        public string NextActionTitle => NextAction?.Title ?? "—";
        public string NextActionDescription => NextAction?.Description ?? string.Empty;
        public string NextActionButtonText => NextAction?.PrimaryButtonText ?? "İşlemi Yap";
        public bool NextActionEnabled => NextAction?.IsEnabled ?? false;
        public string NextActionDisabledReason => NextAction?.DisabledReason ?? string.Empty;

        public ObservableCollection<WorkspaceWarningItem> Warnings { get; } = new();
        public bool HasWarnings => Warnings.Count > 0;

        public bool HasGeneralEditor => _openGeneralEditor != null;

        /// <summary>Pencere açılışında code-behind tarafından atanır; içeriden açılan editörlerin sahibi olur.</summary>
        public Window? OwnerWindow { get; set; }

        public WorkOrderWorkspaceViewModel(
            ServiceJobRowDto job,
            IServiceJobReadService readService,
            IServiceJobCommandService commandService,
            PdfService pdfService,
            IDialogService dialogService,
            IToastService toastService,
            Func<Task>? openGeneralEditor = null)
        {
            _job = job ?? throw new ArgumentNullException(nameof(job));
            _jobId = job.Id;
            _readService = readService;
            _commandService = commandService;
            _dialogService = dialogService;
            _toastService = toastService;
            _openGeneralEditor = openGeneralEditor;

            JobNumber = $"#{_jobId:D6}";
            CustomerFullName = job.CustomerFullName;
            CustomerPhone = job.CustomerPhone;
            Description = job.Description;
            WorkOrderTypeDisplay = job.WorkOrderTypeDisplay;
            PriorityDisplay = job.PriorityDisplay;
            StatusDisplay = ServiceJobRowDto.MapStatusDisplay(job.Status);
            AssignedTechnician = string.IsNullOrWhiteSpace(job.AssignedTechnician) ? "Atanmadı" : job.AssignedTechnician;
            ScheduledDateDisplay = job.ScheduledDate?.ToString("dd.MM.yyyy HH:mm") ?? "Planlanmadı";
            CreatedDateDisplay = job.CreatedDate.ToString("dd.MM.yyyy HH:mm");
            SlaStatusDisplay = job.SlaStatusDisplay;

            Func<Window?> owner = () => OwnerWindow ?? Application.Current.MainWindow;
            Func<Task> refresh = () => InitializeAsync();

            OverviewTab = new WorkspaceOverviewViewModel(
                _jobId, readService, toastService, DispatchActionAsync, refresh,
                openGeneralEditor is null ? null : OpenGeneralEditorAsync);
            QuotationTab = new WorkspaceQuotationViewModel(
                _jobId, readService, commandService, pdfService, dialogService, toastService,
                DispatchActionAsync, refresh, owner);
            DiscoveryTab = new WorkspaceDiscoveryViewModel(
                _jobId, readService, commandService, dialogService, toastService,
                DispatchActionAsync, refresh, owner, QuotationTab.OpenEditorAsync);
            InstallationTab = new WorkspaceInstallationViewModel(
                _jobId, readService, commandService, dialogService, toastService,
                DispatchActionAsync, refresh, owner);
            DeliveryTab = new WorkspaceDeliveryViewModel(
                _jobId, readService, commandService, pdfService, dialogService, toastService,
                DispatchActionAsync, refresh, owner, BuildWorkflow);
            DocumentsTab = new WorkspaceDocumentsViewModel(
                _jobId, readService, pdfService, dialogService, toastService, BuildWorkflow);
            TimelineTab = new WorkspaceTimelineViewModel();

            Stages.Add(new WorkOrderStageItem("Talep", "📥", 0));
            Stages.Add(new WorkOrderStageItem("Keşif", "🔍", 1));
            Stages.Add(new WorkOrderStageItem("Teklif", "📄", 2));
            Stages.Add(new WorkOrderStageItem("Montaj", "🛠️", 3));
            Stages.Add(new WorkOrderStageItem("Teslim", "🚚", 4));
            Stages.Add(new WorkOrderStageItem("Kapandı", "✅", 0));

            foreach (var stage in Stages)
            {
                var captured = stage;
                stage.NavigateCommand = new RelayCommand(() => ActiveTabIndex = captured.TargetTabIndex);
            }
        }

        /// <summary>Çalışma alanını merkezi projeksiyondan yükler ve tüm sekmelere uygular.</summary>
        public async Task<bool> InitializeAsync()
        {
            var result = await _readService.GetWorkspaceAsync(_jobId);
            if (result.IsFailure || result.Value is null)
            {
                _toastService.ShowError(result.Error ?? "İş emri verileri yüklenemedi.");
                return false;
            }

            ApplyWorkspace(result.Value);
            await OverviewTab.LoadMaterialsAsync();
            return true;
        }

        private void ApplyWorkspace(WorkOrderWorkspaceDto dto)
        {
            _workspace = dto;

            JobNumber = dto.WorkOrderNumber;
            CustomerFullName = string.IsNullOrWhiteSpace(dto.CustomerName) ? _job.CustomerFullName : dto.CustomerName;
            CustomerPhone = string.IsNullOrWhiteSpace(dto.CustomerPhone) ? _job.CustomerPhone : dto.CustomerPhone;
            Description = string.IsNullOrWhiteSpace(dto.Description) ? _job.Description : dto.Description;
            WorkOrderTypeDisplay = _job.WorkOrderTypeDisplay;
            PriorityDisplay = _job.PriorityDisplay;
            StatusDisplay = ServiceJobRowDto.MapStatusDisplay(dto.JobStatus);
            AssignedTechnician = string.IsNullOrWhiteSpace(dto.AssignedTechnicianName) ? "Atanmadı" : dto.AssignedTechnicianName;
            ScheduledDateDisplay = dto.TargetDate?.ToString("dd.MM.yyyy HH:mm") ?? "Planlanmadı";
            CreatedDateDisplay = dto.CreatedAt.ToString("dd.MM.yyyy HH:mm");
            SlaStatusDisplay = dto.SlaStatus;

            LoadStages(dto.CurrentStage);

            NextAction = dto.NextAction;
            OnPropertyChanged(nameof(NextAction));
            OnPropertyChanged(nameof(HasNextAction));
            OnPropertyChanged(nameof(NextActionTitle));
            OnPropertyChanged(nameof(NextActionDescription));
            OnPropertyChanged(nameof(NextActionButtonText));
            OnPropertyChanged(nameof(NextActionEnabled));
            OnPropertyChanged(nameof(NextActionDisabledReason));

            Warnings.Clear();
            foreach (var warning in dto.Warnings) Warnings.Add(new WorkspaceWarningItem(warning));
            OnPropertyChanged(nameof(HasWarnings));

            // Sekmeler: veri + izinli işlemler (UI kural üretmez; application çözer)
            OverviewTab.ApplyHeader(_job, dto);
            OverviewTab.ApplyQuote(dto.QuotationSummary);
            OverviewTab.ApplyActions(dto.AllowedActions);

            DiscoveryTab.ApplyData(dto.DiscoverySummary, dto.Visits);
            DiscoveryTab.ApplyActions(dto.AllowedActions);

            QuotationTab.ApplyData(dto.QuotationSummary);
            QuotationTab.ApplyActions(dto.AllowedActions);

            InstallationTab.ApplyData(dto.InstallationSummary);
            InstallationTab.ApplyActions(dto.AllowedActions);

            DeliveryTab.ApplyData(dto.DeliverySummary, dto.JobStatus, dto.QuotationSummary, dto.InstallationSummary);
            DeliveryTab.ApplyActions(dto.AllowedActions);

            DocumentsTab.ApplyData(dto);

            TimelineTab.ApplyData(dto.RecentActivities);

            OnPropertyChanged(nameof(JobNumber));
            OnPropertyChanged(nameof(CustomerFullName));
            OnPropertyChanged(nameof(CustomerPhone));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(WorkOrderTypeDisplay));
            OnPropertyChanged(nameof(PriorityDisplay));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(AssignedTechnician));
            OnPropertyChanged(nameof(ScheduledDateDisplay));
            OnPropertyChanged(nameof(CreatedDateDisplay));
            OnPropertyChanged(nameof(SlaStatusDisplay));
            OnPropertyChanged(nameof(HasGeneralEditor));
        }

        // ── Süreç göstergesi ──

        private static int MapStageIndex(WorkOrderStage stage) => stage switch
        {
            WorkOrderStage.Pending => 0,
            WorkOrderStage.Discovery => 1,
            WorkOrderStage.Quotation => 2,
            WorkOrderStage.Installation => 3,
            WorkOrderStage.Delivery => 4,
            WorkOrderStage.Closed => 5,
            _ => 0
        };

        private void LoadStages(WorkOrderStage stage)
        {
            IsCancelled = stage == WorkOrderStage.Cancelled;

            if (IsCancelled)
            {
                foreach (var item in Stages) item.State = WorkOrderStageState.Skipped;
                CurrentStageName = "İptal Edildi";
            }
            else
            {
                int current = MapStageIndex(stage);
                for (int i = 0; i < Stages.Count; i++)
                {
                    Stages[i].State = i < current ? WorkOrderStageState.Completed
                        : i == current ? WorkOrderStageState.Current
                        : WorkOrderStageState.Pending;
                }
                CurrentStageName = Stages[current].Name;
            }

            OnPropertyChanged(nameof(IsCancelled));
            OnPropertyChanged(nameof(CurrentStageName));
        }

        // ── PDF için canlı workflow aggregate'i (GetWorkspaceAsync projeksiyonundan) ──

        private WorkOrderWorkflowDto? BuildWorkflow() => _workspace is null
            ? null
            : new WorkOrderWorkflowDto(
                _workspace.JobId,
                _workspace.JobStatus,
                _workspace.DiscoverySummary,
                _workspace.QuotationSummary,
                _workspace.InstallationSummary,
                _workspace.Visits,
                _workspace.DeliverySummary);

        // ── Komutlar ──

        [RelayCommand]
        private async Task Refresh()
        {
            _toastService.ShowInfo("İş dosyası yenileniyor...");
            var ok = await InitializeAsync();
            if (ok) _toastService.ShowSuccess("İş dosyası güncellendi.");
        }

        [RelayCommand]
        private void Close() => RequestClose?.Invoke();

        [RelayCommand]
        private Task OpenGeneralEditor() => OpenGeneralEditorAsync();

        /// <summary>AllowedActions butonlarının ortak dağıtıcısı: işlemi doğru sekme/ekrana yönlendirir.</summary>
        [RelayCommand]
        private Task ExecuteAction(WorkOrderAction action) => DispatchActionAsync(action);

        /// <summary>
        /// İşlemin ev sahibi sekme. NextAction panelinden (veya başka bir sekmeden) tetiklenen
        /// bir işlem, önce bu sekmeye geçer — kullanıcı işlemin bağlamını görür.
        /// Sekme sırası: 0 Genel Bakış, 1 Keşif, 2 Teklif, 3 Montaj, 4 Teslim, 5 Belgeler, 6 Geçmiş.
        /// </summary>
        internal static int TabIndexForAction(WorkOrderAction action) => action switch
        {
            WorkOrderAction.ScheduleDiscovery or WorkOrderAction.EditDiscovery
                or WorkOrderAction.CompleteDiscovery or WorkOrderAction.CreateQuotation => 1,
            WorkOrderAction.EditQuotation or WorkOrderAction.SendQuotation
                or WorkOrderAction.AcceptQuotation or WorkOrderAction.RejectQuotation
                or WorkOrderAction.ReviseQuotation or WorkOrderAction.PlanInstallation => 2,
            WorkOrderAction.EditInstallation or WorkOrderAction.CompleteInstallation => 3,
            WorkOrderAction.CompleteDelivery => 4,
            WorkOrderAction.GenerateInvoice or WorkOrderAction.GenerateServiceReport => 5,
            _ => 0 // AssignResponsible, EditGeneralInfo, CancelWorkOrder, CloseWorkOrder → Genel Bakış
        };

        private async Task DispatchActionAsync(WorkOrderAction action)
        {
            // Önce işlemin ev sahibi sekmeye geç (kullanıcı bağlamı görsün), sonra çalıştır.
            ActiveTabIndex = TabIndexForAction(action);

            switch (action)
            {
                case WorkOrderAction.EditGeneralInfo:
                case WorkOrderAction.AssignResponsible:
                    await OpenGeneralEditorAsync();
                    break;
                case WorkOrderAction.ScheduleDiscovery:
                case WorkOrderAction.EditDiscovery:
                    await DiscoveryTab.OpenEditorAsync();
                    break;
                case WorkOrderAction.CompleteDiscovery:
                    await DiscoveryTab.CompleteAsync();
                    break;
                case WorkOrderAction.CreateQuotation:
                    await DiscoveryTab.CreateQuotationAsync();
                    break;
                case WorkOrderAction.EditQuotation:
                case WorkOrderAction.AcceptQuotation:
                case WorkOrderAction.RejectQuotation:
                case WorkOrderAction.ReviseQuotation:
                    await QuotationTab.OpenEditorAsync();
                    break;
                case WorkOrderAction.SendQuotation:
                    // Gerçek uygulama: teklifi gönderilmiş olarak işaretle (Draft → Sent).
                    await QuotationTab.SendQuotationAsync();
                    break;
                case WorkOrderAction.PlanInstallation:
                    if (InstallationTab.HasInstallation)
                    {
                        await InstallationTab.OpenEditorAsync();
                    }
                    else
                    {
                        await InstallationTab.PlanAsync();
                    }
                    break;
                case WorkOrderAction.EditInstallation:
                    await InstallationTab.OpenEditorAsync();
                    break;
                case WorkOrderAction.CompleteInstallation:
                    await InstallationTab.CompleteAsync();
                    break;
                case WorkOrderAction.CompleteDelivery:
                    await DeliveryTab.OpenEditorAsync();
                    break;
                case WorkOrderAction.GenerateInvoice:
                    await DeliveryTab.GenerateInvoiceAsync();
                    break;
                case WorkOrderAction.GenerateServiceReport:
                    await DeliveryTab.GenerateServiceReportAsync();
                    break;
                case WorkOrderAction.CloseWorkOrder:
                    // Politika Delivered'dan çıkışa izin vermez; kapatma ayrı adım değildir.
                    _toastService.ShowInfo("İş teslim edildiğinde kapalı duruma geçer; ek adım gerekmez.");
                    break;
                case WorkOrderAction.CancelWorkOrder:
                    await CancelWorkOrderAsync();
                    break;
            }
        }

        private async Task OpenGeneralEditorAsync()
        {
            if (_openGeneralEditor is not null)
            {
                await _openGeneralEditor();
                await InitializeAsync();
            }
        }

        private async Task CancelWorkOrderAsync()
        {
            bool confirmed = await _dialogService.ShowConfirmationAsync(
                $"İş #{_jobId} iptal edilecek. Emin misiniz?", "İptal Onayı");
            if (!confirmed) return;

            var result = await _commandService.ChangeStatusAsync(
                _jobId, JobStatus.Cancelled, App.CurrentUser?.Username ?? "Sistem");
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            _toastService.ShowSuccess($"İş #{_jobId} iptal edildi.");
            await InitializeAsync();
        }
    }
}
