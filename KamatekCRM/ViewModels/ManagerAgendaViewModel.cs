using KamatekCrm.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.CustomerInteractions;
using KamatekCrm.ApplicationCore.DTOs.Users;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Events;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace KamatekCrm.ViewModels
{
    public enum AgendaFilterType
    {
        All,
        Overdue,
        Today,
        Critical,
        AssignedToMe
    }

    public partial class ManagerAgendaViewModel : ViewModelBase
    {
        private readonly ICustomerInteractionReadService _readService;
        private readonly ICustomerInteractionCommandService _commandService;
        private readonly IServiceJobCommandService _serviceJobCommandService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private readonly IAuthService _authService;
        private readonly NavigationService _navigationService;
        private readonly IUserAppService _userAppService;
        private readonly IServiceProvider _serviceProvider;

        private CustomerInteractionDto? _selectedAgendaItem;
        private readonly List<CustomerInteractionDto> _allAgendaItems = new();
        private AgendaFilterType _selectedFilter = AgendaFilterType.All;
        private UserListItemDto? _selectedUser;

        // ===== KPI SAYAÇLARI =====
        private int _overdueCount;
        private int _todayCount;
        private int _criticalCount;
        private int _totalPendingCount;
        private int _completedCount;

        public ObservableCollection<CustomerInteractionDto> AgendaItems { get; } = new();
        public ObservableCollection<UserListItemDto> Users { get; } = new();
        private IDisposable? _interactionChangedSubscription;

        private static readonly UserListItemDto AllUsersOption = new()
        {
            Id = 0,
            AdSoyad = "Tüm Personel"
        };

        public CustomerInteractionDto? SelectedAgendaItem
        {
            get => _selectedAgendaItem;
            set
            {
                if (SetProperty(ref _selectedAgendaItem, value))
                {
                    OnPropertyChanged(nameof(HasSelectedItem));
                    MarkAsSeenCommand.NotifyCanExecuteChanged();
                    ResolveAgendaCommand.NotifyCanExecuteChanged();
                    CompleteItemCommand.NotifyCanExecuteChanged();
                    SnoozeItemCommand.NotifyCanExecuteChanged();
                    NavigateToSourceCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HasSelectedItem => SelectedAgendaItem != null;
        public bool IsAdmin => _authService.IsAdmin;

        public AgendaFilterType SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (SetProperty(ref _selectedFilter, value))
                {
                    ApplyFilter();
                }
            }
        }

        public UserListItemDto? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    ApplyFilter();
                }
            }
        }

        public int OverdueCount
        {
            get => _overdueCount;
            set => SetProperty(ref _overdueCount, value);
        }

        public int TodayCount
        {
            get => _todayCount;
            set => SetProperty(ref _todayCount, value);
        }

        public int CriticalCount
        {
            get => _criticalCount;
            set => SetProperty(ref _criticalCount, value);
        }

        public int TotalPendingCount
        {
            get => _totalPendingCount;
            set => SetProperty(ref _totalPendingCount, value);
        }

        public int CompletedCount
        {
            get => _completedCount;
            set => SetProperty(ref _completedCount, value);
        }

        public ManagerAgendaViewModel(
            ICustomerInteractionReadService readService,
            ICustomerInteractionCommandService commandService,
            IServiceJobCommandService serviceJobCommandService,
            IToastService toastService,
            ILoadingService loadingService,
            IAuthService authService,
            NavigationService navigationService,
            IUserAppService userAppService,
            IServiceProvider serviceProvider)
        {
            _readService = readService;
            _commandService = commandService;
            _serviceJobCommandService = serviceJobCommandService;
            _toastService = toastService;
            _loadingService = loadingService;
            _authService = authService;
            _navigationService = navigationService;
            _userAppService = userAppService;
            _serviceProvider = serviceProvider;

            _interactionChangedSubscription = EventAggregator.Instance.Subscribe<CustomerInteractionChangedEvent>(OnInteractionChanged);

            LoadUsersAsync().SafeFireAndForget("ManagerAgendaViewModel.LoadUsersAsync");
            LoadAgendaAsync().SafeFireAndForget("ManagerAgendaViewModel.LoadAgendaAsync");
        }

        private void OnInteractionChanged(CustomerInteractionChangedEvent evt)
        {
            Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                await LoadAgendaAsync();
            });
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                var res = await _userAppService.GetAllAsync();
                if (res.IsSuccess && res.Value != null)
                {
                    Users.Clear();
                    Users.Add(AllUsersOption);
                    foreach (var u in res.Value.Where(x => x.IsActive))
                    {
                        Users.Add(u);
                    }

                    if (SelectedUser == null)
                    {
                        SelectedUser = AllUsersOption;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Personel listesi yüklenemedi: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task LoadAgendaAsync()
        {
            _loadingService.Show();
            try
            {
                var res = await _readService.GetManagerAgendaAsync();
                if (res.IsSuccess && res.Value != null)
                {
                    _allAgendaItems.Clear();
                    _allAgendaItems.AddRange(res.Value);

                    ApplyFilter();
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Hata", ex.Message ?? "Gündem yüklenemedi.");
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        private IEnumerable<CustomerInteractionDto> GetRoleAndUserScopedItems()
        {
            var query = _allAgendaItems.AsEnumerable();

            if (!IsAdmin)
            {
                var currentUserId = _authService.CurrentUser?.Id;
                if (currentUserId.HasValue)
                {
                    query = query.Where(x => !x.AssignedToUserId.HasValue || x.AssignedToUserId == currentUserId.Value);
                }
            }
            else if (SelectedUser != null && SelectedUser.Id > 0)
            {
                query = query.Where(x => x.AssignedToUserId == SelectedUser.Id);
            }

            return query;
        }

        private void UpdateMetrics(IReadOnlyCollection<CustomerInteractionDto> scopedItems)
        {
            var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

            OverdueCount = scopedItems.Count(x => x.IsOverdue);
            TodayCount = scopedItems.Count(x => !x.IsOverdue && x.FollowUpDate.HasValue && x.FollowUpDate.Value.Date == todayUtc);
            CriticalCount = scopedItems.Count(x => x.PriorityLevel == "Critical" || x.Priority == InteractionPriority.Critical);
            TotalPendingCount = scopedItems.Count;
        }

        private void ApplyFilter()
        {
            var scopedList = GetRoleAndUserScopedItems().ToList();
            UpdateMetrics(scopedList);

            var query = scopedList.AsEnumerable();
            var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

            // Durum / Zaman filtrelemesi
            switch (SelectedFilter)
            {
                case AgendaFilterType.Overdue:
                    query = query.Where(x => x.IsOverdue);
                    break;
                case AgendaFilterType.Today:
                    query = query.Where(x => !x.IsOverdue && x.FollowUpDate.HasValue && x.FollowUpDate.Value.Date == todayUtc);
                    break;
                case AgendaFilterType.Critical:
                    query = query.Where(x => x.PriorityLevel == "Critical" || x.Priority == InteractionPriority.Critical);
                    break;
                case AgendaFilterType.AssignedToMe:
                    var myId = _authService.CurrentUser?.Id;
                    if (myId.HasValue)
                        query = query.Where(x => x.AssignedToUserId == myId.Value);
                    break;
                case AgendaFilterType.All:
                default:
                    break;
            }

            AgendaItems.Clear();
            foreach (var item in query)
            {
                AgendaItems.Add(item);
            }

            if (SelectedAgendaItem != null && !AgendaItems.Contains(SelectedAgendaItem))
            {
                SelectedAgendaItem = AgendaItems.FirstOrDefault();
            }
        }

        [RelayCommand]
        public void SelectFilter(string filterName)
        {
            if (Enum.TryParse<AgendaFilterType>(filterName, out var filter))
            {
                SelectedFilter = filter;
            }
        }

        private bool CanActOnAgenda() => SelectedAgendaItem != null && SelectedAgendaItem.Status != InteractionStatus.Completed;

        private bool CanSnoozeOrMarkSeen() =>
            CanActOnAgenda() &&
            SelectedAgendaItem?.SourceEntityId is > 0 &&
            (SelectedAgendaItem.SourceType == "Interaction" || SelectedAgendaItem.SourceType == "ServiceJob");

        [RelayCommand(CanExecute = nameof(CanActOnAgenda))]
        public async Task CompleteItemAsync()
        {
            if (SelectedAgendaItem == null) return;
            _loadingService.Show();
            try
            {
                var current = SelectedAgendaItem;
                if (current.SourceType == "ServiceJob" && current.SourceEntityId.HasValue)
                {
                    var changeRes = await _serviceJobCommandService.ChangeStatusAsync(
                        current.SourceEntityId.Value,
                        JobStatus.Completed,
                        _authService.CurrentUser?.Username ?? "Yönetici");

                    if (changeRes.IsSuccess)
                    {
                        _toastService.ShowSuccess("Tamamlandı", $"{current.InteractionNumber} iş emri tamamlandı.");
                    }
                    else
                    {
                        _toastService.ShowError("Hata", changeRes.Error ?? "İş emri tamamlanamadı.");
                        return;
                    }
                }
                else
                {
                    // Interaction
                    var dto = new UpdateCustomerInteractionStatusDto
                    {
                        InteractionId = current.SourceEntityId ?? current.Id,
                        NewStatus = InteractionStatus.Completed,
                        Reason = "Yönetici gündeminden tamamlandı.",
                        ResolutionNotes = $"Yönetici tarafından sonuçlandırıldı ({DateTime.UtcNow:dd.MM.yyyy HH:mm})."
                    };

                    var res = await _commandService.UpdateStatusAsync(dto);
                    if (res.IsSuccess)
                    {
                        _toastService.ShowSuccess("Sonuçlandırıldı", $"{current.InteractionNumber} tamamlandı.");
                        EventAggregator.Instance.Publish(new CustomerInteractionChangedEvent(
                            current.SourceEntityId ?? current.Id,
                            current.InteractionNumber,
                            current.CustomerId,
                            InteractionChangeType.StatusChanged));
                    }
                    else
                    {
                        _toastService.ShowError("Hata", res.Error ?? "Hata oluştu.");
                        return;
                    }
                }

                CompletedCount++;
                _allAgendaItems.Remove(current);
                AgendaItems.Remove(current);
                UpdateMetrics(GetRoleAndUserScopedItems().ToList());
                SelectedAgendaItem = AgendaItems.FirstOrDefault();
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        [RelayCommand(CanExecute = nameof(CanSnoozeOrMarkSeen))]
        public async Task SnoozeItemAsync(string? daysParam)
        {
            if (SelectedAgendaItem == null || !CanSnoozeOrMarkSeen()) return;
            int days = 1;
            if (int.TryParse(daysParam, out var d) && d > 0) days = d;

            _loadingService.Show();
            try
            {
                var current = SelectedAgendaItem;
                var newDateUtc = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(days), DateTimeKind.Utc);
                string actor = _authService.CurrentUser?.Username ?? "Yönetici";
                string reason = $"Yönetici gündeminden +{days} gün ertelendi.";

                if (current.SourceType == "Interaction")
                {
                    var res = await _commandService.RescheduleAsync(
                        current.SourceEntityId ?? current.Id,
                        newDateUtc,
                        reason);

                    if (res.IsSuccess)
                    {
                        _toastService.ShowSuccess("Ertelendi", $"{current.InteractionNumber} {newDateUtc:dd.MM.yyyy} tarihine ertelendi.");
                        EventAggregator.Instance.Publish(new CustomerInteractionChangedEvent(
                            current.SourceEntityId ?? current.Id,
                            current.InteractionNumber,
                            current.CustomerId,
                            InteractionChangeType.Updated));
                        await LoadAgendaAsync();
                    }
                    else
                    {
                        _toastService.ShowError("Hata", res.Error ?? "Erteleme işlemi başarısız.");
                    }
                }
                else if (current.SourceType == "ServiceJob" && current.SourceEntityId.HasValue)
                {
                    bool isSla = current.InteractionNumber.StartsWith("SLA-", StringComparison.OrdinalIgnoreCase);
                    var res = await _serviceJobCommandService.SnoozeAgendaItemAsync(
                        current.SourceEntityId.Value,
                        newDateUtc,
                        isSla,
                        actor,
                        reason);

                    if (res.IsSuccess)
                    {
                        _toastService.ShowSuccess("Ertelendi", $"{current.InteractionNumber} kalıcı olarak {newDateUtc:dd.MM.yyyy} tarihine ertelendi.");
                        await LoadAgendaAsync();
                    }
                    else
                    {
                        _toastService.ShowError("Hata", res.Error ?? "İş emri ertelenemedi.");
                    }
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Hata", ex.Message ?? "Erteleme hatası.");
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        [RelayCommand]
        public void NavigateToSource()
        {
            if (SelectedAgendaItem == null) return;
            var current = SelectedAgendaItem;

            if (current.CustomerId.HasValue && current.CustomerId.Value > 0)
            {
                var custVm = _navigationService.NavigateTo<CustomerDetailViewModel>();
                custVm.Initialize(current.CustomerId.Value);
            }
            else if (current.SourceType == "ServiceJob")
            {
                var jobVm = _navigationService.NavigateTo<ServiceJobViewModel>();
                if (current.SourceEntityId.HasValue)
                {
                    jobVm.FocusJobByIdAsync(current.SourceEntityId.Value).SafeFireAndForget("ManagerAgendaViewModel.jobVm.FocusJobByIdAsync");
                }
            }
            else
            {
                _navigationService.NavigateTo<CustomerInteractionsViewModel>();
            }
        }

        [RelayCommand(CanExecute = nameof(CanSnoozeOrMarkSeen))]
        public async Task MarkAsSeenAsync()
        {
            if (SelectedAgendaItem == null || !CanSnoozeOrMarkSeen()) return;
            _loadingService.Show();
            try
            {
                var current = SelectedAgendaItem;
                string actor = _authService.CurrentUser?.Username ?? "Yönetici";

                if (current.SourceType == "Interaction")
                {
                    var dto = new UpdateCustomerInteractionStatusDto
                    {
                        InteractionId = current.SourceEntityId ?? current.Id,
                        NewStatus = InteractionStatus.Seen,
                        Reason = "Yönetici tarafından incelendi."
                    };

                    var res = await _commandService.UpdateStatusAsync(dto);
                    if (res.IsSuccess)
                    {
                        _toastService.ShowSuccess("İncelendi", $"{current.InteractionNumber} incelendi olarak işaretlendi.");
                        current.Status = InteractionStatus.Seen;
                        OnPropertyChanged(nameof(SelectedAgendaItem));
                    }
                    else
                    {
                        _toastService.ShowError("Hata", res.Error ?? "İncelendi olarak işaretlenemedi.");
                    }
                }
                else if (current.SourceType == "ServiceJob" && current.SourceEntityId.HasValue)
                {
                    var res = await _serviceJobCommandService.MarkAgendaItemSeenAsync(
                        current.SourceEntityId.Value,
                        actor,
                        $"{current.InteractionNumber} yönetici gündeminden incelendi.");

                    if (res.IsSuccess)
                    {
                        _toastService.ShowSuccess("İncelendi", $"{current.InteractionNumber} iş emri tarihçesine incelendi olarak kaydedildi.");
                        current.Status = InteractionStatus.Seen;
                        OnPropertyChanged(nameof(SelectedAgendaItem));
                    }
                    else
                    {
                        _toastService.ShowError("Hata", res.Error ?? "İşlem kaydedilemedi.");
                    }
                }
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        [RelayCommand(CanExecute = nameof(CanActOnAgenda))]
        public async Task ResolveAgendaAsync()
        {
            await CompleteItemAsync();
        }
    }
}
