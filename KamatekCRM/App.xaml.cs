using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using KamatekCrm.Services;
using KamatekCrm.Extensions;
using KamatekCrm.Helpers;
using KamatekCrm.ViewModels;
using Microsoft.Extensions.Configuration;
using KamatekCrm.Configuration;
using Serilog;
using Wpf.Ui.Appearance;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;

namespace KamatekCrm
{
    /// <summary>
    /// WPF masaüstü uygulaması başlangıç noktası.
    /// API kullanılmadan, uygulama ve altyapı katmanları üzerinden PostgreSQL'e bağlanır.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private IHost? _host;
        private readonly System.Threading.CancellationTokenSource _appCts = new();

        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        public static KamatekCrm.Shared.Models.User? CurrentUser { get; set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Velopack Bootstrap — Uygulama başlangıcının en ilk adımı (Installer/Hook yönetimi)
            try
            {
                Velopack.VelopackApp.Build().Run();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Velopack bootstrap: {ex.Message}");
            }

            // WPF binding formatları işletim sistemi dilinden bağımsız olarak Türkçe CRM
            // bağlamında para, sayı ve tarih üretmelidir.
            var applicationCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = applicationCulture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = applicationCulture;
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    System.Windows.Markup.XmlLanguage.GetLanguage(applicationCulture.IetfLanguageTag)));

            // PostgreSQL Legacy Timestamp Behavior (Fix for Kind=Local error)
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            // WPF bağlama hatalarını geliştirme sırasında görünür kıl (yalnızca DEBUG derlemeleri)
            // Tüm hatalar Debug çıktısına; "TwoWay or OneWayToSource" sınıfı ayrıca popup'a düşer.
