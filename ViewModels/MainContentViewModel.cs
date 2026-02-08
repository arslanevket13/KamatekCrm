using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Services;
using KamatekCrm.Views;
using KamatekCrm.Repositories;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Ana içerik alanı ViewModel (Sidebar + Content)
    /// </summary>
    public class MainContentViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthService _authService;
        private readonly NavigationService _navigationService;
        private readonly NotificationService _notificationService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;

        private object? _currentView;

        /// <summary>
        /// Global arama ViewModel
        /// </summary>
        public GlobalSearchViewModel SearchViewModel { get; }

        public System.Collections.ObjectModel.ObservableCollection<NotificationItem> Notifications { get; } = new();

        private int _notificationCount;
        public int NotificationCount
        {
            get => _notificationCount;
            set => SetProperty(ref _notificationCount, value);
        }

        private bool _isNotificationsOpen;
        public bool IsNotificationsOpen
        {
            get => _isNotificationsOpen;
            set => SetProperty(ref _isNotificationsOpen, value);
        }

        // ==================== SIDEBAR & TEMA ====================
        
        private bool _isSidebarCollapsed;
        /// <summary>
        /// Sidebar daraltılmış mı?
        /// </summary>
        public bool IsSidebarCollapsed
        {
            get => _isSidebarCollapsed;
            set
            {
                if (SetProperty(ref _isSidebarCollapsed, value))
                {
                    OnPropertyChanged(nameof(SidebarWidth));
                    OnPropertyChanged(nameof(ShowSidebarText));
                    // Tercihi kaydet
                    Properties.Settings.Default.SidebarCollapsed = value;
                    Properties.Settings.Default.Save();
                }
            }
        }

        /// <summary>
        /// Sidebar genişliği (Collapsed: 60, Expanded: 250)
        /// </summary>
        public double SidebarWidth => IsSidebarCollapsed ? 65 : 250;

        /// <summary>
        /// Sidebar metin gösterilsin mi?
        /// </summary>
        public bool ShowSidebarText => !IsSidebarCollapsed;

        private bool _isDarkMode;
        /// <summary>
        /// Dark mode aktif mi?
        /// </summary>
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    ThemeService.ApplyTheme(value);
                }
            }
        }

        /// <summary>
        /// Aktif görünüm
        /// </summary>
        public object? CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        /// <summary>
        /// Mevcut kullanıcı ad soyad
        /// </summary>
        public string CurrentUserName => _authService.CurrentUser?.AdSoyad ?? "Misafir";

        /// <summary>
        /// Mevcut kullanıcı rol gösterimi
        /// </summary>
        public string CurrentUserRole => GetDisplayRole(_authService.CurrentUser?.Role);

        /// <summary>
        /// Admin mi?
        /// </summary>
        public bool IsAdmin => _authService.IsAdmin;

        #region Navigation Commands

        public ICommand NavigateToDashboardCommand { get; }
        public ICommand NavigateToCustomersCommand { get; }
        public ICommand NavigateToProductsCommand { get; }
        public ICommand NavigateToServiceJobsCommand { get; }
        public ICommand NavigateToStockCountCommand { get; }
        public ICommand NavigateToReportsCommand { get; }
        public ICommand NavigateToUsersCommand { get; }
        public ICommand NavigateToSystemLogsCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand OpenFaultTicketCommand { get; }
        public ICommand OpenProjectQuoteCommand { get; }
        public ICommand OpenRepairTrackingCommand { get; }
        public ICommand OpenDirectSalesCommand { get; }
        public ICommand NavigateToRepairListCommand { get; }
        public ICommand NavigateToFieldJobListCommand { get; }
        public ICommand NavigateToSettingsCommand { get; }
        public ICommand NavigateToFinanceCommand { get; }
        public ICommand NavigateToAnalyticsCommand { get; }
        public ICommand NavigateToPurchaseOrdersCommand { get; }
        public ICommand NavigateToSuppliersCommand { get; } // NEW
        public ICommand NavigateToPipelineCommand { get; }

        // ...

        // Constructor
        // NavigateToPurchaseOrdersCommand = new RelayCommand(_ => NavigateToPurchaseOrders());
        // NavigateToSuppliersCommand = new RelayCommand(_ => NavigateToSuppliers()); // NEW

        // Methods

        public ICommand NavigateToSchedulerCommand { get; }
        public ICommand ToggleNotificationsCommand { get; }
        public ICommand RefreshNotificationsCommand { get; }
        
        // Yeni Komutlar
        public ICommand ToggleSidebarCommand { get; }
        public ICommand ToggleDarkModeCommand { get; }
        public ICommand OpenQuickAddCommand { get; }

        // RBAC Visibility
        public bool CanViewFinance => _authService.CanViewFinance;
        public bool CanViewAnalytics => _authService.CanViewAnalytics;
        public bool CanAccessSettings => _authService.CanAccessSettings;

        // Finansal Sağlık Komutu
        public ICommand NavigateToFinancialHealthCommand { get; }

        public ICommand NavigateToRoutePlanningCommand { get; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public MainContentViewModel(IUnitOfWork unitOfWork, IAuthService authService, NavigationService navigationService, IToastService toastService, ILoadingService loadingService)
        {
            _unitOfWork = unitOfWork;
            _authService = authService;
            _navigationService = navigationService;
            _toastService = toastService;
            _loadingService = loadingService;

            // Global arama başlat
            SearchViewModel = new GlobalSearchViewModel();
            _notificationService = new NotificationService();

            // Kayıtlı tercihleri yükle
            _isSidebarCollapsed = Properties.Settings.Default.SidebarCollapsed;
            _isDarkMode = Properties.Settings.Default.IsDarkMode;

            NavigateToDashboardCommand = new RelayCommand(_ => NavigateToDashboard());
            NavigateToCustomersCommand = new RelayCommand(_ => NavigateToCustomers());
            NavigateToProductsCommand = new RelayCommand(_ => NavigateToProducts());
            NavigateToServiceJobsCommand = new RelayCommand(_ => NavigateToServiceJobs());
            NavigateToStockCountCommand = new RelayCommand(_ => NavigateToStockCount());
            NavigateToReportsCommand = new RelayCommand(_ => NavigateToReports());
            NavigateToUsersCommand = new RelayCommand(_ => NavigateToUsers(), _ => _authService.IsAdmin);
            NavigateToSystemLogsCommand = new RelayCommand(_ => NavigateToSystemLogs(), _ => _authService.IsAdmin);
            LogoutCommand = new RelayCommand(_ => Logout());
            OpenFaultTicketCommand = new RelayCommand(_ => OpenFaultTicket());
            OpenProjectQuoteCommand = new RelayCommand(_ => OpenProjectQuote());
            OpenRepairTrackingCommand = new RelayCommand(_ => OpenRepairTracking());
            OpenDirectSalesCommand = new RelayCommand(_ => OpenDirectSales());
            NavigateToRepairListCommand = new RelayCommand(_ => NavigateToRepairList());
            NavigateToFieldJobListCommand = new RelayCommand(_ => NavigateToFieldJobList());
            NavigateToSettingsCommand = new RelayCommand(_ => NavigateToSettings(), _ => _authService.CanAccessSettings);
            NavigateToFinanceCommand = new RelayCommand(_ => NavigateToFinance(), _ => _authService.CanViewFinance);
            NavigateToAnalyticsCommand = new RelayCommand(_ => NavigateToAnalytics(), _ => _authService.CanViewAnalytics);
            NavigateToPurchaseOrdersCommand = new RelayCommand(_ => NavigateToPurchaseOrders());
            NavigateToPipelineCommand = new RelayCommand(_ => NavigateToPipeline());
            NavigateToSchedulerCommand = new RelayCommand(_ => NavigateToScheduler());
            ToggleNotificationsCommand = new RelayCommand(_ => IsNotificationsOpen = !IsNotificationsOpen);
            RefreshNotificationsCommand = new RelayCommand(_ => LoadNotifications());
            
            // Yeni komutlar
            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarCollapsed = !IsSidebarCollapsed);
            ToggleDarkModeCommand = new RelayCommand(_ => IsDarkMode = !IsDarkMode);
            OpenQuickAddCommand = new RelayCommand(_ => OpenQuickAdd());
            NavigateToFinancialHealthCommand = new RelayCommand(_ => NavigateToFinancialHealth(), _ => _authService.CanViewFinance);
            NavigateToRoutePlanningCommand = new RelayCommand(_ => NavigateToRoutePlanning());
            
            NavigateToSuppliersCommand = new RelayCommand(_ => NavigateToSuppliers()); // Initialization

            LoadNotifications();

            // Varsayılan olarak Dashboard'u göster
            NavigateToDashboard();
        }

        #region Navigation Methods

        private void NavigateToDashboard() => _navigationService.NavigateTo<DashboardViewModel>();
        private void NavigateToCustomers() => _navigationService.NavigateTo<CustomersViewModel>();
        private void NavigateToProducts() => _navigationService.NavigateTo<ProductViewModel>();
        private void NavigateToServiceJobs() => _navigationService.NavigateTo<ServiceJobViewModel>();
        private void NavigateToStockCount() => _navigationService.NavigateTo<StockCountViewModel>();
        private void NavigateToReports() => _navigationService.NavigateTo<StockReportsViewModel>();
        private void NavigateToUsers() => _navigationService.NavigateTo<UsersViewModel>();
        private void NavigateToSystemLogs() => _navigationService.NavigateTo<SystemLogsViewModel>();
        private void NavigateToRepairList() => _navigationService.NavigateTo<RepairListViewModel>();
        private void NavigateToFieldJobList() => _navigationService.NavigateTo<FieldJobListViewModel>();

        /// <summary>
        /// Müşteri detay sayfasına geçiş
        /// </summary>
        public void NavigateToCustomerDetail(int customerId)
        {
            CurrentView = new CustomerDetailViewModel(customerId, _navigationService, _toastService, _loadingService);
        }

        private void OpenFaultTicket()
        {
            // Yeni Cihaz Kabul Ekranı (Repair Module)
            var window = new RepairRegistrationWindow();
            window.ShowDialog();
        }

        private void OpenRepairTracking()
        {
            // Yeni Arıza Takip Merkezi (Repair Module)
            // Tracking penceresi genellikle non-modal olabilir ki ana ekran kullanılmaya devam etsin
            var window = new RepairTrackingWindow();
            window.Show();
        }

        private void OpenProjectQuote()
        {
            var window = new ProjectQuoteEditorWindow();
            window.ShowDialog();
        }

        private void OpenDirectSales()
        {
            var window = new DirectSalesWindow();
            window.Show();
        }

        private void NavigateToSettings() => _navigationService.NavigateTo<SettingsViewModel>();
        private void NavigateToFinance() => _navigationService.NavigateTo<FinanceViewModel>();
        private void NavigateToAnalytics() => _navigationService.NavigateTo<AnalyticsViewModel>();
        private void NavigateToFinancialHealth() => _navigationService.NavigateTo<FinancialHealthViewModel>();
        // These need DI too! 
        // If I use _navigationService.NavigateTo<PurchaseOrderViewModel>(), PurchaseOrderViewModel MUST have properties injected.
        // PurchaseOrderViewModel constructor takes IUnitOfWork currently (line 271 of old file).
        // DI can resolve it if registered.
        private void NavigateToPurchaseOrders() => _navigationService.NavigateTo<PurchaseOrderViewModel>();
        private void NavigateToSuppliers() => _navigationService.NavigateTo<SuppliersViewModel>();
        private void NavigateToPipeline() => _navigationService.NavigateTo<PipelineViewModel>();
        private void NavigateToScheduler() => _navigationService.NavigateTo<SchedulerViewModel>();
        private void NavigateToRoutePlanning() => _navigationService.NavigateTo<RoutePlanningViewModel>();


        private void LoadNotifications()
        {
            var items = _notificationService.GetNotifications();
            Notifications.Clear();
            foreach (var item in items) Notifications.Add(item);
            NotificationCount = items.Count;
        }

        #endregion

        /// <summary>
        /// Çıkış yap - Login ekranına dön
        /// </summary>
        private void Logout()
        {
            _authService.Logout();
            _navigationService.NavigateToLogin();
        }

        /// <summary>
        /// Rol adını arayüz gösterimine dönüştür
        /// </summary>
        private static string GetDisplayRole(string? role)
        {
            return role?.ToLower() switch
            {
                "admin" => "Patron",
                "technician" => "Personel",
                "viewer" => "İzleyici",
                _ => role ?? ""
            };
        }

        /// <summary>
        /// Quick Add Modal'ı aç (Ctrl+K)
        /// </summary>
        private void OpenQuickAdd()
        {
            var modal = new Views.QuickAddModal
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            modal.ActionSelected += action =>
            {
                switch (action)
                {
                    case "OpenFaultTicket":
                        OpenFaultTicket();
                        break;
                    case "OpenDirectSales":
                        OpenDirectSales();
                        break;
                    case "NewCustomer":
                        NavigateToCustomers();
                        break;
                    case "OpenProjectQuote":
                        OpenProjectQuote();
                        break;
                    case "NavigateDashboard":
                        NavigateToDashboard();
                        break;
                    case "NavigateCustomers":
                        NavigateToCustomers();
                        break;
                    case "NavigateProducts":
                        NavigateToProducts();
                        break;
                    case "NavigateRepairList":
                        NavigateToRepairList();
                        break;
                    case "NavigateFinance":
                        NavigateToFinance();
                        break;
                    case "NavigateSettings":
                        NavigateToSettings();
                        break;
                }
            };

            modal.ShowDialog();
        }
    }
}
