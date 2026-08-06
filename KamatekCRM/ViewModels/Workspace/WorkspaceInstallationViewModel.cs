using System;
using System.Collections.ObjectModel;
using System.Linq;
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
    /// Montaj sekmesi: montaj emri, malzemeler, görevler ve işçilik saati.
    /// Buton aktifliği AllowedActions'tan gelir (PlanInstallation, EditInstallation,
    /// CompleteInstallation). Tamamlama ön koşulu (malzeme + işçilik) application
    /// katmanında resolver'da çözülür ve serviste tekrar doğrulanır.
    /// </summary>
    public partial class WorkspaceInstallationViewModel : WorkspaceTabViewModelBase
    {
        private readonly int _jobId;
        private readonly IServiceJobReadService _readService;
        private readonly IServiceJobCommandService _commandService;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;
        private readonly Func<Task> _refresh;
        private readonly Func<Window?> _ownerProvider;

        public WorkspaceInstallationViewModel(
            int jobId,
            IServiceJobReadService readService,
            IServiceJobCommandService commandService,
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
            _dialogService = dialogService;
            _toastService = toastService;
            _refresh = refresh;
            _ownerProvider = ownerProvider;
        }

        public bool HasInstallation { get; private set; }
        public bool IsCompleted { get; private set; }
        public string Technician { get; private set; } = "—";
        public string DateDisplay { get; private set; } = "—";
        public string Notes { get; private set; } = "—";
        public string LaborHoursDisplay { get; private set; } = "—";
        public string CompletedDisplay { get; private set; } = "—";
        public string CompletionTechnician { get; private set; } = "—";
        public string DeliveryNote { get; private set; } = "—";
        public string TaskSummary { get; private set; } = "—";
        public string MaterialSummary { get; private set; } = "—";
        public string CompletionSummary { get; private set; } = string.Empty;

        public ObservableCollection<InstallationMaterialDto> Materials { get; } = new();
        public ObservableCollection<InstallationTaskDto> Tasks { get; } = new();

        protected override bool IsRelevantAction(WorkOrderAction action) => action is
            WorkOrderAction.PlanInstallation or
            WorkOrderAction.EditInstallation or
            WorkOrderAction.CompleteInstallation;

        public void ApplyData(InstallationOrderDto? installation)
        {
            Materials.Clear();
            Tasks.Clear();

            HasInstallation = installation is not null;
            IsCompleted = installation?.CompletedAt is not null;

            if (installation is null)
            {
                CompletionSummary = "Montaj emri yok — önce montajı planlayın.";
                OnPropertyChanged(nameof(HasInstallation));
                OnPropertyChanged(nameof(IsCompleted));
                OnPropertyChanged(nameof(CompletionSummary));
                return;
            }

            Technician = string.IsNullOrWhiteSpace(installation.TechnicianName) ? "Atanmadı" : installation.TechnicianName;
            DateDisplay = installation.InstallationDate?.ToString("dd.MM.yyyy HH:mm") ?? "Planlanmadı";
            Notes = string.IsNullOrWhiteSpace(installation.Notes) ? "Not girilmedi." : installation.Notes;
            LaborHoursDisplay = $"{installation.LaborHours:N1} saat";
            CompletedDisplay = installation.CompletedAt?.ToString("dd.MM.yyyy HH:mm") ?? "Tamamlanmadı";
            CompletionTechnician = string.IsNullOrWhiteSpace(installation.CompletionTechnician) ? "—" : installation.CompletionTechnician;
            DeliveryNote = string.IsNullOrWhiteSpace(installation.DeliveryNote) ? "—" : installation.DeliveryNote;
            TaskSummary = $"{installation.Tasks.Count(t => t.IsCompleted)}/{installation.Tasks.Count} görev tamam";
            MaterialSummary = installation.Materials.Count == 0 ? "Malzeme yok" : $"{installation.Materials.Count} kalem";

            foreach (var material in installation.Materials) Materials.Add(material);
            foreach (var task in installation.Tasks) Tasks.Add(task);

            CompletionSummary = IsCompleted
                ? "Montaj tamamlandı — teslim formu PDF'e hazır."
                : "Montaj tamamlanması için en az bir malzeme ve işçilik saati gerekli.";

            OnPropertyChanged(nameof(HasInstallation));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(Technician));
            OnPropertyChanged(nameof(DateDisplay));
            OnPropertyChanged(nameof(Notes));
            OnPropertyChanged(nameof(LaborHoursDisplay));
            OnPropertyChanged(nameof(CompletedDisplay));
            OnPropertyChanged(nameof(CompletionTechnician));
            OnPropertyChanged(nameof(DeliveryNote));
            OnPropertyChanged(nameof(TaskSummary));
            OnPropertyChanged(nameof(MaterialSummary));
            OnPropertyChanged(nameof(CompletionSummary));
        }

        /// <summary>Montajı planlar (yalnızca kabul edilmiş teklif; servis doğrular).</summary>
        public async Task PlanAsync()
        {
            if (!IsActionEnabled(WorkOrderAction.PlanInstallation, out var reason))
            {
                _toastService.ShowWarning(reason);
                return;
            }

            string? dateInput = await _dialogService.ShowInputAsync(
                "Montaj tarihi (ör. 2026-08-10) ve/veya boş bırakarak planlayın:",
                "Montaj Planlama");
            if (dateInput is null) return; // iptal

            DateTime? installationDate = null;
            if (!string.IsNullOrWhiteSpace(dateInput))
            {
                if (!DateTime.TryParse(dateInput, out var parsed))
                {
                    _toastService.ShowWarning("Geçerli bir tarih girilmedi; montaj plansız kaydedilecek.");
                }
                else
                {
                    installationDate = parsed;
                }
            }

            try
            {
                var result = await _commandService.PlanInstallationAsync(new PlanInstallationRequest(
                    _jobId, null, null, installationDate, null, App.CurrentUser?.Username ?? "Sistem"));
                if (result.IsFailure)
                {
                    _toastService.ShowError(result.Error);
                    await _refresh();
                    return;
                }

                _toastService.ShowSuccess("Montaj planlandı; teklif kalemleri montaj malzemelerine kopyalandı.");
                await _refresh();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Montaj planlanamadı: {ex.Message}");
            }
        }

        /// <summary>Montaj emrini düzenler (malzemeler, görevler, işçilik saati).</summary>
        public async Task OpenEditorAsync()
        {
            if (!HasInstallation)
            {
                _toastService.ShowWarning("Bu iş emri için montaj planlanmamış.");
                return;
            }
            if (IsCompleted)
            {
                _toastService.ShowWarning("Montaj tamamlanmış; kayıt salt okunurdur.");
                return;
            }

            try
            {
                var vm = new InstallationEditorViewModel(
                    _jobId, _readService, _commandService, _dialogService, _toastService);
                if (!await vm.InitializeAsync())
                {
                    _toastService.ShowError("Montaj verileri yüklenemedi.");
                    return;
                }

                var window = new InstallationEditorWindow(vm) { Owner = _ownerProvider() };
                if (window.ShowDialog() == true) await _refresh();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Montaj ekranı açılamadı: {ex.Message}");
            }
        }

        /// <summary>Montajı tamamlar (teslim notu, teknisyen, imza — editör içinde).</summary>
        public async Task CompleteAsync()
        {
            if (!IsActionEnabled(WorkOrderAction.CompleteInstallation, out var reason))
            {
                _toastService.ShowWarning(reason);
                return;
            }

            // Tamamlama formu (teslim notu + imza) montaj editöründe tamamlanır.
            await OpenEditorAsync();
        }
    }
}
