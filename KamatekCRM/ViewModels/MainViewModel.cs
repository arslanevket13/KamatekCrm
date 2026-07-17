using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Services;
using KamatekCrm.Views;
using CommunityToolkit.Mvvm.Messaging;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Ana ViewModel - Navigasyon kontrolü
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        private readonly NavigationService _navigationService;
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private object? _currentView;
        private bool _isConnectionLost;

        /// <summary>
        /// Ağ veya veritabanı bağlantısı koptuğunda true olur (Overlay göstermek için)
        /// </summary>
        public bool IsConnectionLost
        {
            get => _isConnectionLost;
            set => SetProperty(ref _isConnectionLost, value);
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

        /// <summary>
        /// Dashboard sayfasına git komutu
        /// </summary>

        /// <summary>
        /// Müşteriler sayfasına git komutu
        /// </summary>

        /// <summary>
        /// Stoklar sayfasına git komutu
        /// </summary>

        /// <summary>
        /// İş Emirleri sayfasına git komutu
        /// </summary>

        /// <summary>
        /// Tamir Listesi sayfasına git komutu
        /// </summary>

        /// <summary>
        /// Saha İşleri sayfasına git komutu
        /// </summary>

        /// <summary>
        /// Stok Sayım sayfasına git komutu
        /// </summary>

        /// <summary>
        /// Stok Raporları sayfasına git komutu
        /// </summary>

        /// <summary>
        /// Kullanıcılar sayfasına git komutu
        /// </summary>

        /// <summary>
        /// Çıkış yap komutu
        /// </summary>

        /// <summary>
        /// Arıza Kaydı penceresi aç komutu
        /// </summary>

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

            // 401 Unauthorized yakalama
            WeakReferenceMessenger.Default.Register<UnauthorizedMessage>(this, (r, m) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ForceLogout();
                });
            });

            // Komutları tanımla

            // Varsayılan olarak Dashboard sayfasını göster
            NavigateToDashboard();
        }

        #region Navigation Methods

        [RelayCommand]
        private void NavigateToDashboard() => _navigationService.NavigateTo<DashboardViewModel>();
        [RelayCommand]
        private void NavigateToCustomers() => _navigationService.NavigateTo<CustomersViewModel>();
        [RelayCommand]
        private void NavigateToProducts() => _navigationService.NavigateTo<ProductViewModel>();
        [RelayCommand]
        private void NavigateToServiceJobs() => _navigationService.NavigateTo<ServiceJobViewModel>();
        [RelayCommand]
        public void NavigateToRepairList() => _navigationService.NavigateTo<RepairListViewModel>();
        [RelayCommand]
        public void NavigateToFieldJobList() => _navigationService.NavigateTo<FieldJobListViewModel>();
        [RelayCommand]
        private void NavigateToStockCount() => _navigationService.NavigateTo<StockCountViewModel>();
        [RelayCommand]
        private void NavigateToReports() => _navigationService.NavigateTo<StockReportsViewModel>();
        [RelayCommand]
        private void NavigateToUsers() => _navigationService.NavigateTo<UsersViewModel>();

        /// <summary>
        /// Müşteri detay sayfasına geçiş
        /// </summary>
        public void NavigateToCustomerDetail(int customerId)
        {
             var vm = _navigationService.NavigateTo<CustomerDetailViewModel>();
             vm.Initialize(customerId);
        }

        /// <summary>
        /// Arıza Kaydı penceresini aç
        /// </summary>
        [RelayCommand]
        private void OpenFaultTicket()
        {
            // Arıza Kaydı — DI ile ViewModel çözümlenir
            var faultTicketVm = App.ServiceProvider.GetRequiredService<FaultTicketViewModel>();
            var window = new FaultTicketWindow(faultTicketVm);
            window.ShowDialog();
        }

        [RelayCommand]
        private void NavigateToSettings() => _navigationService.NavigateTo<SettingsViewModel>();

        [RelayCommand]
        private void GoToSettings()
        {
            IsConnectionLost = false; // Overlay'i gizle
            _navigationService.NavigateTo<SettingsViewModel>();
        }

        /// <summary>
        /// Ayarlar sayfasına git komutu
        /// </summary>

        /// <summary>
        /// Kilitlenmeyi aşarak Ayarlar sayfasına git komutu
        /// </summary>

        #endregion

        /// <summary>
        /// Çıkış yap
        /// </summary>
        [RelayCommand]
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

        private void ForceLogout()
        {
            _authService.Logout();
            MessageBox.Show("Oturum süreniz doldu veya yetkiniz yok. Tekrar giriş yapın.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            System.Diagnostics.Process.Start(System.Windows.Application.ResourceAssembly.Location.Replace(".dll", ".exe"));
            System.Windows.Application.Current.Shutdown();
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


