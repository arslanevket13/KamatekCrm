using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.CustomerInteractions;
using KamatekCrm.ApplicationCore.DTOs.Users;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Events;
using KamatekCrm.Helpers;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KamatekCrm.ViewModels
{
    public enum InteractionViewMode
    {
        Feed,
        Daily,
        Weekly,
        Monthly
    }

    public enum InteractionFolder
    {
        Today,
        Overdue,
        BallInCourtUs,
        BallInCourtCustomer,
        BallInCourtManager,
        AssignedToMe,
        Completed,
        All
    }

    public class CalendarDayItemViewModel : ViewModelBase
    {
        public DateTime Date { get; set; }
        public int DayNumber => Date.Day;
        public bool IsCurrentMonth { get; set; }
        public bool IsToday { get; set; }
        
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private int _interactionCount;
        public int InteractionCount
        {
            get => _interactionCount;
            set
            {
                if (SetProperty(ref _interactionCount, value))
                {
                    OnPropertyChanged(nameof(HasInteractions));
                }
            }
        }

        private int _overdueCount;
        public int OverdueCount
        {
            get => _overdueCount;
            set
            {
                if (SetProperty(ref _overdueCount, value))
                {
                    OnPropertyChanged(nameof(HasOverdue));
                }
            }
        }

        public bool HasInteractions => InteractionCount > 0;
        public bool HasOverdue => OverdueCount > 0;
    }

    public partial class CustomerInteractionsViewModel : ViewModelBase
    {
        private readonly ICustomerInteractionReadService _readService;
        private readonly ICustomerInteractionCommandService _commandService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IAuthService _authService;
        private readonly IUserAppService _userAppService;

        private InteractionViewMode _viewMode = InteractionViewMode.Feed;
        private InteractionFolder _activeFolder = InteractionFolder.Today;
        private string _searchText = string.Empty;
        private CustomerInteractionDto? _selectedInteraction;
        private UserListItemDto? _selectedAssignedUser;

        // Mini Calendar State
        private DateTime _currentCalendarMonth = DateTime.UtcNow;
        private CalendarDayItemViewModel? _selectedCalendarDay;
        private DateTime? _selectedCalendarDate;

        // Drawer State
        private bool _isDrawerOpen;
        private InteractionDirection _drawerDirection = InteractionDirection.InboundRequest;
        private InteractionRequestType _drawerRequestType = InteractionRequestType.PriceQuote;
        private InteractionPriority _drawerPriority = InteractionPriority.Normal;
        private BallInCourtStatus _drawerBallInCourt = BallInCourtStatus.Us;
        private string _drawerCallerPhone = string.Empty;
        private string _drawerCallerName = string.Empty;
        private string _drawerSubject = string.Empty;
        private string _drawerNotes = string.Empty;
        private DateTime? _drawerFollowUpDate;
        private int? _drawerSelectedCustomerId;
        private string? _drawerSelectedCustomerName;
        private CallerContextDto? _drawerCallerContext;
        private bool _isLoadingDrawerContext;
        private string _drawerNewChecklistText = string.Empty;
        private CancellationTokenSource? _drawerLookupCts;

        // Right Panel Case Book State
        private string _newChecklistTitle = string.Empty;
        private string _newThreadNoteContent = string.Empty;
        private ThreadEntryType _selectedThreadEntryType = ThreadEntryType.Note;

        // Collections
        public ObservableCollection<CustomerInteractionDto> Interactions { get; } = new ObservableCollection<CustomerInteractionDto>();
        public ObservableCollection<CalendarDayItemViewModel> CalendarDays { get; } = new ObservableCollection<CalendarDayItemViewModel>();
        public ObservableCollection<UserListItemDto> Users { get; } = new ObservableCollection<UserListItemDto>();
        public ObservableCollection<InteractionChecklistItem> RightPanelChecklist { get; } = new ObservableCollection<InteractionChecklistItem>();
        public ObservableCollection<InteractionThreadEntry> RightPanelThread { get; } = new ObservableCollection<InteractionThreadEntry>();
        public ObservableCollection<string> DrawerChecklistItems { get; } = new ObservableCollection<string>();

        [RelayCommand]
        public void SetViewMode(string mode)
        {
            if (Enum.TryParse<InteractionViewMode>(mode, out var m))
            {
                ViewMode = m;
            }
        }

        [RelayCommand]
        public async Task SelectFolderAsync(string folder)
        {
            if (Enum.TryParse<InteractionFolder>(folder, out var f))
            {
                ActiveFolder = f;
            }
        }
        private int _countToday;
        public int CountToday { get => _countToday; set => SetProperty(ref _countToday, value); }

        private int _countOverdue;
        public int CountOverdue { get => _countOverdue; set => SetProperty(ref _countOverdue, value); }

        private int _countBallInCourtUs;
        public int CountBallInCourtUs { get => _countBallInCourtUs; set => SetProperty(ref _countBallInCourtUs, value); }

        private int _countBallInCourtCustomer;
        public int CountBallInCourtCustomer { get => _countBallInCourtCustomer; set => SetProperty(ref _countBallInCourtCustomer, value); }

        private int _countBallInCourtManager;
        public int CountBallInCourtManager { get => _countBallInCourtManager; set => SetProperty(ref _countBallInCourtManager, value); }

        private int _countAssignedToMe;
        public int CountAssignedToMe { get => _countAssignedToMe; set => SetProperty(ref _countAssignedToMe, value); }

        private int _countCompleted;
        public int CountCompleted { get => _countCompleted; set => SetProperty(ref _countCompleted, value); }

        private int _countAll;
        public int CountAll { get => _countAll; set => SetProperty(ref _countAll, value); }

        // Properties
        public InteractionViewMode ViewMode
        {
            get => _viewMode;
            set
            {
                if (SetProperty(ref _viewMode, value))
                {
                    OnPropertyChanged(nameof(IsFeedView));
                    OnPropertyChanged(nameof(IsDailyView));
                    OnPropertyChanged(nameof(IsWeeklyView));
                    OnPropertyChanged(nameof(IsMonthlyView));
                }
            }
        }

        public bool IsFeedView => ViewMode == InteractionViewMode.Feed;
        public bool IsDailyView => ViewMode == InteractionViewMode.Daily;
        public bool IsWeeklyView => ViewMode == InteractionViewMode.Weekly;
        public bool IsMonthlyView => ViewMode == InteractionViewMode.Monthly;

        public InteractionFolder ActiveFolder
        {
            get => _activeFolder;
            set
            {
                if (SetProperty(ref _activeFolder, value))
                {
                    _selectedCalendarDate = null;
                    if (_selectedCalendarDay != null)
                    {
                        _selectedCalendarDay.IsSelected = false;
                        _selectedCalendarDay = null;
                    }
                    OnPropertyChanged(nameof(SelectedCalendarDate));
                    LoadInteractionsAsync().SafeFireAndForget("CustomerInteractionsViewModel.ActiveFolderChanged");
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    LoadInteractionsAsync().SafeFireAndForget("CustomerInteractionsViewModel.SearchChanged");
                }
            }
        }

        public CustomerInteractionDto? SelectedInteraction
        {
            get => _selectedInteraction;
            set
            {
                if (SetProperty(ref _selectedInteraction, value))
                {
                    OnPropertyChanged(nameof(HasSelectedInteraction));
                    CompleteInteractionCommand.NotifyCanExecuteChanged();
                    ConvertToQuoteCommand.NotifyCanExecuteChanged();
                    ConvertToServiceJobCommand.NotifyCanExecuteChanged();
                    ConvertToFaultTicketCommand.NotifyCanExecuteChanged();
                    PopulateRightPanel();
                }
            }
        }

        public bool HasSelectedInteraction => SelectedInteraction != null;

        public UserListItemDto? SelectedAssignedUser
        {
            get => _selectedAssignedUser;
            set => SetProperty(ref _selectedAssignedUser, value);
        }

        // Mini Calendar properties
        public string CalendarMonthTitle => _currentCalendarMonth.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("tr-TR")).ToUpper(CultureInfo.GetCultureInfo("tr-TR"));

        public DateTime? SelectedCalendarDate
        {
            get => _selectedCalendarDate;
            set => SetProperty(ref _selectedCalendarDate, value);
        }

        // Slide-Over Drawer Properties
        public bool IsDrawerOpen
        {
            get => _isDrawerOpen;
            set => SetProperty(ref _isDrawerOpen, value);
        }

        public InteractionDirection DrawerDirection
        {
            get => _drawerDirection;
            set => SetProperty(ref _drawerDirection, value);
        }

        public InteractionRequestType DrawerRequestType
        {
            get => _drawerRequestType;
            set => SetProperty(ref _drawerRequestType, value);
        }

        public InteractionPriority DrawerPriority
        {
            get => _drawerPriority;
            set => SetProperty(ref _drawerPriority, value);
        }

        public BallInCourtStatus DrawerBallInCourt
        {
            get => _drawerBallInCourt;
            set => SetProperty(ref _drawerBallInCourt, value);
        }

        public string DrawerCallerPhone
        {
            get => _drawerCallerPhone;
            set
            {
                if (SetProperty(ref _drawerCallerPhone, value))
                {
                    DebouncedLookupCallerContext();
                }
            }
        }

        public string DrawerCallerName
        {
            get => _drawerCallerName;
            set => SetProperty(ref _drawerCallerName, value);
        }

        public string DrawerSubject
        {
            get => _drawerSubject;
            set => SetProperty(ref _drawerSubject, value);
        }

        public string DrawerNotes
        {
            get => _drawerNotes;
            set => SetProperty(ref _drawerNotes, value);
        }

        public DateTime? DrawerFollowUpDate
        {
            get => _drawerFollowUpDate;
            set => SetProperty(ref _drawerFollowUpDate, value);
        }

        public int? DrawerSelectedCustomerId
        {
            get => _drawerSelectedCustomerId;
            set => SetProperty(ref _drawerSelectedCustomerId, value);
        }

        public string? DrawerSelectedCustomerName
        {
            get => _drawerSelectedCustomerName;
            set => SetProperty(ref _drawerSelectedCustomerName, value);
        }

        public CallerContextDto? DrawerCallerContext
        {
            get => _drawerCallerContext;
            set
            {
                if (SetProperty(ref _drawerCallerContext, value))
                {
                    OnPropertyChanged(nameof(HasDrawerCallerContext));
                }
            }
        }

        public bool HasDrawerCallerContext => DrawerCallerContext != null;

        public bool IsLoadingDrawerContext
        {
            get => _isLoadingDrawerContext;
            set => SetProperty(ref _isLoadingDrawerContext, value);
        }

        public string DrawerNewChecklistText
        {
            get => _drawerNewChecklistText;
            set => SetProperty(ref _drawerNewChecklistText, value);
        }

        // Right Panel Case Book Properties
        public string NewChecklistTitle
        {
            get => _newChecklistTitle;
            set => SetProperty(ref _newChecklistTitle, value);
        }

        public string NewThreadNoteContent
        {
            get => _newThreadNoteContent;
            set => SetProperty(ref _newThreadNoteContent, value);
        }

        public ThreadEntryType SelectedThreadEntryType
        {
            get => _selectedThreadEntryType;
            set => SetProperty(ref _selectedThreadEntryType, value);
        }

        public Array ThreadEntryTypes => Enum.GetValues(typeof(ThreadEntryType));

        // Constructor
        public CustomerInteractionsViewModel(
            ICustomerInteractionReadService readService,
            ICustomerInteractionCommandService commandService,
            IToastService toastService,
            ILoadingService loadingService,
            IServiceProvider serviceProvider,
            IAuthService authService,
            IUserAppService userAppService)
        {
            _readService = readService;
            _commandService = commandService;
            _toastService = toastService;
            _loadingService = loadingService;
            _serviceProvider = serviceProvider;
            _authService = authService;
            _userAppService = userAppService;

            EventAggregator.Instance.Subscribe<CustomerInteractionChangedEvent>(OnInteractionChanged);

            InitializeAsync().SafeFireAndForget("CustomerInteractionsViewModel.InitializeAsync");
        }

        private async Task InitializeAsync()
        {
            await LoadUsersAsync();
            BuildCalendarMonth();
            await LoadCalendarCountsAsync();
            await LoadInteractionsAsync();
        }

        private void OnInteractionChanged(CustomerInteractionChangedEvent evt)
        {
            Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                await LoadInteractionsAsync();
                await LoadCalendarCountsAsync();
                if (SelectedInteraction != null && SelectedInteraction.Id == evt.InteractionId)
                {
                    var updated = await _readService.GetByIdAsync(evt.InteractionId);
                    if (updated.IsSuccess && updated.Value != null)
                    {
                        SelectedInteraction = updated.Value;
                    }
                }
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
                    foreach (var u in res.Value)
                    {
                        Users.Add(u);
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Kullanıcılar yüklenirken hata oluştu.");
            }
        }

        private void BuildCalendarMonth()
        {
            CalendarDays.Clear();
            var firstDayOfMonth = new DateTime(_currentCalendarMonth.Year, _currentCalendarMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var daysInMonth = DateTime.DaysInMonth(_currentCalendarMonth.Year, _currentCalendarMonth.Month);
            var today = DateTime.UtcNow.Date;

            // Pazartesi = 1, Pazar = 7
            int firstDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
            if (firstDayOfWeek == 0) firstDayOfWeek = 7;
            int paddingDaysBefore = firstDayOfWeek - 1;

            var prevMonth = firstDayOfMonth.AddMonths(-1);
            int prevMonthDays = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);
            for (int i = paddingDaysBefore - 1; i >= 0; i--)
            {
                var d = new DateTime(prevMonth.Year, prevMonth.Month, prevMonthDays - i, 0, 0, 0, DateTimeKind.Utc);
                CalendarDays.Add(new CalendarDayItemViewModel
                {
                    Date = d,
                    IsCurrentMonth = false,
                    IsToday = d.Date == today
                });
            }

            for (int day = 1; day <= daysInMonth; day++)
            {
                var d = new DateTime(_currentCalendarMonth.Year, _currentCalendarMonth.Month, day, 0, 0, 0, DateTimeKind.Utc);
                var isToday = d.Date == today;
                var item = new CalendarDayItemViewModel
                {
                    Date = d,
                    IsCurrentMonth = true,
                    IsToday = isToday
                };
                if (SelectedCalendarDate.HasValue && SelectedCalendarDate.Value.Date == d.Date)
                {
                    item.IsSelected = true;
                    _selectedCalendarDay = item;
                }
                CalendarDays.Add(item);
            }

            int remaining = 42 - CalendarDays.Count; // 6 satır x 7 sütun
            var nextMonth = firstDayOfMonth.AddMonths(1);
            for (int day = 1; day <= remaining; day++)
            {
                var d = new DateTime(nextMonth.Year, nextMonth.Month, day, 0, 0, 0, DateTimeKind.Utc);
                CalendarDays.Add(new CalendarDayItemViewModel
                {
                    Date = d,
                    IsCurrentMonth = false,
                    IsToday = d.Date == today
                });
            }

            OnPropertyChanged(nameof(CalendarMonthTitle));
        }

        [RelayCommand]
        private async Task PreviousMonthAsync()
        {
            _currentCalendarMonth = _currentCalendarMonth.AddMonths(-1);
            BuildCalendarMonth();
            await LoadCalendarCountsAsync();
        }

        [RelayCommand]
        private async Task NextMonthAsync()
        {
            _currentCalendarMonth = _currentCalendarMonth.AddMonths(1);
            BuildCalendarMonth();
            await LoadCalendarCountsAsync();
        }

        [RelayCommand]
        private async Task ResetToTodayAsync()
        {
            _currentCalendarMonth = DateTime.UtcNow;
            _selectedCalendarDate = DateTime.UtcNow.Date;
            BuildCalendarMonth();
            await LoadCalendarCountsAsync();
            await LoadInteractionsAsync();
        }

        [RelayCommand]
        private async Task SelectCalendarDayAsync(CalendarDayItemViewModel? day)
        {
            if (day == null) return;
            if (_selectedCalendarDay != null)
            {
                _selectedCalendarDay.IsSelected = false;
            }
            _selectedCalendarDay = day;
            day.IsSelected = true;
            SelectedCalendarDate = day.Date;

            await LoadInteractionsAsync();
        }

        private async Task LoadCalendarCountsAsync()
        {
            try
            {
                var countsRes = await _readService.GetCalendarDayCountsAsync(_currentCalendarMonth.Year, _currentCalendarMonth.Month);
                if (countsRes.IsSuccess && countsRes.Value != null)
                {
                    var lookup = countsRes.Value.ToDictionary(k => k.Date.Date, v => v);
                    foreach (var dayItem in CalendarDays)
                    {
                        if (lookup.TryGetValue(dayItem.Date.Date, out var summary))
                        {
                            dayItem.InteractionCount = summary.TotalCount;
                            dayItem.OverdueCount = summary.OverdueCount;
                        }
                        else
                        {
                            dayItem.InteractionCount = 0;
                            dayItem.OverdueCount = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Takvim gün sayıları yüklenirken hata oluştu.");
            }
        }

        [RelayCommand]
        public async Task LoadInteractionsAsync()
        {
            _loadingService.Show();
            try
            {
                var filter = new CustomerInteractionFilterDto
                {
                    SearchText = SearchText,
                    PageNumber = 1,
                    PageSize = 250
                };

                // Eğer takvimden gün seçilmişse o günü filtrele
                if (SelectedCalendarDate.HasValue)
                {
                    var d = SelectedCalendarDate.Value.Date;
                    filter.FollowUpStartDate = d;
                    filter.FollowUpEndDate = d.AddDays(1).AddTicks(-1);
                }
                else
                {
                    var todayUtc = DateTime.UtcNow.Date;
                    switch (ActiveFolder)
                    {
                        case InteractionFolder.Today:
                            filter.FollowUpStartDate = todayUtc;
                            filter.FollowUpEndDate = todayUtc.AddDays(1).AddTicks(-1);
                            filter.Status = InteractionStatus.New;
                            break;
                        case InteractionFolder.Overdue:
                            filter.IsOverdueOnly = true;
                            break;
                        case InteractionFolder.BallInCourtUs:
                            filter.BallInCourt = BallInCourtStatus.Us;
                            break;
                        case InteractionFolder.BallInCourtCustomer:
                            filter.BallInCourt = BallInCourtStatus.Customer;
                            break;
                        case InteractionFolder.BallInCourtManager:
                            filter.BallInCourt = BallInCourtStatus.Manager;
                            break;
                        case InteractionFolder.AssignedToMe:
                            filter.AssignedUserId = _authService.CurrentUser?.Id;
                            break;
                        case InteractionFolder.Completed:
                            filter.Status = InteractionStatus.Completed;
                            break;
                        case InteractionFolder.All:
                            break;
                    }
                }

                var res = await _readService.FilterAsync(filter);
                Interactions.Clear();
                if (res.IsSuccess && res.Value != null)
                {
                    foreach (var item in res.Value.Items)
                    {
                        Interactions.Add(item);
                    }
                }

                await RefreshSmartFolderCountsAsync();

                if (SelectedInteraction != null)
                {
                    var stillExists = Interactions.FirstOrDefault(x => x.Id == SelectedInteraction.Id);
                    SelectedInteraction = stillExists;
                }
            }
            catch (Exception ex)
            {
                ReportInteractionException(ex, "Görüşme kayıtları yüklenemedi.");
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        private async Task RefreshSmartFolderCountsAsync()
        {
            try
            {
                // Hafif sorgu ile tüm kayıtları çekip klasör sayılarını güncelle
                var allRes = await _readService.FilterAsync(new CustomerInteractionFilterDto { PageNumber = 1, PageSize = 500 });
                if (allRes.IsSuccess && allRes.Value != null)
                {
                    var items = allRes.Value.Items;
                    var todayUtc = DateTime.UtcNow.Date;
                    var currentUserId = _authService.CurrentUser?.Id;

                    CountAll = items.Count;
                    CountCompleted = items.Count(x => x.Status == InteractionStatus.Completed);
                    CountOverdue = items.Count(x => x.IsOverdue && x.Status != InteractionStatus.Completed);
                    CountToday = items.Count(x => x.FollowUpDate.HasValue && x.FollowUpDate.Value.Date == todayUtc && x.Status != InteractionStatus.Completed);
                    CountBallInCourtUs = items.Count(x => x.BallInCourt == BallInCourtStatus.Us && x.Status != InteractionStatus.Completed);
                    CountBallInCourtCustomer = items.Count(x => x.BallInCourt == BallInCourtStatus.Customer && x.Status != InteractionStatus.Completed);
                    CountBallInCourtManager = items.Count(x => x.BallInCourt == BallInCourtStatus.Manager && x.Status != InteractionStatus.Completed);
                    CountAssignedToMe = items.Count(x => currentUserId.HasValue && x.AssignedUserId == currentUserId && x.Status != InteractionStatus.Completed);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Klasör sayaçları güncellenirken hata oluştu.");
            }
        }

        // ==========================================
        // SLIDE-OVER DRAWER METHODS
        // ==========================================
        [RelayCommand]
        public void OpenNewDrawer()
        {
            DrawerDirection = InteractionDirection.InboundRequest;
            DrawerRequestType = InteractionRequestType.PriceQuote;
            DrawerPriority = InteractionPriority.Normal;
            DrawerBallInCourt = BallInCourtStatus.Us;
            DrawerCallerPhone = string.Empty;
            DrawerCallerName = string.Empty;
            DrawerSubject = string.Empty;
            DrawerNotes = string.Empty;
            DrawerFollowUpDate = DateTime.UtcNow.AddMinutes(30);
            DrawerSelectedCustomerId = null;
            DrawerSelectedCustomerName = null;
            DrawerCallerContext = null;
            DrawerChecklistItems.Clear();
            DrawerNewChecklistText = string.Empty;
            IsDrawerOpen = true;
        }

        [RelayCommand]
        public void CloseDrawer()
        {
            IsDrawerOpen = false;
        }

        [RelayCommand]
        public void SetDrawerCategory(string category)
        {
            switch (category)
            {
                case "Fiyat":
                    DrawerRequestType = InteractionRequestType.PriceQuote;
                    DrawerPriority = InteractionPriority.Normal;
                    break;
                case "Keşif":
                    DrawerRequestType = InteractionRequestType.Discovery;
                    DrawerPriority = InteractionPriority.Normal;
                    break;
                case "Servis":
                    DrawerRequestType = InteractionRequestType.ServiceStatus;
                    DrawerPriority = InteractionPriority.High;
                    break;
                case "Patron":
                    DrawerRequestType = InteractionRequestType.ManagerAgenda;
                    DrawerPriority = InteractionPriority.Critical;
                    DrawerBallInCourt = BallInCourtStatus.Manager;
                    break;
                case "Şikâyet":
                    DrawerRequestType = InteractionRequestType.Complaint;
                    DrawerPriority = InteractionPriority.High;
                    break;
                case "Geri Ara":
                    DrawerRequestType = InteractionRequestType.CallBack;
                    DrawerPriority = InteractionPriority.Normal;
                    DrawerFollowUpDate = DateTime.UtcNow.AddHours(2);
                    break;
            }
        }

        [RelayCommand]
        public void SetDrawerFollowUp(string option)
        {
            var now = DateTime.UtcNow;
            switch (option)
            {
                case "+30dk":
                    DrawerFollowUpDate = now.AddMinutes(30);
                    break;
                case "+2saat":
                    DrawerFollowUpDate = now.AddHours(2);
                    break;
                case "Yarın 09:30":
                    var tomorrow = now.AddDays(1);
                    DrawerFollowUpDate = new DateTime(tomorrow.Year, tomorrow.Month, tomorrow.Day, 6, 30, 0, DateTimeKind.Utc); // 09:30 TSİ = 06:30 UTC
                    break;
                case "2 Gün Sonra":
                    var in2Days = now.AddDays(2);
                    DrawerFollowUpDate = new DateTime(in2Days.Year, in2Days.Month, in2Days.Day, 6, 30, 0, DateTimeKind.Utc);
                    break;
            }
        }

        [RelayCommand]
        public void AddDrawerChecklistItem()
        {
            if (!string.IsNullOrWhiteSpace(DrawerNewChecklistText))
            {
                DrawerChecklistItems.Add(DrawerNewChecklistText.Trim());
                DrawerNewChecklistText = string.Empty;
            }
        }

        [RelayCommand]
        public void RemoveDrawerChecklistItem(string item)
        {
            if (DrawerChecklistItems.Contains(item))
            {
                DrawerChecklistItems.Remove(item);
            }
        }

        private void DebouncedLookupCallerContext()
        {
            _drawerLookupCts?.Cancel();
            _drawerLookupCts = new CancellationTokenSource();
            var token = _drawerLookupCts.Token;

            Task.Delay(300, token).ContinueWith(async t =>
            {
                if (token.IsCancellationRequested) return;
                var phone = DrawerCallerPhone?.Trim();
                if (string.IsNullOrWhiteSpace(phone) || phone.Length < 4)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        DrawerCallerContext = null;
                        IsLoadingDrawerContext = false;
                    });
                    return;
                }

                Application.Current?.Dispatcher.Invoke(() => IsLoadingDrawerContext = true);
                try
                {
                    var res = await _readService.LookupCallerContextAsync(phone, token);
                    if (!token.IsCancellationRequested && res.IsSuccess && res.Value != null)
                    {
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            DrawerCallerContext = res.Value;
                            if (res.Value.CustomerId.HasValue)
                            {
                                DrawerSelectedCustomerId = res.Value.CustomerId;
                                DrawerSelectedCustomerName = res.Value.CustomerName;
                                if (string.IsNullOrWhiteSpace(DrawerCallerName))
                                {
                                    DrawerCallerName = res.Value.CustomerName ?? string.Empty;
                                }
                            }
                        });
                    }
                }
                catch (OperationCanceledException ex)
                {
                    Serilog.Log.Debug(ex, "Arayan bağlamı araması iptal edildi.");
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Arayan bağlamı aranırken hata.");
                }
                finally
                {
                    if (!token.IsCancellationRequested)
                    {
                        Application.Current?.Dispatcher.Invoke(() => IsLoadingDrawerContext = false);
                    }
                }
            }, TaskScheduler.Default);
        }

        [RelayCommand]
        public async Task SaveDrawerRecordAsync()
        {
            if (string.IsNullOrWhiteSpace(DrawerCallerPhone) && string.IsNullOrWhiteSpace(DrawerCallerName))
            {
                _toastService.ShowWarning("Lütfen en azından bir telefon numarası veya arayan adı giriniz.", "Eksik Bilgi");
                return;
            }

            _loadingService.Show();
            try
            {
                var dto = new CreateCustomerInteractionDto
                {
                    Direction = DrawerDirection,
                    BallInCourt = DrawerBallInCourt,
                    RequestType = DrawerRequestType,
                    Priority = DrawerPriority,
                    Channel = InteractionChannel.Phone,
                    CallerPhone = DrawerCallerPhone?.Trim() ?? string.Empty,
                    CallerName = DrawerCallerName?.Trim() ?? "Bilinmeyen Arayan",
                    Subject = string.IsNullOrWhiteSpace(DrawerSubject) ? $"{DrawerRequestType} Talebi" : DrawerSubject.Trim(),
                    Summary = DrawerNotes?.Trim() ?? string.Empty,
                    DetailedNotes = DrawerNotes?.Trim() ?? string.Empty,
                    CustomerId = DrawerSelectedCustomerId,
                    RequiresFollowUp = DrawerFollowUpDate.HasValue,
                    FollowUpDate = DrawerFollowUpDate,
                    RequiresManagerAttention = DrawerBallInCourt == BallInCourtStatus.Manager,
                    AssignedUserId = _authService.CurrentUser?.Id
                };

                var createRes = await _commandService.CreateAsync(dto);
                if (createRes.IsSuccess && createRes.Value != null)
                {
                    int newId = createRes.Value.Id;
                    // Eğer drawer'da checklist maddeleri girildiyse ekle
                    foreach (var chk in DrawerChecklistItems)
                    {
                        await _commandService.AddChecklistItemAsync(newId, chk);
                    }

                    _toastService.ShowSuccess("Yeni vaka defter kaydı başarıyla açıldı.", "Kayıt Oluşturuldu");
                    IsDrawerOpen = false;
                    await LoadInteractionsAsync();
                    await LoadCalendarCountsAsync();
                    SelectedInteraction = Interactions.FirstOrDefault(x => x.Id == newId);
                }
                else
                {
                    ReportInteractionFailure(createRes.Error, "Vaka kaydı oluşturulamadı.");
                }
            }
            catch (Exception ex)
            {
                ReportInteractionException(ex, "Vaka kaydı kaydedilirken bir sorun oluştu.");
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        // ==========================================
        // RIGHT PANEL (MULTI-LAYER CASE BOOK)
        // ==========================================
        private void PopulateRightPanel()
        {
            RightPanelChecklist.Clear();
            RightPanelThread.Clear();
            NewChecklistTitle = string.Empty;
            NewThreadNoteContent = string.Empty;

            if (SelectedInteraction != null)
            {
                foreach (var item in SelectedInteraction.ChecklistItems)
                {
                    RightPanelChecklist.Add(item);
                }

                foreach (var entry in SelectedInteraction.ThreadEntries.OrderBy(x => x.CreatedAtUtc))
                {
                    RightPanelThread.Add(entry);
                }

                SelectedAssignedUser = Users.FirstOrDefault(u => u.Id == SelectedInteraction.AssignedUserId);
            }
        }

        [RelayCommand]
        public async Task AddChecklistItemAsync()
        {
            if (SelectedInteraction == null || string.IsNullOrWhiteSpace(NewChecklistTitle)) return;
            try
            {
                var title = NewChecklistTitle.Trim();
                var res = await _commandService.AddChecklistItemAsync(SelectedInteraction.Id, title);
                if (res.IsSuccess)
                {
                    NewChecklistTitle = string.Empty;
                    var updated = await _readService.GetByIdAsync(SelectedInteraction.Id);
                    if (updated.IsSuccess && updated.Value != null)
                    {
                        SelectedInteraction = updated.Value;
                    }
                }
                else
                {
                    ReportInteractionFailure(res.Error, "Kontrol listesi maddesi eklenemedi.");
                }
            }
            catch (Exception ex)
            {
                ReportInteractionException(ex, "Kontrol listesi maddesi eklenemedi.");
            }
        }

        [RelayCommand]
        public async Task ToggleChecklistItemAsync(InteractionChecklistItem? item)
        {
            if (SelectedInteraction == null || item == null) return;
            try
            {
                var res = await _commandService.ToggleChecklistItemAsync(SelectedInteraction.Id, item.Id);
                if (res.IsSuccess)
                {
                    var updated = await _readService.GetByIdAsync(SelectedInteraction.Id);
                    if (updated.IsSuccess && updated.Value != null)
                    {
                        SelectedInteraction = updated.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                ReportInteractionException(ex, "Kontrol listesi güncellenemedi.");
            }
        }

        [RelayCommand]
        public async Task DeleteChecklistItemAsync(InteractionChecklistItem? item)
        {
            if (SelectedInteraction == null || item == null) return;
            try
            {
                var res = await _commandService.DeleteChecklistItemAsync(SelectedInteraction.Id, item.Id);
                if (res.IsSuccess)
                {
                    var updated = await _readService.GetByIdAsync(SelectedInteraction.Id);
                    if (updated.IsSuccess && updated.Value != null)
                    {
                        SelectedInteraction = updated.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                ReportInteractionException(ex, "Kontrol listesi maddesi silinemedi.");
            }
        }

        [RelayCommand]
        public async Task AddThreadEntryAsync()
        {
            if (SelectedInteraction == null || string.IsNullOrWhiteSpace(NewThreadNoteContent)) return;
            try
            {
                var dto = new AddThreadEntryDto
                {
                    EntryType = SelectedThreadEntryType,
                    Content = NewThreadNoteContent.Trim()
                };

                var res = await _commandService.AddThreadEntryAsync(SelectedInteraction.Id, dto);
                if (res.IsSuccess)
                {
                    NewThreadNoteContent = string.Empty;
                    var updated = await _readService.GetByIdAsync(SelectedInteraction.Id);
                    if (updated.IsSuccess && updated.Value != null)
                    {
                        SelectedInteraction = updated.Value;
                    }
                    _toastService.ShowSuccess("Zaman çizelgesine yeni kayıt eklendi.", "Katman Eklendi");
                }
                else
                {
                    ReportInteractionFailure(res.Error, "Zaman çizelgesine kayıt eklenemedi.");
                }
            }
            catch (Exception ex)
            {
                ReportInteractionException(ex, "Zaman çizelgesine kayıt eklenemedi.");
            }
        }

        [RelayCommand]
        public async Task ChangeBallInCourtAsync(string statusStr)
        {
            if (SelectedInteraction == null) return;
            if (Enum.TryParse<BallInCourtStatus>(statusStr, out var newStatus))
            {
                try
                {
                    var dto = new AddThreadEntryDto
                    {
                        EntryType = ThreadEntryType.Handoff,
                        Content = $"Top durumu güncellendi: {statusStr}",
                        NewBallInCourt = newStatus
                    };
                    var res = await _commandService.AddThreadEntryAsync(SelectedInteraction.Id, dto);
                    if (res.IsSuccess)
                    {
                        var updated = await _readService.GetByIdAsync(SelectedInteraction.Id);
                        if (updated.IsSuccess && updated.Value != null)
                        {
                            SelectedInteraction = updated.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ReportInteractionException(ex, "Top durumu güncellenemedi.");
                }
            }
        }

        [RelayCommand]
        public async Task QuickSnoozeAsync(string option)
        {
            if (SelectedInteraction == null) return;
            var now = DateTime.UtcNow;
            DateTime snoozeUntil;
            string reason;

            switch (option)
            {
                case "+30dk":
                    snoozeUntil = now.AddMinutes(30);
                    reason = "30 dakika ertelendi";
                    break;
                case "+2s":
                    snoozeUntil = now.AddHours(2);
                    reason = "2 saat ertelendi";
                    break;
                case "Yarın 09:30":
                    var t = now.AddDays(1);
                    snoozeUntil = new DateTime(t.Year, t.Month, t.Day, 6, 30, 0, DateTimeKind.Utc);
                    reason = "Yarın 09:30'a ertelendi";
                    break;
                case "Haftaya Pzt":
                    int daysUntilMon = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
                    if (daysUntilMon == 0) daysUntilMon = 7;
                    var mon = now.AddDays(daysUntilMon);
                    snoozeUntil = new DateTime(mon.Year, mon.Month, mon.Day, 6, 30, 0, DateTimeKind.Utc);
                    reason = "Gelecek Pazartesiye ertelendi";
                    break;
                default:
                    snoozeUntil = now.AddHours(1);
                    reason = "Ertelendi";
                    break;
            }

            try
            {
                var res = await _commandService.SnoozeInteractionAsync(SelectedInteraction.Id, snoozeUntil, reason);
                if (res.IsSuccess)
                {
                    _toastService.ShowSuccess($"{reason}.", "Ertelendi");
                    var updated = await _readService.GetByIdAsync(SelectedInteraction.Id);
                    if (updated.IsSuccess && updated.Value != null)
                    {
                        SelectedInteraction = updated.Value;
                    }
                    await LoadInteractionsAsync();
                }
            }
            catch (Exception ex)
            {
                ReportInteractionException(ex, "Erteleme işlemi gerçekleştirilemedi.");
            }
        }

        [RelayCommand]
        public async Task ReassignUserAsync(UserListItemDto? user)
        {
            if (SelectedInteraction == null || user == null) return;
            try
            {
                var res = await _commandService.AssignUserAsync(SelectedInteraction.Id, user.Id, user.AdSoyad);
                if (res.IsSuccess)
                {
                    await _commandService.AddThreadEntryAsync(SelectedInteraction.Id, new AddThreadEntryDto
                    {
                        EntryType = ThreadEntryType.Handoff,
                        Content = $"Vaka {user.AdSoyad} personeline devredildi.",
                        UpdateAssignedToUserId = user.Id,
                        UpdateAssignedToUsername = user.AdSoyad
                    });

                    _toastService.ShowSuccess($"Sorumlu: {user.AdSoyad}", "Devredildi");
                    var updated = await _readService.GetByIdAsync(SelectedInteraction.Id);
                    if (updated.IsSuccess && updated.Value != null)
                    {
                        SelectedInteraction = updated.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                ReportInteractionException(ex, "Sorumlu devir işlemi gerçekleştirilemedi.");
            }
        }

        [RelayCommand]
        public async Task EscalateToManagerAsync()
        {
            if (SelectedInteraction == null) return;
            try
            {
                var dto = new AddThreadEntryDto
                {
                    EntryType = ThreadEntryType.ManagerInstruction,
                    Content = "★ Vaka yönetici / patron incelemesine ve kararına sevk edildi.",
                    NewBallInCourt = BallInCourtStatus.Manager
                };
                var res = await _commandService.AddThreadEntryAsync(SelectedInteraction.Id, dto);
                if (res.IsSuccess)
                {
                    _toastService.ShowSuccess("Vaka yönetici gündemine iletildi.", "Patrona Aktarıldı");
                    var updated = await _readService.GetByIdAsync(SelectedInteraction.Id);
                    if (updated.IsSuccess && updated.Value != null)
                    {
                        SelectedInteraction = updated.Value;
                    }
                    await LoadInteractionsAsync();
                }
            }
            catch (Exception ex)
            {
                ReportInteractionException(ex, "Vaka yönetici gündemine iletilemedi.");
            }
        }

        [RelayCommand]
        public void OpenWhatsApp()
        {
            if (SelectedInteraction == null || string.IsNullOrWhiteSpace(SelectedInteraction.CallerPhone)) return;
            try
            {
                var digits = new string(SelectedInteraction.CallerPhone.Where(char.IsDigit).ToArray());
                if (digits.StartsWith("0")) digits = "9" + digits;
                else if (!digits.StartsWith("90") && digits.Length == 10) digits = "90" + digits;

                var psi = new ProcessStartInfo
                {
                    FileName = $"https://wa.me/{digits}",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                ReportInteractionException(ex, "WhatsApp bağlantısı açılamadı.");
            }
        }

        private bool CanActOnSelected() => SelectedInteraction != null && SelectedInteraction.Status != InteractionStatus.Completed;

        [RelayCommand(CanExecute = nameof(CanActOnSelected))]
        private async Task CompleteInteractionAsync()
        {
            if (SelectedInteraction == null) return;
            _loadingService.Show();
            try
            {
                var dto = new UpdateCustomerInteractionStatusDto
                {
                    InteractionId = SelectedInteraction.Id,
                    NewStatus = InteractionStatus.Completed,
                    Reason = "Kullanıcı tarafından tamamlandı.",
                    ResolutionNotes = "Görüşme ve takip tamamlandı."
                };

                var res = await _commandService.UpdateStatusAsync(dto);
                if (res.IsSuccess)
                {
                    _toastService.ShowSuccess($"{SelectedInteraction.InteractionNumber} tamamlandı.", "Tamamlandı");
                    EventAggregator.Instance.Publish(new CustomerInteractionChangedEvent(
                        SelectedInteraction.Id,
                        SelectedInteraction.InteractionNumber,
                        SelectedInteraction.CustomerId,
                        InteractionChangeType.StatusChanged));
                    await LoadInteractionsAsync();
                }
                else
                {
                    ReportInteractionFailure(res.Error, "Görüşme tamamlanamadı.");
                }
            }
            catch (Exception ex)
            {
                ReportInteractionException(ex, "Görüşme tamamlanırken bir sorun oluştu.");
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        [RelayCommand(CanExecute = nameof(CanActOnSelected))]
        private async Task ConvertToQuote()
        {
            if (SelectedInteraction == null) return;
            try
            {
                var quotationLauncher = _serviceProvider.GetRequiredService<IQuotationLauncher>();
                await quotationLauncher.ShowAsync(SelectedInteraction.CustomerId, modal: false);

                var dto = new UpdateCustomerInteractionStatusDto
                {
                    InteractionId = SelectedInteraction.Id,
                    NewStatus = InteractionStatus.InProgress,
                    Reason = "Teklif ekranına aktarıldı.",
                    ResolutionNotes = $"Standart teklif ekranı açıldı. Tarih: {DateTime.UtcNow:dd.MM.yyyy HH:mm}"
                };
                await _commandService.UpdateStatusAsync(dto);

                await _commandService.AddThreadEntryAsync(SelectedInteraction.Id, new AddThreadEntryDto
                {
                    EntryType = ThreadEntryType.SystemConversion,
                    Content = "Teklif hazırlama ekranına aktarıldı."
                });

                EventAggregator.Instance.Publish(new CustomerInteractionChangedEvent(
                    SelectedInteraction.Id,
                    SelectedInteraction.InteractionNumber,
                    SelectedInteraction.CustomerId,
                    InteractionChangeType.Converted));

                _toastService.ShowSuccess($"{SelectedInteraction.InteractionNumber} teklif hazırlama ekranına aktarıldı.", "Teklife Dönüştür");
                await LoadInteractionsAsync();
            }
            catch (Exception ex)
            {
                ReportInteractionException(ex, "Teklif hazırlama ekranı açılamadı.");
            }
        }

        [RelayCommand(CanExecute = nameof(CanActOnSelected))]
        private async Task ConvertToServiceJobAsync()
        {
            if (SelectedInteraction == null) return;
            try
            {
                var serviceJobVm = _serviceProvider.GetRequiredService<ServiceJobViewModel>();
                if (SelectedInteraction.CustomerId.HasValue && SelectedInteraction.CustomerId.Value > 0)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var cust = await ctx.Customers.FindAsync(SelectedInteraction.CustomerId.Value);
                    if (cust != null)
                    {
                        serviceJobVm.SetInitialCustomer(cust);
                    }
                }
                else
                {
                    serviceJobVm.QuickCustomerName = SelectedInteraction.CallerName;
                    serviceJobVm.QuickCustomerPhone = SelectedInteraction.CallerPhone;
                    serviceJobVm.IsQuickAddCustomer = true;
                }

                serviceJobVm.Description = $"[Talep: {SelectedInteraction.InteractionNumber}] {SelectedInteraction.Subject}\n{SelectedInteraction.Summary}\n{SelectedInteraction.DetailedNotes}".Trim();

                var window = new NewServiceJobWindow(serviceJobVm)
                {
                    Owner = Application.Current?.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                if (window.ShowDialog() == true)
                {
                    var dto = new UpdateCustomerInteractionStatusDto
                    {
                        InteractionId = SelectedInteraction.Id,
                        NewStatus = InteractionStatus.InProgress,
                        Reason = "Servis iş emrine aktarıldı.",
                        ResolutionNotes = $"İş emri oluşturuldu. Tarih: {DateTime.UtcNow:dd.MM.yyyy HH:mm}"
                    };
                    await _commandService.UpdateStatusAsync(dto);

                    await _commandService.AddThreadEntryAsync(SelectedInteraction.Id, new AddThreadEntryDto
                    {
                        EntryType = ThreadEntryType.SystemConversion,
                        Content = "Servis iş emrine dönüştürüldü."
                    });

                    EventAggregator.Instance.Publish(new CustomerInteractionChangedEvent(
                        SelectedInteraction.Id,
                        SelectedInteraction.InteractionNumber,
                        SelectedInteraction.CustomerId,
                        InteractionChangeType.Converted));

                    _toastService.ShowSuccess($"{SelectedInteraction.InteractionNumber} iş emrine dönüştürüldü.", "Dönüştürüldü");
                    await LoadInteractionsAsync();
                }
            }
            catch (Exception ex)
            {
                ReportInteractionException(ex, "Kayıt iş emrine dönüştürülemedi.");
            }
        }

        [RelayCommand(CanExecute = nameof(CanActOnSelected))]
        private async Task ConvertToFaultTicketAsync()
        {
            if (SelectedInteraction == null) return;
            try
            {
                var faultVm = _serviceProvider.GetRequiredService<FaultTicketViewModel>();
                faultVm.SetInitialData(
                    SelectedInteraction.CustomerId,
                    SelectedInteraction.CustomerName,
                    SelectedInteraction.CallerPhone,
                    SelectedInteraction.Subject,
                    $"{SelectedInteraction.Summary}\n{SelectedInteraction.DetailedNotes}".Trim());

                var window = new Views.FaultTicketWindow(faultVm)
                {
                    Owner = Application.Current?.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                if (window.ShowDialog() == true)
                {
                    var dto = new UpdateCustomerInteractionStatusDto
                    {
                        InteractionId = SelectedInteraction.Id,
                        NewStatus = InteractionStatus.InProgress,
                        Reason = "Arıza kaydına aktarıldı.",
                        ResolutionNotes = $"Arıza kaydı oluşturuldu. Tarih: {DateTime.UtcNow:dd.MM.yyyy HH:mm}"
                    };
                    await _commandService.UpdateStatusAsync(dto);

                    await _commandService.AddThreadEntryAsync(SelectedInteraction.Id, new AddThreadEntryDto
                    {
                        EntryType = ThreadEntryType.SystemConversion,
                        Content = "Arıza kaydına dönüştürüldü."
                    });

                    EventAggregator.Instance.Publish(new CustomerInteractionChangedEvent(
                        SelectedInteraction.Id,
                        SelectedInteraction.InteractionNumber,
                        SelectedInteraction.CustomerId,
                        InteractionChangeType.Converted));

                    _toastService.ShowSuccess($"{SelectedInteraction.InteractionNumber} arıza kaydına dönüştürüldü.", "Dönüştürüldü");
                    await LoadInteractionsAsync();
                }
            }
            catch (Exception ex)
            {
                ReportInteractionException(ex, "Kayıt arıza kaydına dönüştürülemedi.");
            }
        }

        private void ReportInteractionException(Exception ex, string friendlyMessage)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            Serilog.Log.Error(ex, "{FriendlyMessage} [CorrelationId: {CorrelationId}]", friendlyMessage, correlationId);
            _toastService.ShowError($"{friendlyMessage} Lütfen tekrar deneyin. (Ref: {correlationId})", "Hata");
        }

        private void ReportInteractionFailure(string? errorText, string fallbackMessage)
        {
            if (string.IsNullOrWhiteSpace(errorText) || ToastService.ContainsRawDatabaseError(errorText))
            {
                var correlationId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                Serilog.Log.Error(
                    "Görüşme işlemi başarısız [CorrelationId: {CorrelationId}] Detay: {RawError}",
                    correlationId, errorText);
                _toastService.ShowError($"{fallbackMessage} (Ref: {correlationId})", "Hata");
                return;
            }

            _toastService.ShowError(errorText, "Hata");
        }
    }
}
