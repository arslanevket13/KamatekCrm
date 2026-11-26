using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.ViewModels
{
    public sealed class WorkspaceReadinessItem
    {
        public string Title { get; init; } = string.Empty;
        public bool IsReady { get; init; }
        public int TargetTabIndex { get; init; }
        public ICommand? NavigateCommand { get; init; }
    }

    public sealed class InlineDiscoveryMaterialRow
    {
        public int? Id { get; set; }
        public int? ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public string? Notes { get; set; }
        public int AvailableStock { get; set; }
        public bool IsLowStock => ProductId.HasValue && AvailableStock < Quantity;
        public string StockBadgeText => !ProductId.HasValue
            ? "[Serbest]"
            : AvailableStock >= Quantity
                ? $"[Stokta: {AvailableStock}]"
                : $"[Eksik: {Quantity - AvailableStock}]";
    }

    public partial class WorkOrderWorkspaceViewModel
    {
        private readonly IWorkOrderIntelligenceService? _intelligenceService;
        private Dictionary<int, int> _cockpitStockMap = new();

        public event Func<IReadOnlyList<string>?>? RequestBrowsePhotos;

        // ── Active Tab Indicators (Left Rail) ──
        public bool IsOverviewTabActive => ActiveTabIndex == 0;
        public bool IsDiscoveryTabActive => ActiveTabIndex == 1;
        public bool IsQuotationTabActive => ActiveTabIndex == 2;
        public bool IsInstallationTabActive => ActiveTabIndex == 3;
        public bool IsDeliveryTabActive => ActiveTabIndex == 4;
        public bool IsDocumentsTabActive => ActiveTabIndex == 5;
        public bool IsTimelineTabActive => ActiveTabIndex == 6;

        public string DocumentCountDisplay => $"Belgeler ({DocumentsTab.Documents.Count})";

        [RelayCommand]
        private void SelectTab(string tabIndexStr)
        {
            if (int.TryParse(tabIndexStr, out int idx))
            {
                ActiveTabIndex = idx;
            }
        }

        private void NotifyActiveTabFlags()
        {
            OnPropertyChanged(nameof(IsOverviewTabActive));
            OnPropertyChanged(nameof(IsDiscoveryTabActive));
            OnPropertyChanged(nameof(IsQuotationTabActive));
            OnPropertyChanged(nameof(IsInstallationTabActive));
            OnPropertyChanged(nameof(IsDeliveryTabActive));
            OnPropertyChanged(nameof(IsDocumentsTabActive));
            OnPropertyChanged(nameof(IsTimelineTabActive));
        }

        // ── Top Bar Actions: PDF & WhatsApp ──
        [RelayCommand]
        private async Task DownloadPrimaryPdfAsync()
        {
            var readyDoc = DocumentsTab.Documents.FirstOrDefault(d => d.IsAvailable);
            if (readyDoc is null)
            {
                _toastService.ShowWarning("İndirilebilir hazır belge bulunamadı.");
                return;
            }

            await readyDoc.Command.ExecuteAsync(null);
        }

        [RelayCommand]
        private void OpenWhatsApp()
        {
            try
            {
                var jobModel = new ServiceJob
                {
                    Id = _jobId,
                    Title = _workspace?.JobTitle ?? Description,
                    Status = _workspace?.JobStatus ?? _job.Status,
                    ScheduledDate = _workspace?.TargetDate ?? _job.ScheduledDate,
                    AssignedTechnician = AssignedTechnician == "Atanmadı" ? null : AssignedTechnician,
                    TotalAmount = _job.TotalAmount,
                    Customer = new Customer { FullName = CustomerFullName, PhoneNumber = CustomerPhone }
                };

                string message = _intelligenceService?.BuildWhatsAppSummaryText(jobModel)
                    ?? $"Merhaba {CustomerFullName}, İş Emri {JobNumber} güncel durumu: {StatusDisplay}.";
                string url = _intelligenceService?.BuildWhatsAppUrl(CustomerPhone, message)
                    ?? $"https://wa.me/?text={Uri.EscapeDataString(message)}";

                try
                {
                    Clipboard.SetText(message);
                }
                catch (Exception clipEx)
                {
                    Serilog.Log.Debug(clipEx, "WhatsApp metni panoya kopyalanamadı.");
                }

                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                _toastService.ShowSuccess("WhatsApp mesajı hazırlandı ve panoya kopyalandı.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "WhatsApp bağlantısı açılamadı.");
                _toastService.ShowError("WhatsApp açılamadı.");
            }
        }

        [RelayCommand]
        private async Task ExecuteNextActionAsync()
        {
            if (NextAction?.Action is not { } action) return;
            await DispatchActionAsync(action);
        }

        // ── Right Rail: Calm Readiness Checklist ──
        public ObservableCollection<WorkspaceReadinessItem> ReadinessItems { get; } = new();

        // ── Right Rail: Live Profitability Card ──
        private WorkOrderProfitabilityDto? _profitability;
        public string MaterialCostDisplay => FormatMoney(_profitability?.MaterialCost ?? 0m);
        public string QuotedTotalDisplay => FormatMoney(_profitability?.QuotedTotal ?? _job.TotalAmount);
        public string EstimatedProfitDisplay => FormatMoney(_profitability?.EstimatedProfit ?? 0m);
        public string ProfitMarginDisplay => _profitability is not null
            ? $"%{_profitability.ProfitMarginPercent.ToString("N1", CultureInfo.GetCultureInfo("tr-TR"))}"
            : "%0,0";
        public string CollectedDisplay => FormatMoney(_profitability?.CollectedAmount ?? 0m);
        public string RemainingDisplay => FormatMoney(_profitability?.RemainingBalance ?? _job.TotalAmount);

        // ── Right Rail: Quick Snooze & Timeline Note ──
        private string _quickTimelineNote = string.Empty;
        public string QuickTimelineNote
        {
            get => _quickTimelineNote;
            set
            {
                if (SetProperty(ref _quickTimelineNote, value))
                {
                    AddQuickTimelineNoteCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool CanAddQuickTimelineNote() => !string.IsNullOrWhiteSpace(QuickTimelineNote);

        [RelayCommand(CanExecute = nameof(CanAddQuickTimelineNote))]
        private async Task AddQuickTimelineNoteAsync()
        {
            string note = QuickTimelineNote.Trim();
            if (string.IsNullOrWhiteSpace(note)) return;

            var res = await _commandService.MarkAgendaItemSeenAsync(_jobId, CurrentActor(), note);
            if (res.IsFailure)
            {
                _toastService.ShowError(res.Error);
                return;
            }

            QuickTimelineNote = string.Empty;
            _toastService.ShowSuccess("Not zaman çizelgesine eklendi.");
            await InitializeAsync();
        }

        [RelayCommand]
        private Task SnoozeTomorrow10Async()
        {
            var tomorrow10Local = DateTime.Today.AddDays(1).AddHours(10);
            return SaveFollowUpAsync(tomorrow10Local.ToUniversalTime(), "Yarın 10:00 takip hatırlatması");
        }

        [RelayCommand]
        private Task SnoozeTwoDaysAsync()
        {
            var inTwoDaysLocal = DateTime.Today.AddDays(2).AddHours(10);
            return SaveFollowUpAsync(inTwoDaysLocal.ToUniversalTime(), "2 gün sonra takip hatırlatması");
        }

        private async Task SaveFollowUpAsync(DateTime followUpUtc, string note)
        {
            var res = await _commandService.SaveSmartWorkOrderFieldsAsync(new SaveSmartWorkOrderFieldsRequest(
                _jobId,
                SerializeSubTasks(),
                _job.DiscoveryChecklistJson,
                followUpUtc,
                note,
                CurrentActor(),
                SyncCustomerInteraction: true));

            if (res.IsFailure)
            {
                _toastService.ShowError(res.Error);
                return;
            }

            _toastService.ShowSuccess($"Hatırlatma ayarlandı: {followUpUtc.ToLocalTime():dd.MM.yyyy HH:mm}");
            await InitializeAsync();
        }

        // ── Tab 0: Sub-Tasks Checklist ──
        public ObservableCollection<WorkOrderSubTaskItem> SubTasks { get; } = new();

        private string _newSubTaskTitle = string.Empty;
        public string NewSubTaskTitle
        {
            get => _newSubTaskTitle;
            set
            {
                if (SetProperty(ref _newSubTaskTitle, value))
                {
                    AddSubTaskCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool CanAddSubTask() => !string.IsNullOrWhiteSpace(NewSubTaskTitle);

        [RelayCommand(CanExecute = nameof(CanAddSubTask))]
        private async Task AddSubTaskAsync()
        {
            string title = NewSubTaskTitle.Trim();
            if (string.IsNullOrWhiteSpace(title)) return;

            SubTasks.Add(new WorkOrderSubTaskItem
            {
                Title = title,
                AssignedTo = AssignedTechnician == "Atanmadı" ? null : AssignedTechnician
            });
            NewSubTaskTitle = string.Empty;
            await PersistSubTasksAsync();
        }

        [RelayCommand]
        private async Task ToggleSubTaskAsync(WorkOrderSubTaskItem? item)
        {
            if (item is null) return;
            item.CompletedAtUtc = item.IsCompleted ? DateTime.UtcNow : null;
            await PersistSubTasksAsync();
        }

        [RelayCommand]
        private async Task RemoveSubTaskAsync(WorkOrderSubTaskItem? item)
        {
            if (item is null) return;
            SubTasks.Remove(item);
            await PersistSubTasksAsync();
        }

        private async Task PersistSubTasksAsync()
        {
            var res = await _commandService.SaveSmartWorkOrderFieldsAsync(new SaveSmartWorkOrderFieldsRequest(
                _jobId,
                SerializeSubTasks(),
                null,
                _job.NextFollowUpUtc,
                _job.FollowUpNote,
                CurrentActor(),
                SyncCustomerInteraction: false));

            if (res.IsFailure)
            {
                _toastService.ShowError(res.Error);
            }
        }

        // ── Tab 1: Direct Inline Editable Discovery Cockpit (Zero Modal Hell) ──
        private string _inlineDiscoveryTechnician = string.Empty;
        public string InlineDiscoveryTechnician
        {
            get => _inlineDiscoveryTechnician;
            set => SetProperty(ref _inlineDiscoveryTechnician, value);
        }

        private double _inlineDiscoveryLaborHours;
        public double InlineDiscoveryLaborHours
        {
            get => _inlineDiscoveryLaborHours;
            set => SetProperty(ref _inlineDiscoveryLaborHours, value);
        }

        private string _inlineDiscoveryTechnicalNotes = string.Empty;
        public string InlineDiscoveryTechnicalNotes
        {
            get => _inlineDiscoveryTechnicalNotes;
            set => SetProperty(ref _inlineDiscoveryTechnicalNotes, value);
        }

        private string _inlineDiscoveryRecommendedSolution = string.Empty;
        public string InlineDiscoveryRecommendedSolution
        {
            get => _inlineDiscoveryRecommendedSolution;
            set => SetProperty(ref _inlineDiscoveryRecommendedSolution, value);
        }

        private DateTime? _inlineDiscoveryVisitDate = DateTime.Today;
        public DateTime? InlineDiscoveryVisitDate
        {
            get => _inlineDiscoveryVisitDate;
            set => SetProperty(ref _inlineDiscoveryVisitDate, value);
        }

        public ObservableCollection<InlineDiscoveryMaterialRow> InlineDiscoveryMaterials { get; } = new();
        public ObservableCollection<ServiceJobProductLookupDto> InlineProductCatalog { get; } = new();
        public ObservableCollection<QuickMaterialKitDto> InlineQuickKits { get; } = new();
        public ObservableCollection<string> InlineDiscoveryPhotos { get; } = new();

        private ServiceJobProductLookupDto? _selectedInlineProduct;
        public ServiceJobProductLookupDto? SelectedInlineProduct
        {
            get => _selectedInlineProduct;
            set
            {
                if (SetProperty(ref _selectedInlineProduct, value))
                {
                    AddInlineMaterialCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private int _inlineMaterialQty = 1;
        public int InlineMaterialQty
        {
            get => _inlineMaterialQty;
            set
            {
                if (SetProperty(ref _inlineMaterialQty, Math.Max(1, value)))
                {
                    AddInlineMaterialCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool CanAddInlineMaterial() => SelectedInlineProduct is not null && InlineMaterialQty > 0;

        [RelayCommand(CanExecute = nameof(CanAddInlineMaterial))]
        private void AddInlineMaterial()
        {
            if (SelectedInlineProduct is null) return;
            int avail = _cockpitStockMap.TryGetValue(SelectedInlineProduct.Id, out var q) ? q : 0;

            var existing = InlineDiscoveryMaterials.FirstOrDefault(m => m.ProductId == SelectedInlineProduct.Id);
            if (existing is not null)
            {
                existing.Quantity += InlineMaterialQty;
                // Trigger UI refresh for row
                int idx = InlineDiscoveryMaterials.IndexOf(existing);
                InlineDiscoveryMaterials[idx] = existing;
            }
            else
            {
                InlineDiscoveryMaterials.Add(new InlineDiscoveryMaterialRow
                {
                    ProductId = SelectedInlineProduct.Id,
                    ProductName = SelectedInlineProduct.ProductName,
                    Quantity = InlineMaterialQty,
                    AvailableStock = avail
                });
            }

            InlineMaterialQty = 1;
        }

        [RelayCommand]
        private void RemoveInlineMaterial(InlineDiscoveryMaterialRow? row)
        {
            if (row is null) return;
            InlineDiscoveryMaterials.Remove(row);
        }

        [RelayCommand]
        private void ApplyInlineDiscoveryKit(QuickMaterialKitDto? kit)
        {
            if (kit is null) return;
            int added = 0;
            foreach (var item in kit.Items)
            {
                int avail = item.ProductId.HasValue && _cockpitStockMap.TryGetValue(item.ProductId.Value, out var q) ? q : item.AvailableStock;
                InlineDiscoveryMaterials.Add(new InlineDiscoveryMaterialRow
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    AvailableStock = avail
                });
                added++;
            }

            if (added > 0)
            {
                _toastService.ShowSuccess($"'{kit.Name}' paketinden {added} kalem keşif listesine eklendi.");
            }
        }

        [RelayCommand]
        private void BrowseInlinePhotos()
        {
            var picked = RequestBrowsePhotos?.Invoke();
            if (picked is null || picked.Count == 0) return;
            foreach (var path in picked)
            {
                if (!string.IsNullOrWhiteSpace(path) && !InlineDiscoveryPhotos.Contains(path))
                {
                    InlineDiscoveryPhotos.Add(path);
                }
            }
        }

        public void AddDroppedPhotoPaths(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (!string.IsNullOrWhiteSpace(path) && !InlineDiscoveryPhotos.Contains(path))
                {
                    InlineDiscoveryPhotos.Add(path);
                }
            }
        }

        [RelayCommand]
        private void RemoveInlinePhoto(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            InlineDiscoveryPhotos.Remove(path);
        }

        [RelayCommand]
        private async Task SaveInlineDiscoveryDraftAsync()
        {
            var ok = await PersistInlineDiscoveryCoreAsync();
            if (ok)
            {
                _toastService.ShowSuccess("Keşif taslağı kaydedildi.");
                await InitializeAsync();
            }
        }

        [RelayCommand]
        private async Task CompleteDiscoveryAndConvertToQuoteAsync()
        {
            var saved = await PersistInlineDiscoveryCoreAsync();
            if (!saved) return;

            var actor = CurrentActor();
            if (_workspace?.JobStatus is JobStatus.Pending or JobStatus.DiscoveryRequest or JobStatus.PendingDiscovery)
            {
                var comp = await _commandService.CompleteDiscoveryAsync(_jobId, actor);
                if (comp.IsFailure)
                {
                    _toastService.ShowError(comp.Error);
                    return;
                }
            }

            var conv = await _commandService.ConvertToQuoteAsync(_jobId, actor);
            if (conv.IsFailure)
            {
                _toastService.ShowError(conv.Error);
                await InitializeAsync();
                return;
            }

            _toastService.ShowSuccess("Keşif tamamlandı ve teklife aktarıldı.");
            await InitializeAsync();
            ActiveTabIndex = 2;
        }

        [RelayCommand]
        private async Task SaveShortcutAsync()
        {
            if (ActiveTabIndex == 1)
            {
                await SaveInlineDiscoveryDraftAsync();
            }
            else if (ActiveTabIndex == 0)
            {
                await PersistSubTasksAsync();
                _toastService.ShowSuccess("Alt görevler kaydedildi.");
            }
        }

        private async Task<bool> PersistInlineDiscoveryCoreAsync()
        {
            var materials = InlineDiscoveryMaterials
                .Where(m => !string.IsNullOrWhiteSpace(m.ProductName) && m.Quantity > 0)
                .Select(m => new DiscoveryMaterialInput(m.Id, m.ProductId, m.ProductName.Trim(), m.Quantity, m.Notes))
                .ToList();

            var visits = new List<DiscoveryVisitInput>();
            if (_workspace?.Visits is not null)
            {
                foreach (var v in _workspace.Visits)
                {
                    visits.Add(new DiscoveryVisitInput(v.Id, v.VisitDate, v.TechnicianName, v.Notes, v.PhotoPaths));
                }
            }

            if (visits.Count == 0 && InlineDiscoveryVisitDate.HasValue)
            {
                visits.Add(new DiscoveryVisitInput(
                    null,
                    DateTime.SpecifyKind(InlineDiscoveryVisitDate.Value, DateTimeKind.Local).ToUniversalTime(),
                    string.IsNullOrWhiteSpace(InlineDiscoveryTechnician) ? AssignedTechnician : InlineDiscoveryTechnician.Trim(),
                    InlineDiscoveryTechnicalNotes,
                    InlineDiscoveryPhotos.ToList()));
            }

            var request = new SaveDiscoveryRequest(
                _jobId,
                InlineDiscoveryTechnicalNotes,
                InlineDiscoveryRecommendedSolution,
                InlineDiscoveryLaborHours,
                string.IsNullOrWhiteSpace(InlineDiscoveryTechnician) ? AssignedTechnician : InlineDiscoveryTechnician.Trim(),
                InlineDiscoveryPhotos.ToList(),
                materials,
                visits,
                CurrentActor());

            var res = await _commandService.SaveDiscoveryAsync(request);
            if (res.IsFailure)
            {
                _toastService.ShowError(res.Error);
                return false;
            }

            return true;
        }

        private async Task InitializeCockpitDataAsync(WorkOrderWorkspaceDto dto)
        {
            OnPropertyChanged(nameof(DocumentCountDisplay));
            NotifyActiveTabFlags();

            // 1. Sub-tasks
            LoadSubTasksFromJson(_job.SubTasksJson);

            // 2. Inline Discovery fields
            var disc = dto.DiscoverySummary;
            InlineDiscoveryTechnician = disc?.TechnicianName ?? (AssignedTechnician == "Atanmadı" ? string.Empty : AssignedTechnician);
            InlineDiscoveryLaborHours = disc?.EstimatedLaborHours ?? 0;
            InlineDiscoveryTechnicalNotes = disc?.TechnicalNotes ?? string.Empty;
            InlineDiscoveryRecommendedSolution = disc?.RecommendedSolution ?? string.Empty;
            InlineDiscoveryVisitDate = dto.DiscoveryAppointmentDate?.ToLocalTime() ?? DateTime.Today;

            InlineDiscoveryPhotos.Clear();
            if (disc?.PhotoPaths is not null)
            {
                foreach (var p in disc.PhotoPaths) InlineDiscoveryPhotos.Add(p);
            }

            // 3. Product catalog & live stock map (once)
            if (InlineProductCatalog.Count == 0)
            {
                var wsLookup = await _readService.GetWorkspaceAsync();
                if (wsLookup.IsSuccess && wsLookup.Value is not null)
                {
                    foreach (var prod in wsLookup.Value.Products) InlineProductCatalog.Add(prod);
                }
            }

            if (_intelligenceService is not null)
            {
                var stockRes = await _intelligenceService.GetStockAvailabilityMapAsync(InlineProductCatalog.Select(p => p.Id));
                if (stockRes.IsSuccess && stockRes.Value is not null)
                {
                    _cockpitStockMap = new Dictionary<int, int>(stockRes.Value);
                }

                if (InlineQuickKits.Count == 0)
                {
                    var kitsRes = await _intelligenceService.GetQuickMaterialKitsAsync();
                    if (kitsRes.IsSuccess && kitsRes.Value is not null)
                    {
                        foreach (var k in kitsRes.Value) InlineQuickKits.Add(k);
                    }
                }

                var profRes = await _intelligenceService.CalculateProfitabilityAsync(_jobId);
                if (profRes.IsSuccess)
                {
                    _profitability = profRes.Value;
                    OnPropertyChanged(nameof(MaterialCostDisplay));
                    OnPropertyChanged(nameof(QuotedTotalDisplay));
                    OnPropertyChanged(nameof(EstimatedProfitDisplay));
                    OnPropertyChanged(nameof(ProfitMarginDisplay));
                    OnPropertyChanged(nameof(CollectedDisplay));
                    OnPropertyChanged(nameof(RemainingDisplay));
                }
            }

            InlineDiscoveryMaterials.Clear();
            if (disc?.Materials is not null)
            {
                foreach (var m in disc.Materials)
                {
                    int avail = m.ProductId.HasValue && _cockpitStockMap.TryGetValue(m.ProductId.Value, out var q) ? q : 0;
                    InlineDiscoveryMaterials.Add(new InlineDiscoveryMaterialRow
                    {
                        Id = m.Id,
                        ProductId = m.ProductId,
                        ProductName = m.ProductName,
                        Quantity = m.Quantity,
                        Notes = m.Notes,
                        AvailableStock = avail
                    });
                }
            }

            // 4. Calm Readiness Checklist (replaces scolding walls)
            RebuildReadinessChecklist(dto);
        }

        private void RebuildReadinessChecklist(WorkOrderWorkspaceDto dto)
        {
            ReadinessItems.Clear();
            var disc = dto.DiscoverySummary;

            void AddCheck(string title, bool isReady, int targetTab)
            {
                ReadinessItems.Add(new WorkspaceReadinessItem
                {
                    Title = title,
                    IsReady = isReady,
                    TargetTabIndex = targetTab,
                    NavigateCommand = new RelayCommand(() => ActiveTabIndex = targetTab)
                });
            }

            AddCheck("Sorumlu teknisyen atandı", !string.IsNullOrWhiteSpace(dto.AssignedTechnicianName) || !string.IsNullOrWhiteSpace(disc?.TechnicianName), 1);
            AddCheck("Teknik tespit notu girildi", !string.IsNullOrWhiteSpace(disc?.TechnicalNotes), 1);
            AddCheck("Önerilen çözüm yazıldı", !string.IsNullOrWhiteSpace(disc?.RecommendedSolution), 1);
            AddCheck("Tahmini malzeme listesi eklendi", (disc?.Materials?.Count ?? 0) > 0, 1);
            AddCheck("Teklif oluşturuldu / onaylandı", dto.QuotationSummary is not null, 2);
            AddCheck("Montaj / teslim tamamlandı", dto.DeliverySummary is not null || dto.InstallationSummary?.CompletedAt is not null, 3);
        }

        private void LoadSubTasksFromJson(string? json)
        {
            SubTasks.Clear();
            if (string.IsNullOrWhiteSpace(json) || json == "[]") return;
            try
            {
                var list = JsonSerializer.Deserialize<List<WorkOrderSubTaskItem>>(json);
                if (list is not null)
                {
                    foreach (var item in list) SubTasks.Add(item);
                }
            }
            catch (JsonException ex)
            {
                Serilog.Log.Debug(ex, "SubTasksJson parse edilemedi (JobId: {JobId})", _jobId);
            }
        }

        private string SerializeSubTasks() =>
            SubTasks.Count == 0 ? "[]" : JsonSerializer.Serialize(SubTasks.ToList());

        private static string CurrentActor() =>
            string.IsNullOrWhiteSpace(App.CurrentUser?.Username) ? "Sistem" : App.CurrentUser.Username;

        private static string FormatMoney(decimal amount) =>
            $"{FinancialCalculator.RoundMoney(amount).ToString("N2", CultureInfo.GetCultureInfo("tr-TR"))} ₺";
    }
}