#if DEBUG
            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners.Add(new Diagnostics.BindingErrorTraceListener());
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
#endif

            // Logging'i ilk iş olarak yapılandır
            LoggingConfiguration.ConfigureLogging();
            
            try
            {
                Log.Information("=== KamatekCRM Desktop Starting (Direct Database Mode) ===");
                
                // ÖNCE base.OnStartup çağrılmalı - WPF-UI için önemli!
                base.OnStartup(e);

                // WPF-UI Theme - OnStartup'tan SONRA uygula
                ApplicationThemeManager.Apply(ApplicationTheme.Light);

                // Kendi tema ve stil yapılandırmamızı uygula
                // WPF-UI'ın enjekte ettiği ScrollBar/ScrollViewer stillerini ezer
                ThemeService.Initialize();

                // Host Builder'ı yapılandır — SADECE WPF DI, web server YOK
                _host = Host.CreateDefaultBuilder()
                    .UseSerilog()
                    .ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    })
                    .ConfigureServices((context, services) =>
                    {
                        // JWT Token Storage Removed
                        
                        // WPF servisleri kaydet (DB, ViewModels, Navigation vs.)
                        services.AddApplicationServices(context.Configuration);
                        
                        // MainWindow'u DI container'a ekle
                        services.AddTransient<MainWindow>();

                        // Arka plan servisleri (IHostedService)
                        services.AddSingleton<NetworkDiscoveryService>();
                        services.AddHostedService(provider => provider.GetRequiredService<NetworkDiscoveryService>());

                        services.AddSingleton<ConnectionHeartbeatService>();
                        services.AddHostedService(provider => provider.GetRequiredService<ConnectionHeartbeatService>());
                    })
                    .Build();

                // Service Provider'ı global erişime aç
                ServiceProvider = _host.Services;

                // Global WPF exception handler'ları kur
                DispatcherUnhandledException += OnDispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                await _host.StartAsync();

                // Şema hazırlama Infrastructure katmanında yürütülür; UI sağlayıcıya özel DDL içermez.
                try
                {
                    var databaseInitializer = ServiceProvider.GetRequiredService<IDatabaseInitializationService>();
                    var initializationResult = await databaseInitializer.InitializeAsync();
                    if (initializationResult.AdminCreated)
                    {
                        MessageBox.Show(
                            $"İlk yönetici hesabı oluşturuldu.\n\nKullanıcı adı: admin\nGeçici şifre: {initializationResult.TemporaryAdminPassword}\n\nBu şifre yalnızca bir kez gösterilir. Giriş yaptıktan sonra hemen değiştirin.",
                            "Güvenli İlk Kurulum",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Veritabanı otomatik kurulumu sırasında hata oluştu. Lütfen bağlantı ayarlarını kontrol edin.");
                }


                // MainWindow'u DI'dan al ve göster
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                MainWindow = mainWindow;
                
                // Login ekranını aktif et
                var navigationService = _host.Services.GetRequiredService<NavigationService>();
                navigationService.NavigateToLogin();

                mainWindow.Show();
                
                Log.Information("Desktop application started successfully.");

                // Açılış sonrasında ana pencere ve login akışını bloklamadan güncelleme denetimi (5s gecikmeli, await edilebilir)
                await CheckStartupUpdatesAsync(_appCts.Token);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Uygulama başlatılırken kritik hata");
                MessageBox.Show(
                    $"Uygulama başlatılırken hata oluştu:\n\n{ex.Message}\n\nDetay: {ex.InnerException?.Message}",
                    "Başlatma Hatası",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// Handles unhandled UI thread exceptions gracefully.
        /// Bu handler zaten Dispatcher (UI) thread üzerinde çalışır.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs args)
        {
            var exception = args.Exception;

            // ── Ağ hataları ──
            if (exception is HttpRequestException httpEx)
            {
                Log.Error(httpEx, "HTTP Request Exception - Server unreachable");
                ShowErrorToast("Sunucuya bağlanılamıyor. İnternet bağlantınızı kontrol edin.");
                args.Handled = true;
                return;
            }

            // ── Timeout / iptal hataları ──
            if (exception is TaskCanceledException or OperationCanceledException)
            {
                Log.Warning(exception, "Task cancelled/timeout");
                ShowErrorToast("İşlem zaman aşımına uğradı. Lütfen tekrar deneyin.");
                args.Handled = true;
                return;
            }

            // ── Diğer tüm hatalar ──
            Log.Error(exception, "Unhandled UI exception: {Message}", exception.Message);
            ShowErrorToast($"Beklenmeyen bir hata oluştu: {exception.Message}");

            // Uygulamanın çökmesini engelle — hata loglandı ve kullanıcıya bildirildi
            args.Handled = true;
        }

        /// <summary>
        /// Handles unhandled non-UI (AppDomain) exceptions.
        /// DİKKAT: Bu handler HERHANGİ bir thread'den tetiklenebilir.
        /// UI güncellemeleri Dispatcher üzerinden yapılmalıdır.
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            var exception = args.ExceptionObject as Exception;
            Log.Fatal(exception, "Unhandled domain exception - IsTerminating: {IsTerminating}", args.IsTerminating);

            if (args.IsTerminating)
            {
                // Uygulama kapanıyor — Dispatcher üzerinden son bir bilgilendirme yap.
                // BeginInvoke kullanıyoruz çünkü thread zaten ölüyor olabilir.
                try
                {
                    Current?.Dispatcher?.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"Kritik bir hata oluştu ve uygulama kapanıyor:\n\n{exception?.Message}",
                            "Kritik Hata",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                }
                catch
                {
                    // Dispatcher erişilemiyorsa sessizce geç — log zaten yazıldı
                }
            }
            else
            {
                ShowErrorToast($"Arka plan hatası: {exception?.Message}");
            }
        }

        /// <summary>
        /// Handles unobserved task exceptions (await edilmemiş Task'lardaki hatalar).
        /// DİKKAT: Bu handler Finalizer thread'inden tetiklenir — kesinlikle UI thread değildir.
        /// </summary>
        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            // Flatten ile iç içe AggregateException'ları düzleştir
            var flattenedException = args.Exception?.Flatten();
            var innerException = flattenedException?.InnerExceptions.Count > 0
                ? flattenedException.InnerExceptions[0]
                : args.Exception;

            Log.Error(innerException, "Unobserved task exception: {Message}", innerException?.Message);

            // GC'nin process'i sonlandırmasını engelle
            args.SetObserved();

            // Kullanıcıya thread-safe bildirim gönder
            ShowErrorToast($"Arka plan işleminde hata: {innerException?.Message}");
        }

        /// <summary>
        /// Thread-safe hata bildirimi.
        /// Hangi thread'den çağrılırsa çağrılsın, UI güncellemesi her zaman
        /// Dispatcher (UI thread) üzerinden yapılır.
        /// </summary>
        private void ShowErrorToast(string message)
        {
            try
            {
                // Dispatcher erişilebilir mi kontrol et
                var dispatcher = Current?.Dispatcher;
                if (dispatcher == null || dispatcher.HasShutdownStarted)
                {
                    // Uygulama kapanıyorsa sadece logla
                    Log.Warning("ShowErrorToast called during shutdown: {Message}", message);
                    return;
                }

                // Her zaman UI thread üzerinden çalıştır (fire-and-forget, bloklama yok)
                dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var toastService = ServiceProvider?.GetService(typeof(ToastService)) as ToastService;
                        if (toastService != null)
                        {
                            toastService.ShowError(message);
                        }
                        else
                        {
                            // ToastService henüz hazır değilse fallback MessageBox
                            MessageBox.Show(message, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Toast gösterimi de başarısız olursa sadece logla — sonsuz döngü önleme
                        Log.Warning(ex, "Failed to show error toast: {OriginalMessage}", message);
                    }
                }));
            }
            catch (Exception ex)
            {
                // Dispatcher erişimi bile başarısız olursa (uygulama çöküyorsa)
                Log.Warning(ex, "ShowErrorToast completely failed: {Message}", message);
            }
        }

        /// <summary>
        /// Saves JWT token after successful login
        /// </summary>
        public static async Task SaveTokenAsync(string token)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Clears JWT token on logout
        /// </summary>
        public static async Task ClearTokenAsync()
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Ana pencere açıldıktan sonra UI mesaj döngüsünü bloklamadan 5s gecikmeli güncelleme taraması yapar.
        /// </summary>
        private async Task CheckStartupUpdatesAsync(System.Threading.CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(5000, cancellationToken);

                var updateService = _host?.Services.GetService<Services.Update.IUpdateService>();
                if (updateService != null && updateService.GetSettings().CheckForUpdatesOnStartup)
                {
                    var update = await updateService.CheckForUpdatesAsync(isAutoCheck: true, ct: cancellationToken);
                    if (update != null && !cancellationToken.IsCancellationRequested)
                    {
                        var updateDialog = new Views.UpdateNotificationWindow(updateService)
                        {
                            Owner = MainWindow
                        };
                        updateDialog.ShowDialog();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Information("Startup update check cancelled.");
            }
            catch (Exception updateEx)
            {
                Log.Warning(updateEx, "Background startup update check failed silently.");
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                Log.Information("Application shutting down...");

                // Aktif arka plan güncelleme taramasını iptal et
                try
                {
                    _appCts.Cancel();
                    _appCts.Dispose();
                }
                catch
                {
                    // CancellationTokenSource temizleme hatası yutulur
                }
                
                // Uygulama kapanırken otomatik yedek al (DI üzerinden)
                if (_host != null)
                {
                    try
                    {
                        var updateService = _host.Services.GetService<Services.Update.IUpdateService>();
                        if (updateService != null && updateService.IsUpdateDownloaded && updateService.GetSettings().InstallOnClose)
                        {
                            Log.Information("Applying downloaded update on application exit...");
                            updateService.ApplyUpdateAndRestart();
                        }
                    }
                    catch (Exception updateEx)
                    {
                        Log.Warning(updateEx, "Failed to apply update on application exit");
                    }

                    try
                    {
                        using var backupScope = _host.Services.CreateScope();
                        var backupService = backupScope.ServiceProvider.GetRequiredService<IBackupService>();
                        backupService.BackupDatabase();
                    }
                    catch (Exception backupEx)
                    {
                        Log.Warning(backupEx, "Yedekleme sırasında hata oluştu (kritik değil)");
                    }
                    
                    await _host.StopAsync();
                    _host.Dispose();
                }

                Log.Information("=== KamatekCRM Desktop Stopped ===");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exit cleanup failed");
                Debug.WriteLine($"Exit cleanup failed: {ex.Message}");
            }
            finally
            {
                Log.CloseAndFlush();
                base.OnExit(e);
            }
        }
    }
}

