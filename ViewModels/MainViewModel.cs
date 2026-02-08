using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Services;
using KamatekCrm.Views;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Ana ViewModel - Navigasyon kontrolü
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly NavigationService _navigationService;
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private object? _currentView;

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

        /// <summary>
        /// Dashboard sayfasına git komutu
        /// </summary>
        public ICommand NavigateToDashboardCommand { get; }

        /// <summary>
        /// Müşteriler sayfasına git komutu
        /// </summary>
        public ICommand NavigateToCustomersCommand { get; }

        /// <summary>
        /// Stoklar sayfasına git komutu
        /// </summary>
        public ICommand NavigateToProductsCommand { get; }

        /// <summary>
        /// İş Emirleri sayfasına git komutu
        /// </summary>
        public ICommand NavigateToServiceJobsCommand { get; }

        /// <summary>
        /// Tamir Listesi sayfasına git komutu
        /// </summary>
        public ICommand NavigateToRepairListCommand { get; }

        /// <summary>
        /// Saha İşleri sayfasına git komutu
        /// </summary>
        public ICommand NavigateToFieldJobListCommand { get; }

        /// <summary>
        /// Stok Sayım sayfasına git komutu
        /// </summary>
        public ICommand NavigateToStockCountCommand { get; }

        /// <summary>
        /// Stok Raporları sayfasına git komutu
        /// </summary>
        public ICommand NavigateToReportsCommand { get; }

        /// <summary>
        /// Kullanıcılar sayfasına git komutu
        /// </summary>
        public ICommand NavigateToUsersCommand { get; }

        /// <summary>
        /// Çıkış yap komutu
        /// </summary>
        public ICommand LogoutCommand { get; }

        /// <summary>
        /// Arıza Kaydı penceresi aç komutu
        /// </summary>
        public ICommand OpenFaultTicketCommand { get; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public MainViewModel(NavigationService navigationService, IAuthService authService, IToastService toastService, ILoadingService loadingService)
        {
            _navigationService = navigationService;
            _authService = authService;
            _toastService = toastService;
            _loadingService = loadingService;

            // Komutları tanımla
            NavigateToDashboardCommand = new RelayCommand(_ => NavigateToDashboard());
            NavigateToCustomersCommand = new RelayCommand(_ => NavigateToCustomers());
            NavigateToProductsCommand = new RelayCommand(_ => NavigateToProducts());
            NavigateToServiceJobsCommand = new RelayCommand(_ => NavigateToServiceJobs());
            NavigateToRepairListCommand = new RelayCommand(_ => NavigateToRepairList());
            NavigateToFieldJobListCommand = new RelayCommand(_ => NavigateToFieldJobList());
            NavigateToStockCountCommand = new RelayCommand(_ => NavigateToStockCount());
            NavigateToReportsCommand = new RelayCommand(_ => NavigateToReports());
            NavigateToUsersCommand = new RelayCommand(_ => NavigateToUsers(), _ => IsAdmin);
            LogoutCommand = new RelayCommand(_ => Logout());
            OpenFaultTicketCommand = new RelayCommand(_ => OpenFaultTicket());
            NavigateToSettingsCommand = new RelayCommand(_ => NavigateToSettings());

            // Varsayılan olarak Dashboard sayfasını göster
            NavigateToDashboard();
        }

        #region Navigation Methods

        private void NavigateToDashboard() => _navigationService.NavigateTo<DashboardViewModel>();
        private void NavigateToCustomers() => _navigationService.NavigateTo<CustomersViewModel>();
        private void NavigateToProducts() => _navigationService.NavigateTo<ProductViewModel>();
        private void NavigateToServiceJobs() => _navigationService.NavigateTo<ServiceJobViewModel>();
        public void NavigateToRepairList() => _navigationService.NavigateTo<RepairListViewModel>();
        public void NavigateToFieldJobList() => _navigationService.NavigateTo<FieldJobListViewModel>();
        private void NavigateToStockCount() => _navigationService.NavigateTo<StockCountViewModel>();
        private void NavigateToReports() => _navigationService.NavigateTo<StockReportsViewModel>();
        private void NavigateToUsers() => _navigationService.NavigateTo<UsersViewModel>();

        /// <summary>
        /// Müşteri detay sayfasına geçiş
        /// </summary>
        public void NavigateToCustomerDetail(int customerId)
        {
            // Parameter passing is tricky with simple NavigateTo<T>.
            // Ideally NavigationService should support Parameter navigation or we resolve factory.
            // For now, resolving manually via DI if possible, or using the old way if CustomerDetailViewModel is not fully DI ready yet.
            // But NavigationService no longer supports `new`.
            // Workaround: We need a way to pass parameters.
            // Let's assume CustomerDetailViewModel can be instantiated with ID provided via a service or messenger?
            // Or we use a Factory pattern.
            // Or we hack it for now: _navigationService.CurrentView = new CustomerDetailViewModel(customerId);
            // But CustomerDetailViewModel needs DI dependencies!
            // This requires a factory. 
            // For this specific case, I will leave it as `CurrentView = ...` but I need to resolve dependencies?
            // If CustomerDetailViewModel has dependencies, `new` will fail if I don't pass them.
            // I will default to `new CustomerDetailViewModel(customerId)` but I know this breaks DI rules.
            // I'll skip fixing parameterized navigation for this atomic step to avoid scope creep.
            // But I MUST fix the `_currentView` usage in this file to use `_navigationService`.
             _navigationService.CurrentView = new CustomerDetailViewModel(customerId, _navigationService, _toastService, _loadingService);
        }

        /// <summary>
        /// Arıza Kaydı penceresini aç
        /// </summary>
        private void OpenFaultTicket()
        {
            var window = new FaultTicketWindow();
            window.ShowDialog();
        }

        private void NavigateToSettings() => _navigationService.NavigateTo<SettingsViewModel>();

        /// <summary>
        /// Ayarlar sayfasına git komutu
        /// </summary>
        public ICommand NavigateToSettingsCommand { get; }

        #endregion

        /// <summary>
        /// Çıkış yap
        /// </summary>
        private void Logout()
        {
            var result = MessageBox.Show(
                "Çıkış yapmak istediğinizden emin misiniz?",
                "Çıkış",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _authService.Logout();

                // Uygulamayı yeniden başlat
                System.Diagnostics.Process.Start(System.Windows.Application.ResourceAssembly.Location.Replace(".dll", ".exe"));
                System.Windows.Application.Current.Shutdown();
            }
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

