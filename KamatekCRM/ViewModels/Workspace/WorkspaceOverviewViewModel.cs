using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Genel Bakış sekmesi: iş künyesi (Header), kullanılan malzemeler ve teklif özeti.
    /// Hızlı işlemler, application katmanının AllowedActions listesinden beslenir.
    /// </summary>
    public partial class WorkspaceOverviewViewModel : WorkspaceTabViewModelBase
    {
        private readonly int _jobId;
        private readonly IServiceJobReadService _readService;
        private readonly IToastService _toastService;
        private readonly Func<Task> _refresh;
        private readonly Func<Task>? _openGeneralEditor;

        public WorkspaceOverviewViewModel(
            int jobId,
            IServiceJobReadService readService,
            IToastService toastService,
            Func<WorkOrderAction, Task> executeAction,
            Func<Task> refresh,
            Func<Task>? openGeneralEditor = null)
            : base(executeAction)
        {
            _jobId = jobId;
            _readService = readService;
            _toastService = toastService;
            _refresh = refresh;
            _openGeneralEditor = openGeneralEditor;
        }

        public WorkspaceJobHeader Header { get; } = new();
        public ObservableCollection<ServiceJobMaterialDto> Materials { get; } = new();

        public string MaterialSummary { get; private set; } = "—";
        public string MaterialTotalDisplay { get; private set; } = "—";
        public string QuoteSummaryDisplay { get; private set; } = "Teklif yok";
        public bool HasGeneralEditor => _openGeneralEditor != null;

        protected override bool IsRelevantAction(WorkOrderAction action) => action is
            WorkOrderAction.EditGeneralInfo or
            WorkOrderAction.AssignResponsible or
            WorkOrderAction.CancelWorkOrder;

        internal void ApplyHeader(ServiceJobRowDto row, WorkOrderWorkspaceDto dto) => Header.Apply(row, dto);

        internal void ApplyQuote(WorkOrderQuotationDto? quote)
        {
            QuoteSummaryDisplay = quote is { } q
                ? $"{q.QuotationNumber} — {q.TotalAmount:N2} ₺ ({QuotationStatusLabels.Map(q.Status)})"
                : "Teklif yok";
            OnPropertyChanged(nameof(QuoteSummaryDisplay));
        }

        public async Task LoadMaterialsAsync()
        {
            try
            {
                Materials.Clear();
                var materials = await _readService.GetMaterialsAsync(_jobId);
                if (materials.IsSuccess && materials.Value is not null)
                {
                    foreach (var item in materials.Value) Materials.Add(item);
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Malzemeler yüklenemedi: {ex.Message}");
            }

            MaterialSummary = Materials.Count == 0 ? "Malzeme girilmedi" : $"{Materials.Count} kalem";
            MaterialTotalDisplay = $"{Materials.Sum(m => m.UnitPrice * m.QuantityUsed):N2} ₺";
            OnPropertyChanged(nameof(MaterialSummary));
            OnPropertyChanged(nameof(MaterialTotalDisplay));
        }

        [RelayCommand]
        private Task Refresh() => _refresh();

        [RelayCommand]
        private async Task OpenGeneralEditor()
        {
            if (_openGeneralEditor is null) return;
            await _openGeneralEditor();
        }
    }
}
