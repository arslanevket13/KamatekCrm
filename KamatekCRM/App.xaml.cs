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
using KamatekCrm.Data;

namespace KamatekCrm
{
    /// <summary>
    /// WPF Desktop Application Launcher — Dumb Client
    /// Hiçbir web server, JWT, veya EF Migration barındırmaz.
    /// Tüm iş mantığı KamatekCrm.API üzerinden HttpClient ile erişilir.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private IHost? _host;

        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        public static KamatekCrm.Shared.Models.User? CurrentUser { get; set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // PostgreSQL Legacy Timestamp Behavior (Fix for Kind=Local error)
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            // Logging'i ilk iş olarak yapılandır
            LoggingConfiguration.ConfigureLogging();
            
            try
            {
                Log.Information("=== KamatekCRM Desktop Starting (Dumb Client Mode) ===");
                
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

                        // 1. Thread-safe Provider'ı Singleton olarak ekliyoruz
                        services.AddSingleton<IDatabaseConnectionProvider, DatabaseConnectionProvider>();

                        // 2. DbContextFactory'yi dinamik Connection String alacak şekilde yapılandırıyoruz
                        services.AddDbContextFactory<KamatekCrm.Data.AppDbContext>((sp, options) =>
                        {
                            var connectionProvider = sp.GetRequiredService<IDatabaseConnectionProvider>();
                            try
                            {
                                var connString = connectionProvider.GetConnectionString();
                                options.UseNpgsql(connString);
                            }
                            catch (InvalidOperationException)
                            {
                                // Henüz ağ keşfi yapılmadıysa Design-Time veya başlangıç için dummy string
                                options.UseNpgsql("Host=0.0.0.0;Database=dummy;Username=postgres;Password=123456"); 
                            }
                        });

                        // 3. Arka plan servislerini ekliyoruz (Web server YOK, IHostedService olarak çalışacaklar)
                        // Singleton olarak kaydet ki ViewModeller (LoginViewModel) inject edebilsin.
                        services.AddSingleton<NetworkDiscoveryService>();
                        services.AddHostedService(provider => provider.GetRequiredService<NetworkDiscoveryService>());

                        services.AddSingleton<ConnectionHeartbeatService>();
                        services.AddHostedService(provider => provider.GetRequiredService<ConnectionHeartbeatService>());
                    })
                    .Build();

                // Service Provider'ı global erişime aç
                ServiceProvider = _host.Services;
                
                // Token storage removed

                // Global WPF exception handler'ları kur
                DispatcherUnhandledException += OnDispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                // Host'u başlat (web server YOK, sadece DI lifecycle). IHostedService'ler otomatik başlar.
                await _host.StartAsync();


                // MainWindow'u DI'dan al ve göster
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                MainWindow = mainWindow;
                
                // Login ekranını aktif et
                var navigationService = _host.Services.GetRequiredService<NavigationService>();
                navigationService.NavigateToLogin();

                mainWindow.Show();
                
                Log.Information("Desktop application started successfully.");
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

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                Log.Information("Application shutting down...");
                
                // Uygulama kapanırken otomatik yedek al (DI üzerinden)
                if (_host != null)
                {
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

