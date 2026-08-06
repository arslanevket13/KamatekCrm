using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;
using KamatekCrm.Views;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Keşif sekmesi: keşif raporu, ziyaretler, tahmini malzemeler, fotoğraflar ve
    /// keşif → teklif akışı. Buton aktifliği AllowedActions'tan gelir (ScheduleDiscovery,
    /// EditDiscovery, CompleteDiscovery, CreateQuotation).
    /// </summary>
    public partial class WorkspaceDiscoveryViewModel : WorkspaceTabViewModelBase
    {
        private readonly int _jobId;
        private readonly IServiceJobReadService _readService;
        private readonly IServiceJobCommandService _commandService;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;
        private readonly Func<Task> _refresh;
        private readonly Func<Window?> _ownerProvider;
        private readonly Func<Task>? _openQuotationEditor;

        public WorkspaceDiscoveryViewModel(
            int jobId,
            IServiceJobReadService readService,
            IServiceJobCommandService commandService,
            IDialogService dialogService,
            IToastService toastService,
            Func<WorkOrderAction, Task> executeAction,
            Func<Task> refresh,
            Func<Window?> ownerProvider,
            Func<Task>? openQuotationEditor = null)
            : base(executeAction)
        {
            _jobId = jobId;
            _readService = readService;
            _commandService = commandService;
            _dialogService = dialogService;
            _toastService = toastService;
            _refresh = refresh;
            _ownerProvider = ownerProvider;
            _openQuotationEditor = openQuotationEditor;
        }

        public bool HasDiscovery { get; private set; }
        public string Technician { get; private set; } = "—";
        public string LaborHours { get; private set; } = "—";
        public string TechnicalNotes { get; private set; } = "—";
        public string RecommendedSolution { get; private set; } = "—";
        public string MaterialSummary { get; private set; } = "—";
        public string VisitSummary { get; private set; } = "Ziyaret kaydı yok";
        public string PhotoSummary { get; private set; } = "Fotoğraf yok";
        public string StatusLine { get; private set; } = "Keşif kaydı henüz oluşturulmadı.";
        public string CompletionSummary { get; private set; } = string.Empty;

        public ObservableCollection<DiscoveryMaterialDto> Materials { get; } = new();
        public ObservableCollection<DiscoveryVisitDto> Visits { get; } = new();
        public ObservableCollection<string> Photos { get; } = new();

        protected override bool IsRelevantAction(WorkOrderAction action) => action is
            WorkOrderAction.ScheduleDiscovery or
            WorkOrderAction.EditDiscovery or
            WorkOrderAction.CompleteDiscovery or
            WorkOrderAction.CreateQuotation;

        public void ApplyData(DiscoveryReportDto? discovery, IReadOnlyList<DiscoveryVisitDto>? visits)
        {
            Materials.Clear();
            Visits.Clear();
            Photos.Clear();

            HasDiscovery = discovery is not null;

            if (discovery is not null)
            {
                Technician = string.IsNullOrWhiteSpace(discovery.TechnicianName) ? "Atanmadı" : discovery.TechnicianName;
                LaborHours = $"{discovery.EstimatedLaborHours:N1} saat";
                TechnicalNotes = string.IsNullOrWhiteSpace(discovery.TechnicalNotes) ? "Tespit notu girilmedi." : discovery.TechnicalNotes;
                RecommendedSolution = string.IsNullOrWhiteSpace(discovery.RecommendedSolution) ? "Çözüm önerisi girilmedi." : discovery.RecommendedSolution;
                MaterialSummary = discovery.Materials.Count == 0 ? "Malzeme öngörülmedi" : $"{discovery.Materials.Count} kalem";
                StatusLine = "Keşif raporu kaydedildi.";
                foreach (var material in discovery.Materials) Materials.Add(material);
                foreach (var photo in discovery.PhotoPaths) Photos.Add(photo);
            }
            else
            {
                Technician = "—";
                LaborHours = "—";
                TechnicalNotes = "—";
                RecommendedSolution = "—";
                MaterialSummary = "—";
                StatusLine = "Keşif kaydı henüz oluşturulmadı.";
            }

            if (visits is not null)
            {
                foreach (var visit in visits) Visits.Add(visit);
            }
            VisitSummary = Visits.Count == 0 ? "Ziyaret kaydı yok" : $"{Visits.Count} ziyaret";
            PhotoSummary = Photos.Count == 0 ? "Fotoğraf yok" : $"{Photos.Count} fotoğraf/belge";

            // Bilgilendirme: tamamlama ön koşulu artık CompleteDiscovery action'ının
            // IsEnabled/DisabledReason değerinde yaşar; bu satır yalnızca açıklama sunar.
            bool ready =
                HasDiscovery &&
                (!string.IsNullOrWhiteSpace(discovery?.TechnicalNotes) || !string.IsNullOrWhiteSpace(discovery?.RecommendedSolution)) &&
                (Materials.Count > 0 || Visits.Count > 0);
            CompletionSummary = ready
                ? "Keşif tamamlanmaya hazır."
                : "Keşif tamamlanması için rapor, teknik not ve en az bir malzeme/ziyaret gerekli.";

            OnPropertyChanged(nameof(HasDiscovery));
            OnPropertyChanged(nameof(Technician));
            OnPropertyChanged(nameof(LaborHours));
            OnPropertyChanged(nameof(TechnicalNotes));
            OnPropertyChanged(nameof(RecommendedSolution));
            OnPropertyChanged(nameof(MaterialSummary));
            OnPropertyChanged(nameof(VisitSummary));
            OnPropertyChanged(nameof(PhotoSummary));
            OnPropertyChanged(nameof(StatusLine));
            OnPropertyChanged(nameof(CompletionSummary));
        }

        /// <summary>Keşif raporu / randevu / teknisyen ekranını açar (ScheduleDiscovery + EditDiscovery).</summary>
        public async Task OpenEditorAsync()
        {
            bool canSchedule = IsActionEnabled(WorkOrderAction.ScheduleDiscovery, out var scheduleReason);
            bool canEdit = IsActionEnabled(WorkOrderAction.EditDiscovery, out _);
            if (!canSchedule && !canEdit)
            {
                _toastService.ShowWarning(scheduleReason);
                return;
            }

            try
            {
                var vm = new DiscoveryEditorViewModel(
                    _jobId, _readService, _commandService, _dialogService, _toastService);
                if (!await vm.InitializeAsync())
                {
                    _toastService.ShowError("Keşif verileri yüklenemedi.");
                    return;
                }

                var window = new DiscoveryEditorWindow(vm) { Owner = _ownerProvider() };
                if (window.ShowDialog() == true) await _refresh();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Keşif ekranı açılamadı: {ex.Message}");
            }
        }

        /// <summary>Keşfi tamamlar (CompleteDiscovery).</summary>
        public async Task CompleteAsync()
        {
            if (!IsActionEnabled(WorkOrderAction.CompleteDiscovery, out var reason))
            {
                _toastService.ShowWarning(reason);
                return;
            }

            try
            {
                var result = await _commandService.CompleteDiscoveryAsync(
                    _jobId, App.CurrentUser?.Username ?? "Sistem");
                if (result.IsFailure)
                {
                    _toastService.ShowError(result.Error);
                    await _refresh();
                    return;
                }

                _toastService.ShowSuccess("Keşif tamamlandı. Şimdi teklif oluşturabilirsiniz.");
                await _refresh();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Keşif tamamlanamadı: {ex.Message}");
            }
        }

        /// <summary>Keşfi teklife dönüştürür (CreateQuotation) — ön koşullar servis tarafında da doğrulanır.</summary>
        public async Task CreateQuotationAsync()
        {
            if (!IsActionEnabled(WorkOrderAction.CreateQuotation, out var reason))
            {
                _toastService.ShowWarning(reason);
                return;
            }

            try
            {
                var conversion = await _commandService.ConvertToQuoteAsync(
                    _jobId, App.CurrentUser?.Username ?? "Sistem");
                if (conversion.IsFailure)
                {
                    _toastService.ShowError(conversion.Error);
                    await _refresh();
                    return;
                }

                _toastService.ShowSuccess($"İş #{_jobId} teklif aşamasına alındı; teklif kaydı oluşturuldu.");
                await _refresh();

                if (_openQuotationEditor is not null &&
                    await _dialogService.ShowConfirmationAsync(
                        "Teklif kaydı oluşturuldu. Fiyat ve şartları düzenlemek için teklif ekranını açmak ister misiniz?",
                        "Teklif Düzenle"))
                {
                    await _openQuotationEditor();
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Teklife dönüştürülürken hata: {ex.Message}");
            }
        }
    }
}
