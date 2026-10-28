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

        public string StockShortageWarning { get; private set; } = string.Empty;
        public bool HasStockShortage => !string.IsNullOrEmpty(StockShortageWarning);

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

                // Malzeme stok hazırlık kontrolü
                var ws = await _readService.GetWorkspaceAsync(_jobId);
                if (ws.IsSuccess && ws.Value is not null)
                {
                    var shortages = ws.Value.Warnings.Where(w => w.Code == "MaterialStockShortage").Select(w => w.Message).ToList();
                    StockShortageWarning = shortages.Count > 0 ? string.Join("\n", shortages) : string.Empty;
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
            OnPropertyChanged(nameof(StockShortageWarning));
            OnPropertyChanged(nameof(HasStockShortage));
        }

        public override async Task OnTabActivatedAsync()
        {
            await LoadMaterialsAsync();
        }

        public override Task RefreshAsync() => _refresh();

        [RelayCommand]
        private Task Refresh() => RefreshAsync();

        [RelayCommand]
        private async Task OpenGeneralEditor()
        {
            if (_openGeneralEditor is null) return;
            await _openGeneralEditor();
        }
    }
}
