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
        public MainViewModel()
        {
            // Komutları tanımla
            NavigateToDashboardCommand = new RelayCommand(_ => NavigateToDashboard());
            NavigateToCustomersCommand = new RelayCommand(_ => NavigateToCustomers());
            NavigateToProductsCommand = new RelayCommand(_ => NavigateToProducts());
            NavigateToServiceJobsCommand = new RelayCommand(_ => NavigateToServiceJobs());
            NavigateToStockCountCommand = new RelayCommand(_ => NavigateToStockCount());
            NavigateToReportsCommand = new RelayCommand(_ => NavigateToReports());
            NavigateToUsersCommand = new RelayCommand(_ => NavigateToUsers(), _ => IsAdmin);
            LogoutCommand = new RelayCommand(_ => Logout());
            OpenFaultTicketCommand = new RelayCommand(_ => OpenFaultTicket());

            // Varsayılan olarak Dashboard sayfasını göster
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

        /// <summary>
        /// Müşteri detay sayfasına geçiş
        /// </summary>
        public void NavigateToCustomerDetail(int customerId)
        {
            CurrentView = new CustomerDetailViewModel(customerId);
        }

        /// <summary>
        /// Arıza Kaydı penceresini aç
        /// </summary>
        private void OpenFaultTicket()
        {
            var window = new FaultTicketWindow();
            window.ShowDialog();
        }

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
                AuthService.Logout();

                // Uygulamayı yeniden başlat
                System.Diagnostics.Process.Start(Application.ResourceAssembly.Location.Replace(".dll", ".exe"));
                Application.Current.Shutdown();
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

