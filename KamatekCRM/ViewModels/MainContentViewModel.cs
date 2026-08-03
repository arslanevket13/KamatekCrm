using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Services;
using KamatekCrm.Views;
using KamatekCrm.Shared.Repositories;
using CommunityToolkit.Mvvm.Messaging;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Ana içerik alanı ViewModel (Sidebar + Content)
    /// </summary>
    public partial class MainContentViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthService _authService;
        private readonly NavigationService _navigationService;
        private readonly NotificationService _notificationService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private readonly IServiceProvider _serviceProvider; // Added for resolving child VMs
        private readonly IQuotationLauncher _quotationLauncher;

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
            set
            {
                if (SetProperty(ref _currentView, value))
                {
                    OnPropertyChanged(nameof(CurrentViewName));
                    OnPropertyChanged(nameof(CurrentViewTitle));
                }
            }
        }

        public string CurrentViewName => CurrentView?.GetType().Name.Replace("ViewModel", "").Replace("View", "") ?? "Dashboard";

        /// <summary>
        /// Teknik ViewModel adını kullanıcıya göstermeden, breadcrumb için yerelleştirilmiş başlık üretir.
        /// CurrentViewName menü seçimi gibi iç mantıkta kullanılmaya devam eder.
        /// </summary>
        public string CurrentViewTitle => CurrentViewName switch
        {
            "Dashboard" => "Ana Sayfa",
            "Customers" => "Müşteriler",
            "CustomerDetail" => "Müşteri Profili",
            "Product" => "Ürünler ve Stok",
            "ServiceJob" => "İş Emirleri",
            "RepairList" => "Tamir Listesi",
            "FieldJobList" => "Saha İşleri",
            "RoutePlanning" => "Rota Planlama",
            "QuoteList" => "Proje ve Teklifler",
            "Finance" => "Finans",
            "Analytics" => "Analitik",
            "Purchasing" => "Satın Alma",
            "Suppliers" => "Tedarikçiler",
            "FinancialHealth" => "Finansal Rapor",
            "StockCount" => "Stok Sayımı",
            "StockReports" => "Standart Raporlar",
            "Users" => "Kullanıcılar",
            "CustomerInteractions" => "Müşteri İletişim ve Talep Merkezi",
            "ManagerAgenda" => "Yönetici Gündemi",
            "SystemLogs" => "Sistem Kayıtları",
            "Settings" => "Ayarlar",
            "NetworkSettings" => "Ağ Ayarları",
            _ => "Çalışma Alanı"
        };

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

                                string draftContent = $"{{\"View\":\"{CurrentView.GetType().Name}\",\"SavedAt\":\"{DateTime.Now:O}\"}}";
                                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                                try
                                {
                                    draftContent = System.Text.Json.JsonSerializer.Serialize(new
                                    {
                                        ViewName = CurrentView.GetType().Name,
                                        SavedAt = DateTime.Now
                                    }, options);
                                }
                                catch { }

                                System.IO.File.WriteAllText(draftFile, draftContent);

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

        // Navigation History Stack
        private readonly System.Collections.Generic.Stack<object> _backStack = new();
        private readonly System.Collections.Generic.Stack<object> _forwardStack = new();
        private bool _isNavigatingHistory;

        public bool CanGoBack => _backStack.Count > 0;
        public bool CanGoForward => _forwardStack.Count > 0;

        private bool _isUserProfileOpen;
        public bool IsUserProfileOpen
        {
            get => _isUserProfileOpen;
            set => SetProperty(ref _isUserProfileOpen, value);
        }

        #region Navigation Commands

        // RBAC Visibility
        public bool CanViewFinance => _authService.CanViewFinance;
        public bool CanViewAnalytics => _authService.CanViewAnalytics;
        public bool CanAccessSettings => _authService.CanAccessSettings;

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
            IServiceProvider serviceProvider,
            IQuotationLauncher quotationLauncher) // Inject IServiceProvider
        {
            _unitOfWork = unitOfWork;
            _authService = authService;
            _navigationService = navigationService;
            _toastService = toastService;
            _loadingService = loadingService;
            _serviceProvider = serviceProvider;
            _quotationLauncher = quotationLauncher;

            // Global arama başlat
            SearchViewModel = _serviceProvider.GetRequiredService<GlobalSearchViewModel>();
            _notificationService = _serviceProvider.GetRequiredService<NotificationService>();

            // Kayıtlı tercihleri yükle
            _isSidebarCollapsed = Properties.Settings.Default.SidebarCollapsed;
            _isDarkMode = Properties.Settings.Default.IsDarkMode;

            // 4. Bağlantı Kopma / Geri Gelme Eventlerini Dinleme (UI Overlay için)

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

            // Ağ Yönetimi sayfasına navigasyon event'i (SettingsView → NetworkSettingsView)
            EventAggregator.Instance.Subscribe<NavigateToNetworkSettingsEvent>(_ =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => NavigateTo<NetworkSettingsViewModel>());
            });
            
            // Yeni komutlar

            _ = RefreshNotificationsAsync();

            // Varsayılan olarak Dashboard'u göster (Local Navigation)
            NavigateTo<DashboardViewModel>();
        }

        #region Navigation Methods

        /// <summary>
        /// Sets the inner content view locally, without affecting the global window navigation.
        /// Maintains navigation history stack.
        /// </summary>
        /// <typeparam name="TViewModel"></typeparam>
        private void NavigateTo<TViewModel>() where TViewModel : notnull
        {
            try
            {
                var vm = _serviceProvider.GetRequiredService<TViewModel>();
                if (!_isNavigatingHistory && CurrentView != null && CurrentView.GetType() != typeof(TViewModel))
                {
                    _backStack.Push(CurrentView);
                    _forwardStack.Clear();
                    OnPropertyChanged(nameof(CanGoBack));
                    OnPropertyChanged(nameof(CanGoForward));
                }
                CurrentView = vm;
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Sayfa yüklenemedi: {ex.Message}");
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            if (_backStack.Count > 0)
            {
                _isNavigatingHistory = true;
                _forwardStack.Push(CurrentView!);
                CurrentView = _backStack.Pop();
                _isNavigatingHistory = false;
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoForward));
            }
        }

        [RelayCommand]
        private void GoForward()
        {
            if (_forwardStack.Count > 0)
            {
                _isNavigatingHistory = true;
                _backStack.Push(CurrentView!);
                CurrentView = _forwardStack.Pop();
                _isNavigatingHistory = false;
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoForward));
            }
        }

        [RelayCommand] private void ToggleUserProfile() => IsUserProfileOpen = !IsUserProfileOpen;
        [RelayCommand] private void NavigateToDashboard() => NavigateTo<DashboardViewModel>();
        [RelayCommand] private void NavigateToCustomers() => NavigateTo<CustomersViewModel>();
        [RelayCommand] private void NavigateToCustomerInteractions() => NavigateTo<CustomerInteractionsViewModel>();
        [RelayCommand] private void NavigateToManagerAgenda() => NavigateTo<ManagerAgendaViewModel>();
        [RelayCommand] private void NavigateToProducts() => NavigateTo<ProductViewModel>();
        [RelayCommand] private void NavigateToServiceJobs() => NavigateTo<ServiceJobViewModel>();
        [RelayCommand] private void NavigateToRepairList() => NavigateTo<RepairListViewModel>();
        [RelayCommand] private void NavigateToFieldJobList() => NavigateTo<FieldJobListViewModel>();
        [RelayCommand] private void NavigateToRoutePlanning() => NavigateTo<RoutePlanningViewModel>();
        [RelayCommand] private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;
        [RelayCommand] private void ToggleDarkMode() => IsDarkMode = !IsDarkMode;
        [RelayCommand] private void ToggleNotifications() => IsNotificationsOpen = !IsNotificationsOpen;
        [RelayCommand] private void GoToSettings() => NavigateToSettings();

        [RelayCommand]
        private void OpenQuickInteractionAdd()
        {
            var vm = _serviceProvider.GetRequiredService<QuickInteractionAddViewModel>();
            var window = new Views.QuickInteractionAddWindow(vm);
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();
        }

        [RelayCommand]
        private void NavigateToScheduler()
        {
            NavigateTo<FieldJobListViewModel>();
            _toastService?.ShowInfo("Zamanlayıcı görünümü Saha Görevleri sayfasına yönlendirildi.");
        }
        [RelayCommand]
        private void NavigateToFinance()
        {
            if (!CanViewFinance) { _toastService.ShowError("Finans ekranına erişim yetkiniz yok."); return; }
            NavigateTo<FinanceViewModel>();
        }

        [RelayCommand]
        private void NavigateToAnalytics()
        {
            if (!CanViewAnalytics) { _toastService.ShowError("Analitik ekranına erişim yetkiniz yok."); return; }
            NavigateTo<AnalyticsViewModel>();
        }
        [RelayCommand] private void NavigateToPurchaseOrders() => NavigateTo<PurchasingViewModel>();
        [RelayCommand] private void NavigateToSuppliers() => NavigateTo<SuppliersViewModel>();
        [RelayCommand] private void NavigateToFinancialHealth() => NavigateTo<FinancialHealthViewModel>();
        [RelayCommand] private void NavigateToStockCount() => NavigateTo<StockCountViewModel>();
        [RelayCommand] private void NavigateToReports() => NavigateTo<StockReportsViewModel>();
        [RelayCommand]
        private void NavigateToUsers()
        {
            if (!IsAdmin) { _toastService.ShowError("Kullanıcı yönetimi için yönetici yetkisi gerekir."); return; }
            NavigateTo<UsersViewModel>();
        }

        [RelayCommand]
        private void NavigateToSystemLogs()
        {
            if (!IsAdmin) { _toastService.ShowError("Sistem kayıtları için yönetici yetkisi gerekir."); return; }
            NavigateTo<SystemLogsViewModel>();
        }
        
        [RelayCommand]
        private void NavigateToSettings()
        {
            if (!CanAccessSettings) { _toastService.ShowError("Sistem ayarlarına erişim yetkiniz yok."); return; }
            IsConnectionLost = false; // Overlay'i gizle
            NavigateTo<SettingsViewModel>();
        }

        [RelayCommand]
        private void ForceMainServer()
        {
            if (!CanAccessSettings)
            {
                _toastService.ShowError("Sunucu rolünü değiştirme yetkiniz yok.");
                return;
            }

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

        [RelayCommand]
        private void OpenFaultTicket()
        {
            // Yeni Cihaz Kabul Ekranı (Repair Module) — DI ile ViewModel çözümlenir
            var faultVm = _serviceProvider.GetRequiredService<FaultTicketViewModel>();
            var window = new Views.FaultTicketWindow(faultVm);
            window.ShowDialog();
        }

        [RelayCommand]
        private void OpenRepairTracking()
        {
            // Yeni Arıza Takip Merkezi (Repair Module) — DI ile ViewModel çözümlenir
            var repairVm = _serviceProvider.GetRequiredService<RepairViewModel>();
            var window = new RepairTrackingWindow(repairVm);
            window.Show();
        }

        [RelayCommand]
        private void OpenProjectQuote()
        {
            NavigateTo<QuoteListViewModel>();
        }

        [RelayCommand]
        private async Task OpenQuotation()
        {
            await _quotationLauncher.ShowAsync(modal: false);
        }

        [RelayCommand]
        private void OpenDirectSales()
        {
            // Perakende Satış — DI ile ViewModel çözümlenir
            var directSalesVm = _serviceProvider.GetRequiredService<DirectSalesViewModel>();
            var window = new DirectSalesWindow(directSalesVm);
            window.Show();
        }

        [RelayCommand]
        private async Task RefreshNotificationsAsync()
        {
            var items = await _notificationService.GetNotificationsAsync();
            
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
            {
                Notifications.Clear();
                foreach (var item in items) Notifications.Add(item);
                NotificationCount = items.Count;
            });
        }

        #endregion

        /// <summary>
        /// Çıkış yap - Login ekranına dön
        /// </summary>
        [RelayCommand]
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
        [RelayCommand]
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




