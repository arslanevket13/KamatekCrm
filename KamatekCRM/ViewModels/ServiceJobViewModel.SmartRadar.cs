using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Helpers;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.ViewModels
{
    public sealed class WorkOrderKanbanCard
    {
        public required ServiceJobRowDto Row { get; init; }
        public string CodeDisplay => $"#{Row.Id}";
        public string CustomerName => Row.CustomerFullName;
        public string Description => string.IsNullOrWhiteSpace(Row.Description) ? "Açıklama girilmemiş" : Row.Description;
        public string CategoryDisplay { get; init; } = string.Empty;
        public string TechnicianDisplay => string.IsNullOrWhiteSpace(Row.AssignedTechnician) ? "Teknisyen atanmadı" : Row.AssignedTechnician;
        public string DateDisplay => Row.ScheduledDate.HasValue
            ? Row.ScheduledDate.Value.ToLocalTime().ToString("dd.MM HH:mm")
            : Row.CreatedDate.ToLocalTime().ToString("dd.MM.yyyy");
        public string TotalDisplay => Row.TotalAmount > 0
            ? $"{FinancialCalculator.RoundMoney(Row.TotalAmount).ToString("N0", CultureInfo.GetCultureInfo("tr-TR"))} ₺"
            : "Tutar yok";
        public string? PrimaryAlertText { get; init; }
        public WorkOrderSeverity? PrimaryAlertSeverity { get; init; }
        public bool HasAlert => !string.IsNullOrWhiteSpace(PrimaryAlertText);
        public bool IsCriticalAlert => PrimaryAlertSeverity == WorkOrderSeverity.Critical;
        public bool IsWarningAlert => PrimaryAlertSeverity == WorkOrderSeverity.Warning;
    }

    public sealed class FieldCalendarSlotItem
    {
        public required ServiceJobRowDto Row { get; init; }
        public string CodeDisplay => $"#{Row.Id}";
        public string TimeText { get; init; } = string.Empty;
        public string TechnicianName { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public string StageLabel { get; init; } = string.Empty;
        public bool HasOverlap { get; init; }
        public string OverlapText => HasOverlap ? "Çakışma Riski" : string.Empty;
    }

    public sealed class FieldCalendarDayGroup
    {
        public DateTime Date { get; init; }
        public string DayTitle { get; init; } = string.Empty;
        public bool IsToday { get; init; }
        public ObservableCollection<FieldCalendarSlotItem> Slots { get; } = new();
        public bool HasSlots => Slots.Count > 0;
        public int SlotCount => Slots.Count;
    }

    public partial class ServiceJobViewModel
    {
        private IWorkOrderIntelligenceService? _intelligenceService;

        // ── 3-Mode View Switcher (Default: Kanban) ──
        private string _activeViewMode = "Kanban";
        public string ActiveViewMode
        {
            get => _activeViewMode;
            set
            {
                if (SetProperty(ref _activeViewMode, value))
                {
                    OnPropertyChanged(nameof(IsKanbanView));
                    OnPropertyChanged(nameof(IsTableView));
                    OnPropertyChanged(nameof(IsCalendarView));
                }
            }
        }

        public bool IsKanbanView => string.Equals(ActiveViewMode, "Kanban", StringComparison.OrdinalIgnoreCase);
        public bool IsTableView => string.Equals(ActiveViewMode, "Table", StringComparison.OrdinalIgnoreCase);
        public bool IsCalendarView => string.Equals(ActiveViewMode, "Calendar", StringComparison.OrdinalIgnoreCase);

        [RelayCommand]
        private void SetViewMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return;
            ActiveViewMode = mode;
        }

        // ── 5 Smart-Filter KPI Radar Cards ──
        private string _activeRadarFilter = "All";
        public string ActiveRadarFilter
        {
            get => _activeRadarFilter;
            set
            {
                if (SetProperty(ref _activeRadarFilter, value))
                {
                    OnPropertyChanged(nameof(IsRadarTodayActive));
                    OnPropertyChanged(nameof(IsRadarUnassignedActive));
                    OnPropertyChanged(nameof(IsRadarWaitingQuoteActive));
                    OnPropertyChanged(nameof(IsRadarOverdueActive));
                    OnPropertyChanged(nameof(IsRadarWaitingPaymentActive));
                    ServiceJobsView?.Refresh();
                    RebuildSmartRadarCollections();
                }
            }
        }

        public bool IsRadarTodayActive => ActiveRadarFilter == "Today";
        public bool IsRadarUnassignedActive => ActiveRadarFilter == "Unassigned";
        public bool IsRadarWaitingQuoteActive => ActiveRadarFilter == "WaitingQuote";
        public bool IsRadarOverdueActive => ActiveRadarFilter == "Overdue";
        public bool IsRadarWaitingPaymentActive => ActiveRadarFilter == "WaitingPayment";

        private int _radarTodayCount;
        public int RadarTodayCount { get => _radarTodayCount; set => SetProperty(ref _radarTodayCount, value); }

        private int _radarUnassignedCount;
        public int RadarUnassignedCount { get => _radarUnassignedCount; set => SetProperty(ref _radarUnassignedCount, value); }

        private int _radarWaitingQuoteCount;
        public int RadarWaitingQuoteCount { get => _radarWaitingQuoteCount; set => SetProperty(ref _radarWaitingQuoteCount, value); }

        private int _radarOverdueCount;
        public int RadarOverdueCount { get => _radarOverdueCount; set => SetProperty(ref _radarOverdueCount, value); }

        private int _radarWaitingPaymentCount;
        public int RadarWaitingPaymentCount { get => _radarWaitingPaymentCount; set => SetProperty(ref _radarWaitingPaymentCount, value); }

        [RelayCommand]
        private void SelectRadarFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return;
            ActiveRadarFilter = string.Equals(ActiveRadarFilter, filter, StringComparison.OrdinalIgnoreCase)
                ? "All"
                : filter;
        }

        // ── Kanban 5 Columns ──
        public ObservableCollection<WorkOrderKanbanCard> KanbanRequestCards { get; } = new();
        public ObservableCollection<WorkOrderKanbanCard> KanbanDiscoveryCards { get; } = new();
        public ObservableCollection<WorkOrderKanbanCard> KanbanQuoteCards { get; } = new();
        public ObservableCollection<WorkOrderKanbanCard> KanbanInstallationCards { get; } = new();
        public ObservableCollection<WorkOrderKanbanCard> KanbanDeliveryCards { get; } = new();

        public int KanbanRequestCount => KanbanRequestCards.Count;
        public int KanbanDiscoveryCount => KanbanDiscoveryCards.Count;
        public int KanbanQuoteCount => KanbanQuoteCards.Count;
        public int KanbanInstallationCount => KanbanInstallationCards.Count;
        public int KanbanDeliveryCount => KanbanDeliveryCards.Count;

        // ── Field Calendar ──
        private DateTime _calendarWeekStart = GetStartOfWeek(DateTime.Today);
        public ObservableCollection<FieldCalendarDayGroup> CalendarWeekDays { get; } = new();

        public string CalendarWeekRangeLabel =>
            $"{_calendarWeekStart:dd MMM} – {_calendarWeekStart.AddDays(6):dd MMM yyyy}";

        [RelayCommand]
        private void PreviousCalendarWeek()
        {
            _calendarWeekStart = _calendarWeekStart.AddDays(-7);
            OnPropertyChanged(nameof(CalendarWeekRangeLabel));
            RebuildCalendarWeek();
        }

        [RelayCommand]
        private void TodayCalendarWeek()
        {
            _calendarWeekStart = GetStartOfWeek(DateTime.Today);
            OnPropertyChanged(nameof(CalendarWeekRangeLabel));
            RebuildCalendarWeek();
        }

        [RelayCommand]
        private void NextCalendarWeek()
        {
            _calendarWeekStart = _calendarWeekStart.AddDays(7);
            OnPropertyChanged(nameof(CalendarWeekRangeLabel));
            RebuildCalendarWeek();
        }

        // ── Smart Create Form: Customer Memory, Discovery Checklist & Quick Kits ──
        private WorkOrderCustomerMemoryDto? _customerMemory;
        public WorkOrderCustomerMemoryDto? CustomerMemory
        {
            get => _customerMemory;
            private set
            {
                if (SetProperty(ref _customerMemory, value))
                {
                    OnPropertyChanged(nameof(HasCustomerMemory));
                    OnPropertyChanged(nameof(HasRepeatWarrantyWarning));
                    OnPropertyChanged(nameof(RepeatWarrantyWarningMessage));
                    OnPropertyChanged(nameof(CustomerOpenBalanceDisplay));
                    OnPropertyChanged(nameof(CustomerPastJobsDisplay));
                }
            }
        }

        public bool HasCustomerMemory => CustomerMemory is not null;
        public bool HasRepeatWarrantyWarning => CustomerMemory?.HasRepeatWarrantyWarning == true;
        public string RepeatWarrantyWarningMessage => CustomerMemory?.RepeatWarrantyWarningMessage ?? string.Empty;
        public string CustomerOpenBalanceDisplay => CustomerMemory is not null
            ? $"{CustomerMemory.OpenBalance.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"))} ₺"
            : "0,00 ₺";
        public string CustomerPastJobsDisplay => CustomerMemory is not null
            ? $"{CustomerMemory.PastJobCount} geçmiş iş emri"
            : "Müşteri seçilmedi";

        public ObservableCollection<DiscoveryChecklistItem> DiscoveryChecklistItems { get; } = new();
        public ObservableCollection<QuickMaterialKitDto> QuickMaterialKits { get; } = new();

        private string _newChecklistItemText = string.Empty;
        public string NewChecklistItemText
        {
            get => _newChecklistItemText;
            set
            {
                if (SetProperty(ref _newChecklistItemText, value))
                {
                    AddChecklistItemCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool CanAddChecklistItem() => !string.IsNullOrWhiteSpace(NewChecklistItemText);

        [RelayCommand(CanExecute = nameof(CanAddChecklistItem))]
        private void AddChecklistItem()
        {
            string title = NewChecklistItemText.Trim();
            if (string.IsNullOrWhiteSpace(title)) return;
            DiscoveryChecklistItems.Add(new DiscoveryChecklistItem { Title = title });
            NewChecklistItemText = string.Empty;
        }

        [RelayCommand]
        private void RemoveChecklistItem(DiscoveryChecklistItem? item)
        {
            if (item is null) return;
            DiscoveryChecklistItems.Remove(item);
        }

        private string _selectedProductStockText = string.Empty;
        public string SelectedProductStockText
        {
            get => _selectedProductStockText;
            private set
            {
                if (SetProperty(ref _selectedProductStockText, value))
                {
                    OnPropertyChanged(nameof(HasSelectedProductStockText));
                }
            }
        }

        public bool HasSelectedProductStockText => !string.IsNullOrWhiteSpace(SelectedProductStockText);

        private bool _isSelectedProductLowStock;
        public bool IsSelectedProductLowStock
        {
            get => _isSelectedProductLowStock;
            private set => SetProperty(ref _isSelectedProductLowStock, value);
        }

        private Dictionary<int, int> _productStockCache = new();

        [RelayCommand]
        private void ApplyMaterialKit(QuickMaterialKitDto? kit)
        {
            if (kit is null) return;

            int added = 0;
            int missing = 0;
            foreach (var item in kit.Items)
            {
                if (!item.ProductId.HasValue)
                {
                    missing++;
                    continue;
                }

                var product = Products.FirstOrDefault(p => p.Id == item.ProductId.Value);
                if (product is null)
                {
                    missing++;
                    continue;
                }

                var existing = CurrentJobItems.FirstOrDefault(ci => ci.ProductId == product.Id);
                if (existing is not null)
                {
                    existing.QuantityUsed += item.Quantity;
                }
                else
                {
                    CurrentJobItems.Add(new ServiceJobMaterialDto(
                        0,
                        product.Id,
                        product.ProductName,
                        item.Quantity,
                        product.SalePrice,
                        product.PurchasePrice));
                }
                added++;
            }

            UpdateTotals();
            if (added > 0)
            {
                string suffix = missing > 0 ? $" ({missing} kalem stokta eşleşmedi)" : string.Empty;
                _toastService?.ShowSuccess($"'{kit.Name}' paketinden {added} kalem eklendi.{suffix}");
            }
            else
            {
                _toastService?.ShowWarning($"'{kit.Name}' paketi için eşleşen stok ürünü bulunamadı.");
            }
        }

        [RelayCommand]
        private void OnCategoryToggled()
        {
            OnPropertyChanged(nameof(SummaryCategory));
            RefreshDiscoveryChecklistForSelectedCategories();
            LoadCustomerMemoryAsync().SafeFireAndForget("ServiceJobViewModel.LoadCustomerMemoryAsync");
        }

        private void InitializeSmartRadarHooks()
        {
            // Kategori değişiklikleri CheckBox Command (OnCategoryToggledCommand) ile tetiklenir.
        }

        private async Task InitializeSmartFormAsync()
        {
            RefreshDiscoveryChecklistForSelectedCategories();
            if (_intelligenceService is null) return;

            try
            {
                var kitsResult = await _intelligenceService.GetQuickMaterialKitsAsync();
                if (kitsResult.IsSuccess && kitsResult.Value is not null)
                {
                    QuickMaterialKits.Clear();
                    foreach (var kit in kitsResult.Value)
                    {
                        QuickMaterialKits.Add(kit);
                    }
                }

                var productIds = Products.Select(p => p.Id).ToList();
                if (productIds.Count > 0)
                {
                    var stockResult = await _intelligenceService.GetStockAvailabilityMapAsync(productIds);
                    if (stockResult.IsSuccess && stockResult.Value is not null)
                    {
                        _productStockCache = new Dictionary<int, int>(stockResult.Value);
                        UpdateSelectedProductStockBadge();
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Akıllı form verileri yüklenemedi.");
            }
        }

        private async Task LoadCustomerMemoryAsync()
        {
            if (_intelligenceService is null || SelectedCustomer is null)
            {
                CustomerMemory = null;
                return;
            }

            try
            {
                var selectedCategories = CategoryItems?
                    .Where(c => c.IsSelected)
                    .Select(c => c.Category.ToString())
                    .ToList() ?? new List<string>();

                var res = await _intelligenceService.GetCustomerMemoryAsync(SelectedCustomer.Id, selectedCategories);
                CustomerMemory = res.IsSuccess ? res.Value : null;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Müşteri hafızası yüklenirken hata oluştu.");
            }
        }

        private void RefreshDiscoveryChecklistForSelectedCategories()
        {
            if (_intelligenceService is null) return;
            var firstSelected = CategoryItems?.FirstOrDefault(c => c.IsSelected)?.Category.ToString() ?? "CCTV";
            var template = _intelligenceService.GetCategoryDiscoveryTemplates(firstSelected);

            // Kullanıcı henüz özel işaretleme yapmadıysa şablonu tazele
            bool hasChecked = DiscoveryChecklistItems.Any(i => i.IsChecked);
            if (!hasChecked)
            {
                DiscoveryChecklistItems.Clear();
                foreach (var item in template)
                {
                    DiscoveryChecklistItems.Add(item);
                }
            }
        }

        private void UpdateSelectedProductStockBadge()
        {
            if (SelectedProductToAdd is null)
            {
                SelectedProductStockText = string.Empty;
                IsSelectedProductLowStock = false;
                return;
            }

            int qty = _productStockCache.TryGetValue(SelectedProductToAdd.Id, out var avail) ? avail : 0;
            if (qty <= 0)
            {
                SelectedProductStockText = "[Stok Yok]";
                IsSelectedProductLowStock = true;
            }
            else if (qty < QuantityToAdd)
            {
                SelectedProductStockText = $"[Eksik: {QuantityToAdd - qty} | Stokta: {qty}]";
                IsSelectedProductLowStock = true;
            }
            else
            {
                SelectedProductStockText = $"[Stokta: {qty}]";
                IsSelectedProductLowStock = false;
            }
        }

        private bool MatchesActiveRadarFilter(ServiceJobRowDto row)
        {
            var now = DateTime.UtcNow;
            var todayLocal = DateTime.Today;
            bool isClosed = row.Status is JobStatus.Completed or JobStatus.Delivered or JobStatus.Cancelled or JobStatus.Rejected;

            return ActiveRadarFilter switch
            {
                "Today" => !isClosed && (
                    (row.ScheduledDate.HasValue && row.ScheduledDate.Value.ToLocalTime().Date == todayLocal) ||
                    (row.SlaDeadline.HasValue && row.SlaDeadline.Value.ToLocalTime().Date == todayLocal) ||
                    (row.NextFollowUpUtc.HasValue && row.NextFollowUpUtc.Value.ToLocalTime().Date == todayLocal)),
                "Unassigned" => !isClosed && !row.AssignedTechnicianId.HasValue && string.IsNullOrWhiteSpace(row.AssignedTechnician),
                "WaitingQuote" => row.Status is JobStatus.DiscoveryCompleted or JobStatus.Quoting or JobStatus.ConvertedToQuote or JobStatus.WaitingForApproval,
                "Overdue" => !isClosed && (
                    (row.SlaDeadline.HasValue && row.SlaDeadline.Value < now) ||
                    (row.NextFollowUpUtc.HasValue && row.NextFollowUpUtc.Value <= now) ||
                    (row.ScheduledDate.HasValue && row.ScheduledDate.Value < now.AddHours(-24))),
                "WaitingPayment" => row.Status is JobStatus.InstallationCompleted or JobStatus.Delivered && !row.IsPaid && row.TotalAmount > 0,
                _ => true
            };
        }

        private void RebuildSmartRadarCollections()
        {
            var now = DateTime.UtcNow;
            var todayLocal = DateTime.Today;

            int todayCount = 0;
            int unassignedCount = 0;
            int waitingQuoteCount = 0;
            int overdueCount = 0;
            int waitingPaymentCount = 0;

            foreach (var row in ServiceJobs)
            {
                bool isClosed = row.Status is JobStatus.Completed or JobStatus.Delivered or JobStatus.Cancelled or JobStatus.Rejected;
                if (!isClosed && (
                    (row.ScheduledDate.HasValue && row.ScheduledDate.Value.ToLocalTime().Date == todayLocal) ||
                    (row.SlaDeadline.HasValue && row.SlaDeadline.Value.ToLocalTime().Date == todayLocal) ||
                    (row.NextFollowUpUtc.HasValue && row.NextFollowUpUtc.Value.ToLocalTime().Date == todayLocal)))
                {
                    todayCount++;
                }

                if (!isClosed && !row.AssignedTechnicianId.HasValue && string.IsNullOrWhiteSpace(row.AssignedTechnician))
                {
                    unassignedCount++;
                }

                if (row.Status is JobStatus.DiscoveryCompleted or JobStatus.Quoting or JobStatus.ConvertedToQuote or JobStatus.WaitingForApproval)
                {
                    waitingQuoteCount++;
                }

                if (!isClosed && (
                    (row.SlaDeadline.HasValue && row.SlaDeadline.Value < now) ||
                    (row.NextFollowUpUtc.HasValue && row.NextFollowUpUtc.Value <= now) ||
                    (row.ScheduledDate.HasValue && row.ScheduledDate.Value < now.AddHours(-24))))
                {
                    overdueCount++;
                }

                if (row.Status is JobStatus.InstallationCompleted or JobStatus.Delivered && !row.IsPaid && row.TotalAmount > 0)
                {
                    waitingPaymentCount++;
                }
            }

            RadarTodayCount = todayCount;
            RadarUnassignedCount = unassignedCount;
            RadarWaitingQuoteCount = waitingQuoteCount;
            RadarOverdueCount = overdueCount;
            RadarWaitingPaymentCount = waitingPaymentCount;

            KanbanRequestCards.Clear();
            KanbanDiscoveryCards.Clear();
            KanbanQuoteCards.Clear();
            KanbanInstallationCards.Clear();
            KanbanDeliveryCards.Clear();

            foreach (var row in ServiceJobs.Where(MatchesActiveRadarFilter))
            {
                if (row.Status is JobStatus.Cancelled or JobStatus.Rejected) continue;

                var alerts = _intelligenceService?.EvaluateJobAlerts(row, now) ?? Array.Empty<WorkOrderAlertBadgeDto>();
                var topAlert = alerts.OrderByDescending(a => a.Severity).FirstOrDefault();

                var card = new WorkOrderKanbanCard
                {
                    Row = row,
                    CategoryDisplay = FormatJobCategoryTurkish(row.JobCategory),
                    PrimaryAlertText = topAlert?.Text,
                    PrimaryAlertSeverity = topAlert?.Severity
                };

                switch (row.Status)
                {
                    case JobStatus.Pending:
                    case JobStatus.DiscoveryRequest:
                    case JobStatus.PendingDiscovery:
                        KanbanRequestCards.Add(card);
                        break;
                    case JobStatus.DiscoveryCompleted:
                        KanbanDiscoveryCards.Add(card);
                        break;
                    case JobStatus.ConvertedToQuote:
                    case JobStatus.Quoting:
                    case JobStatus.WaitingForApproval:
                        KanbanQuoteCards.Add(card);
                        break;
                    case JobStatus.InProgress:
                    case JobStatus.WaitingForParts:
                    case JobStatus.InstallationPlanned:
                        KanbanInstallationCards.Add(card);
                        break;
                    case JobStatus.InstallationCompleted:
                    case JobStatus.Delivered:
                    case JobStatus.Completed:
                        KanbanDeliveryCards.Add(card);
                        break;
                }
            }

            OnPropertyChanged(nameof(KanbanRequestCount));
            OnPropertyChanged(nameof(KanbanDiscoveryCount));
            OnPropertyChanged(nameof(KanbanQuoteCount));
            OnPropertyChanged(nameof(KanbanInstallationCount));
            OnPropertyChanged(nameof(KanbanDeliveryCount));

            RebuildCalendarWeek();
        }

        private void RebuildCalendarWeek()
        {
            CalendarWeekDays.Clear();
            var tr = CultureInfo.GetCultureInfo("tr-TR");
            var filtered = ServiceJobs.Where(MatchesActiveRadarFilter).ToList();

            for (int i = 0; i < 7; i++)
            {
                var date = _calendarWeekStart.AddDays(i);
                var dayGroup = new FieldCalendarDayGroup
                {
                    Date = date,
                    DayTitle = date.ToString("ddd dd MMM", tr),
                    IsToday = date.Date == DateTime.Today
                };

                var dayJobs = filtered
                    .Where(j => j.ScheduledDate.HasValue && j.ScheduledDate.Value.ToLocalTime().Date == date.Date)
                    .OrderBy(j => j.ScheduledDate!.Value)
                    .ToList();

                foreach (var job in dayJobs)
                {
                    var localTime = job.ScheduledDate!.Value.ToLocalTime();
                    bool overlap = !string.IsNullOrWhiteSpace(job.AssignedTechnician) && dayJobs.Any(other =>
                        other.Id != job.Id &&
                        string.Equals(other.AssignedTechnician, job.AssignedTechnician, StringComparison.OrdinalIgnoreCase) &&
                        Math.Abs((other.ScheduledDate!.Value.ToLocalTime() - localTime).TotalHours) < 2.0);

                    dayGroup.Slots.Add(new FieldCalendarSlotItem
                    {
                        Row = job,
                        TimeText = localTime.ToString("HH:mm"),
                        TechnicianName = string.IsNullOrWhiteSpace(job.AssignedTechnician) ? "Atanmadı" : job.AssignedTechnician,
                        CustomerName = job.CustomerFullName,
                        StageLabel = job.WorkOrderType == WorkOrderType.Discovery ? "Keşif" : "Montaj / Servis",
                        HasOverlap = overlap
                    });
                }

                CalendarWeekDays.Add(dayGroup);
            }
        }

        private string SerializeDiscoveryChecklistJson()
        {
            if (DiscoveryChecklistItems.Count == 0) return "[]";
            return JsonSerializer.Serialize(DiscoveryChecklistItems.ToList());
        }

        private static DateTime GetStartOfWeek(DateTime dt)
        {
            int diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
            return dt.AddDays(-diff).Date;
        }

        private static string FormatJobCategoryTurkish(JobCategory category) => category switch
        {
            JobCategory.CCTV => "Kamera",
            JobCategory.VideoIntercom => "Diafon",
            JobCategory.FireAlarm => "Yangın",
            JobCategory.BurglarAlarm => "Alarm",
            JobCategory.SmartHome => "Akıllı Ev",
            JobCategory.AccessControl => "Geçiş Kontrol",
            JobCategory.SatelliteSystem => "Uydu",
            JobCategory.FiberOptic => "Fiber / Ağ",
            JobCategory.Security => "Güvenlik",
            JobCategory.Network => "Ağ",
            JobCategory.Automation => "Otomasyon",
            JobCategory.Electrical => "Elektrik",
            JobCategory.Maintenance => "Bakım",
            JobCategory.Fault => "Arıza",
            _ => "Genel"
        };
    }
}
