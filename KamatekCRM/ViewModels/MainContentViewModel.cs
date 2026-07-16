using System;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Services;
using KamatekCrm.Views;
using KamatekCrm.Repositories;
using CommunityToolkit.Mvvm.Messaging;

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
                    ThemeService.ChangeTheme(value ? "MidnightDark" : "PremiumLight");
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

        private bool _isConnectionLost;
        /// <summary>
        /// Ağ veya veritabanı bağlantısı koptuğunda true olur (Overlay göstermek için)
        /// </summary>
        private bool _isForceServerButtonVisible;
        private System.Timers.Timer? _fallbackTimer;

        public bool IsForceServerButtonVisible
        {
            get => _isForceServerButtonVisible;
            set => SetProperty(ref _isForceServerButtonVisible, value);
        }

        public bool IsConnectionLost
        {
            get => _isConnectionLost;
            set 
            {
                if (SetProperty(ref _isConnectionLost, value))
                {
                    if (value)
                    {
                        // 1. Emergency Draft Save Logic (Data Loss Prevention)
                        if (CurrentView != null)
                        {
                            try 
                            {
                                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                                string kamatekFolder = System.IO.Path.Combine(appDataPath, "KamatekCRM");
                                System.IO.Directory.CreateDirectory(kamatekFolder);
                                string draftFile = System.IO.Path.Combine(kamatekFolder, "emergency_draft.json");

                                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                                string json = System.Text.Json.JsonSerializer.Serialize(CurrentView, CurrentView.GetType(), options);
                                System.IO.File.WriteAllText(draftFile, json);

                                // Background thread warning via Dispatcher
                                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                                {
                                    _toastService?.ShowWarning("Bağlantı koptu. Mevcut form verileriniz yerel taslak olarak kaydedildi.");
                                });
                            }
                            catch (Exception ex) 
                            { 
                                System.Diagnostics.Debug.WriteLine($"Acil taslak kaydedilemedi: {ex.Message}");
                            }
                        }

                        // 2. Start 15s fallback timer
                        if (_fallbackTimer == null)
                        {
                            _fallbackTimer = new System.Timers.Timer(15000);
                            _fallbackTimer.AutoReset = false;
                            _fallbackTimer.Elapsed += (s, e) => 
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                                {
                                    IsForceServerButtonVisible = true;
                                });
                            };
                        }
                        IsForceServerButtonVisible = false;
                        _fallbackTimer.Start();
                    }
                    else
                    {
                        _fallbackTimer?.Stop();
                        IsForceServerButtonVisible = false;
                    }
                }
            }
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
        public ICommand OpenQuotationCommand { get; }
        public ICommand OpenRepairTrackingCommand { get; }
        public ICommand OpenDirectSalesCommand { get; }
        public ICommand NavigateToRepairListCommand { get; }
        public ICommand NavigateToFieldJobListCommand { get; }
        public ICommand NavigateToSettingsCommand { get; }
        public ICommand NavigateToFinanceCommand { get; }
        public ICommand NavigateToAnalyticsCommand { get; }
        public ICommand NavigateToPurchaseOrdersCommand { get; }
        public ICommand NavigateToSuppliersCommand { get; }
        public ICommand NavigateToPipelineCommand { get; }
        public ICommand NavigateToSchedulerCommand { get; }

        // Methods

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

        public ICommand GoToSettingsCommand { get; }
        
        public ICommand ForceMainServerCommand { get; }

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

            // 4. Bağlantı Kopma / Geri Gelme Eventlerini Dinleme (UI Overlay için)
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<UnauthorizedMessage>(this, (r, m) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Logout();
                });
            });

            EventAggregator.Instance.Subscribe<DatabaseConnectionLostEvent>(_ =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    if (CurrentView is SettingsViewModel) return;
                    IsConnectionLost = true;
                });
            });

            EventAggregator.Instance.Subscribe<DatabaseConnectionRestoredEvent>(_ =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsConnectionLost = false);
            });

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
            OpenQuotationCommand = new RelayCommand(_ => OpenQuotation());
            OpenRepairTrackingCommand = new RelayCommand(_ => OpenRepairTracking());
            OpenDirectSalesCommand = new RelayCommand(_ => OpenDirectSales());
            NavigateToRepairListCommand = new RelayCommand(_ => NavigateTo<RepairListViewModel>());
            NavigateToFieldJobListCommand = new RelayCommand(_ => NavigateTo<FieldJobListViewModel>());
            NavigateToSettingsCommand = new RelayCommand(_ => NavigateTo<SettingsViewModel>(), _ => _authService.CanAccessSettings);
            NavigateToFinanceCommand = new RelayCommand(_ => NavigateTo<FinanceViewModel>(), _ => _authService.CanViewFinance);
            NavigateToAnalyticsCommand = new RelayCommand(_ => NavigateTo<AnalyticsViewModel>(), _ => _authService.CanViewAnalytics);
            NavigateToPurchaseOrdersCommand = new RelayCommand(_ => NavigateTo<PurchasingViewModel>());
            ToggleNotificationsCommand = new RelayCommand(_ => IsNotificationsOpen = !IsNotificationsOpen);
            RefreshNotificationsCommand = new RelayCommand(_ => LoadNotifications());
            ForceMainServerCommand = new RelayCommand(_ => ForceMainServer());
            
            // Yeni komutlar
            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarCollapsed = !IsSidebarCollapsed);
            ToggleDarkModeCommand = new RelayCommand(_ => IsDarkMode = !IsDarkMode);
            OpenQuickAddCommand = new RelayCommand(_ => OpenQuickAdd());
            NavigateToFinancialHealthCommand = new RelayCommand(_ => NavigateTo<FinancialHealthViewModel>(), _ => _authService.CanViewFinance);
            NavigateToRoutePlanningCommand = new RelayCommand(_ => NavigateTo<RoutePlanningViewModel>());
            
            GoToSettingsCommand = new RelayCommand(_ => GoToSettings());

            NavigateToSuppliersCommand = new RelayCommand(_ => NavigateTo<SuppliersViewModel>());
            NavigateToPipelineCommand = new RelayCommand(_ => _toastService.ShowInfo("Satış Pipeline modülü yapım aşamasındadır."));
            NavigateToSchedulerCommand = new RelayCommand(_ => _toastService.ShowInfo("Takvim modülü yapım aşamasındadır."));

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
                // System.Diagnostics.Debug.WriteLine($"Navigation Error to {typeof(TViewModel).Name}: {ex.Message}");
                _toastService.ShowError($"Sayfa yüklenemedi: {ex.Message}");
            }
        }

        private void GoToSettings()
        {
            IsConnectionLost = false; // Overlay'i gizle
            NavigateTo<SettingsViewModel>();
        }

        private void ForceMainServer()
        {
            // Zorla Ana Sunucu moduna geç ve yeniden başlat
            Properties.Settings.Default.IsMainServerManualOverride = true;
            Properties.Settings.Default.IsMainServer = true;
            Properties.Settings.Default.Save();

            try
            {
                // Appsettings.json fallback
                string appSettingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!System.IO.File.Exists(appSettingsPath))
                {
                    string? projectRoot = System.IO.Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName;
                    if (!string.IsNullOrEmpty(projectRoot)) appSettingsPath = System.IO.Path.Combine(projectRoot, "appsettings.json");
                }

                if (System.IO.File.Exists(appSettingsPath))
                {
                    string jsonString = System.IO.File.ReadAllText(appSettingsPath);
                    var jsonObject = System.Text.Json.Nodes.JsonNode.Parse(jsonString);
                    if (jsonObject != null && jsonObject["NetworkDiscovery"] != null)
                    {
                        jsonObject["NetworkDiscovery"]!["IsMainServer"] = true;
                        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                        System.IO.File.WriteAllText(appSettingsPath, jsonObject.ToJsonString(options));
                    }
                }
            }
            catch { }

            var moduleName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(moduleName))
            {
                System.Diagnostics.Process.Start(moduleName);
            }
            System.Windows.Application.Current.Shutdown();
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
            var faultVm = _serviceProvider.GetRequiredService<FaultTicketViewModel>();
            var window = new Views.FaultTicketWindow(faultVm);
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
            NavigateTo<QuoteListViewModel>();
        }

        private void OpenQuotation()
        {
            var window = new Views.QuotationWindow();
            window.Show();
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
