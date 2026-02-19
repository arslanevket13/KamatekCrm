using System;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceProvider _serviceProvider; // Added for resolving child VMs

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
        /// Aktif görünüm (İçerik Alanı)
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
        public MainContentViewModel(
            IUnitOfWork unitOfWork, 
            IAuthService authService, 
            NavigationService navigationService, 
            IToastService toastService, 
            ILoadingService loadingService,
            IServiceProvider serviceProvider) // Inject IServiceProvider
        {
            _unitOfWork = unitOfWork;
            _authService = authService;
            _navigationService = navigationService;
            _toastService = toastService;
            _loadingService = loadingService;
            _serviceProvider = serviceProvider;

            // Global arama başlat
            SearchViewModel = new GlobalSearchViewModel();
            _notificationService = new NotificationService();

            // Kayıtlı tercihleri yükle
            _isSidebarCollapsed = Properties.Settings.Default.SidebarCollapsed;
            _isDarkMode = Properties.Settings.Default.IsDarkMode;

            NavigateToDashboardCommand = new RelayCommand(_ => NavigateTo<DashboardViewModel>());
            NavigateToCustomersCommand = new RelayCommand(_ => NavigateTo<CustomersViewModel>());
            NavigateToProductsCommand = new RelayCommand(_ => NavigateTo<ProductViewModel>());
            NavigateToServiceJobsCommand = new RelayCommand(_ => NavigateTo<ServiceJobViewModel>());
            NavigateToStockCountCommand = new RelayCommand(_ => NavigateTo<StockCountViewModel>());
            NavigateToReportsCommand = new RelayCommand(_ => NavigateTo<StockReportsViewModel>());
            NavigateToUsersCommand = new RelayCommand(_ => NavigateTo<UsersViewModel>(), _ => _authService.IsAdmin);
            NavigateToSystemLogsCommand = new RelayCommand(_ => NavigateTo<SystemLogsViewModel>(), _ => _authService.IsAdmin);
            LogoutCommand = new RelayCommand(_ => Logout());
            OpenFaultTicketCommand = new RelayCommand(_ => OpenFaultTicket());
            OpenProjectQuoteCommand = new RelayCommand(_ => OpenProjectQuote());
            OpenRepairTrackingCommand = new RelayCommand(_ => OpenRepairTracking());
            OpenDirectSalesCommand = new RelayCommand(_ => OpenDirectSales());
            NavigateToRepairListCommand = new RelayCommand(_ => NavigateTo<RepairListViewModel>());
            NavigateToFieldJobListCommand = new RelayCommand(_ => NavigateTo<FieldJobListViewModel>());
            NavigateToSettingsCommand = new RelayCommand(_ => NavigateTo<SettingsViewModel>(), _ => _authService.CanAccessSettings);
            NavigateToFinanceCommand = new RelayCommand(_ => NavigateTo<FinanceViewModel>(), _ => _authService.CanViewFinance);
            NavigateToAnalyticsCommand = new RelayCommand(_ => NavigateTo<AnalyticsViewModel>(), _ => _authService.CanViewAnalytics);
            NavigateToPurchaseOrdersCommand = new RelayCommand(_ => NavigateTo<PurchaseOrderViewModel>());
            NavigateToPipelineCommand = new RelayCommand(_ => NavigateTo<PipelineViewModel>());
            NavigateToSchedulerCommand = new RelayCommand(_ => NavigateTo<SchedulerViewModel>());
            ToggleNotificationsCommand = new RelayCommand(_ => IsNotificationsOpen = !IsNotificationsOpen);
            RefreshNotificationsCommand = new RelayCommand(_ => LoadNotifications());
            
            // Yeni komutlar
            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarCollapsed = !IsSidebarCollapsed);
            ToggleDarkModeCommand = new RelayCommand(_ => IsDarkMode = !IsDarkMode);
            OpenQuickAddCommand = new RelayCommand(_ => OpenQuickAdd());
            NavigateToFinancialHealthCommand = new RelayCommand(_ => NavigateTo<FinancialHealthViewModel>(), _ => _authService.CanViewFinance);
            NavigateToRoutePlanningCommand = new RelayCommand(_ => NavigateTo<RoutePlanningViewModel>());
            
            NavigateToSuppliersCommand = new RelayCommand(_ => NavigateTo<SuppliersViewModel>()); // Initialization

            LoadNotifications();

            // Varsayılan olarak Dashboard'u göster (Local Navigation)
            NavigateTo<DashboardViewModel>();
        }

        #region Navigation Methods

        /// <summary>
        /// Sets the inner content view locally, without affecting the global window navigation.
        /// </summary>
        /// <typeparam name="TViewModel"></typeparam>
        private void NavigateTo<TViewModel>() where TViewModel : notnull
        {
            try
            {
                var vm = _serviceProvider.GetRequiredService<TViewModel>();
                CurrentView = vm;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation Error to {typeof(TViewModel).Name}: {ex.Message}");
                _toastService.ShowError($"Sayfa yüklenemedi: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Müşteri detay sayfasına geçiş
        /// </summary>
        public void NavigateToCustomerDetail(int customerId)
        {
            var vm = _serviceProvider.GetRequiredService<CustomerDetailViewModel>();
            vm.Initialize(customerId);
            CurrentView = vm;
        }

        private void OpenFaultTicket()
        {
            // Yeni Cihaz Kabul Ekranı (Repair Module) — DI ile ViewModel çözümlenir
            var repairVm = _serviceProvider.GetRequiredService<RepairViewModel>();
            var window = new RepairRegistrationWindow(repairVm);
            window.ShowDialog();
        }

        private void OpenRepairTracking()
        {
            // Yeni Arıza Takip Merkezi (Repair Module) — DI ile ViewModel çözümlenir
            var repairVm = _serviceProvider.GetRequiredService<RepairViewModel>();
            var window = new RepairTrackingWindow(repairVm);
            window.Show();
        }

        private void OpenProjectQuote()
        {
            var window = _serviceProvider.GetRequiredService<ProjectQuoteEditorWindow>();
            window.ShowDialog();
        }

        private void OpenDirectSales()
        {
            // Perakende Satış — DI ile ViewModel çözümlenir
            var directSalesVm = _serviceProvider.GetRequiredService<DirectSalesViewModel>();
            var window = new DirectSalesWindow(directSalesVm);
            window.Show();
        }

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
                        NavigateTo<CustomersViewModel>(); // Fixed
                        break;
                    case "OpenProjectQuote":
                        OpenProjectQuote();
                        break;
                    case "NavigateDashboard":
                        NavigateTo<DashboardViewModel>(); // Fixed
                        break;
                    case "NavigateCustomers":
                        NavigateTo<CustomersViewModel>(); // Fixed
                        break;
                    case "NavigateProducts":
                        NavigateTo<ProductViewModel>(); // Fixed
                        break;
                    case "NavigateRepairList":
                        NavigateTo<RepairListViewModel>(); // Fixed
                        break;
                    case "NavigateFinance":
                        NavigateTo<FinanceViewModel>(); // Fixed
                        break;
                    case "NavigateSettings":
                        NavigateTo<SettingsViewModel>(); // Fixed
                        break;
                }
            };

            modal.ShowDialog();
        }
    }
}
