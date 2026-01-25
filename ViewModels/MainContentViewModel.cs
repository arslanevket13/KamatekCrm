using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Services;
using KamatekCrm.Views;

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

        // RBAC Visibility
        public bool CanViewFinance => AuthService.CanViewFinance;
        public bool CanViewAnalytics => AuthService.CanViewAnalytics;
        public bool CanAccessSettings => AuthService.CanAccessSettings;

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public MainContentViewModel()
        {
            // Global arama başlat
            SearchViewModel = new GlobalSearchViewModel();

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
            NavigateToRepairListCommand = new RelayCommand(_ => NavigateToRepairList());
            NavigateToFieldJobListCommand = new RelayCommand(_ => NavigateToFieldJobList());
            NavigateToSettingsCommand = new RelayCommand(_ => NavigateToSettings(), _ => CanAccessSettings);
            NavigateToFinanceCommand = new RelayCommand(_ => NavigateToFinance(), _ => CanViewFinance);
            NavigateToAnalyticsCommand = new RelayCommand(_ => NavigateToAnalytics(), _ => CanViewAnalytics);
            NavigateToPurchaseOrdersCommand = new RelayCommand(_ => NavigateToPurchaseOrders());

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
        private void NavigateToPurchaseOrders() => CurrentView = new PurchaseOrderViewModel();

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
    }
}
