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
        private object? _currentView;

        /// <summary>
        /// Global arama ViewModel
        /// </summary>
        public GlobalSearchViewModel SearchViewModel { get; }

        private readonly NotificationService _notificationService;
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
        public string CurrentUserName => AuthService.CurrentUser?.AdSoyad ?? "Misafir";

        /// <summary>
        /// Mevcut kullanıcı rol gösterimi
        /// </summary>
        public string CurrentUserRole => GetDisplayRole(AuthService.CurrentUser?.Role);

        /// <summary>
        /// Admin mi?
        /// </summary>
        public bool IsAdmin => AuthService.IsAdmin;

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
        public bool CanViewFinance => AuthService.CanViewFinance;
        public bool CanViewAnalytics => AuthService.CanViewAnalytics;
        public bool CanAccessSettings => AuthService.CanAccessSettings;

        // Finansal Sağlık Komutu
        public ICommand NavigateToFinancialHealthCommand { get; }

        public ICommand NavigateToRoutePlanningCommand { get; }

        #endregion

        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Constructor
        /// </summary>
        public MainContentViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;

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
            NavigateToUsersCommand = new RelayCommand(_ => NavigateToUsers(), _ => IsAdmin);
            NavigateToSystemLogsCommand = new RelayCommand(_ => NavigateToSystemLogs(), _ => IsAdmin);
            LogoutCommand = new RelayCommand(_ => Logout());
            OpenFaultTicketCommand = new RelayCommand(_ => OpenFaultTicket());
            OpenProjectQuoteCommand = new RelayCommand(_ => OpenProjectQuote());
            OpenRepairTrackingCommand = new RelayCommand(_ => OpenRepairTracking());
            OpenDirectSalesCommand = new RelayCommand(_ => OpenDirectSales());
            NavigateToRepairListCommand = new RelayCommand(_ => NavigateToRepairList());
            NavigateToFieldJobListCommand = new RelayCommand(_ => NavigateToFieldJobList());
            NavigateToSettingsCommand = new RelayCommand(_ => NavigateToSettings(), _ => CanAccessSettings);
            NavigateToFinanceCommand = new RelayCommand(_ => NavigateToFinance(), _ => CanViewFinance);
            NavigateToAnalyticsCommand = new RelayCommand(_ => NavigateToAnalytics(), _ => CanViewAnalytics);
            NavigateToPurchaseOrdersCommand = new RelayCommand(_ => NavigateToPurchaseOrders());
            NavigateToPipelineCommand = new RelayCommand(_ => NavigateToPipeline());
            NavigateToSchedulerCommand = new RelayCommand(_ => NavigateToScheduler());
            ToggleNotificationsCommand = new RelayCommand(_ => IsNotificationsOpen = !IsNotificationsOpen);
            RefreshNotificationsCommand = new RelayCommand(_ => LoadNotifications());
            
            // Yeni komutlar
            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarCollapsed = !IsSidebarCollapsed);
            ToggleDarkModeCommand = new RelayCommand(_ => IsDarkMode = !IsDarkMode);
            OpenQuickAddCommand = new RelayCommand(_ => OpenQuickAdd());
            NavigateToFinancialHealthCommand = new RelayCommand(_ => NavigateToFinancialHealth(), _ => CanViewFinance);
            NavigateToRoutePlanningCommand = new RelayCommand(_ => NavigateToRoutePlanning());
            
            NavigateToSuppliersCommand = new RelayCommand(_ => NavigateToSuppliers()); // Initialization

            LoadNotifications();

            // Varsayılan olarak Dashboard'u göster
            NavigateToDashboard();
        }

        #region Navigation Methods

        private void NavigateToDashboard() => CurrentView = new DashboardViewModel();
        private void NavigateToCustomers() => CurrentView = new CustomersViewModel();
        private void NavigateToProducts() => CurrentView = new ProductViewModel();
        private void NavigateToServiceJobs() => CurrentView = new ServiceJobViewModel();
        private void NavigateToStockCount() => CurrentView = new StockCountViewModel();
        private void NavigateToReports() => CurrentView = new StockReportsViewModel();
        private void NavigateToUsers() => CurrentView = new UsersViewModel();
        private void NavigateToSystemLogs() => CurrentView = new SystemLogsViewModel();
        private void NavigateToRepairList() => CurrentView = new RepairListViewModel();
        private void NavigateToFieldJobList() => CurrentView = new FieldJobListViewModel();

        /// <summary>
        /// Müşteri detay sayfasına geçiş
        /// </summary>
        public void NavigateToCustomerDetail(int customerId)
        {
            CurrentView = new CustomerDetailViewModel(customerId);
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

        private void NavigateToSettings() => CurrentView = new SettingsViewModel();
        private void NavigateToFinance() => CurrentView = new FinanceViewModel();
        private void NavigateToAnalytics() => CurrentView = new AnalyticsViewModel();
        private void NavigateToFinancialHealth() => CurrentView = new FinancialHealthViewModel();
        private void NavigateToPurchaseOrders() => CurrentView = new PurchaseOrderViewModel(_unitOfWork);
        private void NavigateToSuppliers() => CurrentView = new SuppliersViewModel(_unitOfWork); // Implementation
        private void NavigateToPipeline() => CurrentView = new PipelineViewModel();
        private void NavigateToScheduler() => CurrentView = new SchedulerViewModel();
        private void NavigateToRoutePlanning() => CurrentView = new RoutePlanningViewModel();


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
            AuthService.Logout();
            NavigationService.Instance.NavigateToLogin();
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
                }
            };

            modal.ShowDialog();
        }
    }
}
