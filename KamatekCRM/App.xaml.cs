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
        private ITokenStorageService? _tokenStorage;

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

                // Host Builder'ı yapılandır — SADECE WPF DI, web server YOK
                _host = Host.CreateDefaultBuilder()
                    .UseSerilog()
                    .ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    })
                    .ConfigureServices((context, services) =>
                    {
                        // Token Storage Service - JWT token'ları için
                        services.AddSingleton<ITokenStorageService, FileTokenStorageService>();
                        
                        // AuthHeaderHandler - Her HTTP isteğine otomatik token ekler
                        services.AddTransient<AuthHeaderHandler>();
                        
                        // WPF servisleri kaydet (DB, ViewModels, Navigation vs.)
                        services.AddApplicationServices(context.Configuration);
                        
                        // MainWindow'u DI container'a ekle
                        services.AddTransient<MainWindow>();

                        // HttpClient — API iletişimi için (http://localhost:5050)
                        // IHttpClientFactory ile socket exhaustion önlenir
                        // AuthHeaderHandler ile JWT token otomatik eklenir
                        services.AddHttpClient("KamatekAPI", client =>
                        {
                            var apiUrl = context.Configuration["Api:BaseUrl"] ?? "http://localhost:5050";
                            client.BaseAddress = new Uri(apiUrl);
                            client.Timeout = TimeSpan.FromSeconds(30);
                            client.DefaultRequestHeaders.Add("Accept", "application/json");
                        })
                        .AddHttpMessageHandler<AuthHeaderHandler>();
                    })
                    .Build();

                // Service Provider'ı global erişime aç
                ServiceProvider = _host.Services;
                
                // Token storage referansını al (login sonrası kullanım için)
                _tokenStorage = ServiceProvider.GetRequiredService<ITokenStorageService>();

                // Global WPF exception handler'ları kur
                DispatcherUnhandledException += OnDispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                // Host'u başlat (web server YOK, sadece DI lifecycle)
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
        /// Handles unhandled UI thread exceptions gracefully
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs args)
        {
            var exception = args.Exception;
            
            // Check if it's a network-related exception
            if (exception is HttpRequestException httpEx)
            {
                Log.Error(httpEx, "HTTP Request Exception - Server unreachable");
                
                // Show user-friendly toast/notification
                ShowErrorToast("Sunucuya bağlanılamıyor. İnternet bağlantınızı kontrol edin.");
                
                args.Handled = true; // Prevent app crash
                return;
            }
            
            // Check for TaskCanceledException (often caused by timeout)
            if (exception is TaskCanceledException taskEx)
            {
                Log.Warning(taskEx, "Task cancelled/timeout");
                ShowErrorToast("İşlem zaman aşımına uğradı. Lütfen tekrar deneyin.");
                
                args.Handled = true;
                return;
            }
            
            // Log other exceptions
            Log.Error(exception, "Unhandled UI exception");
            
            // Show error but don't crash for non-critical errors
            MessageBox.Show(
                $"Bir hata oluştu:\n\n{exception.Message}",
                "Hata",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            
            args.Handled = true;
        }

        /// <summary>
        /// Handles unhandled non-UI exceptions
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            var exception = args.ExceptionObject as Exception;
            Log.Fatal(exception, "Unhandled domain exception - IsTerminating: {IsTerminating}", args.IsTerminating);
            
            if (args.IsTerminating)
            {
                MessageBox.Show(
                    $"Kritik bir hata oluştu ve uygulama kapanıyor:\n\n{exception?.Message}",
                    "Kritik Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles unobserved task exceptions
        /// </summary>
        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved(); // Prevent app crash
        }

        /// <summary>
        /// Shows error toast notification (calls ToastService if available)
        /// </summary>
        private void ShowErrorToast(string message)
        {
            try
            {
                var toastService = ServiceProvider?.GetService(typeof(ToastService)) as ToastService;
                toastService?.ShowError(message);
            }
            catch
            {
                // Fallback to MessageBox if ToastService unavailable
            }
        }

        /// <summary>
        /// Saves JWT token after successful login
        /// </summary>
        public static async Task SaveTokenAsync(string token)
        {
            try
            {
                var tokenStorage = ServiceProvider?.GetService(typeof(ITokenStorageService)) as ITokenStorageService;
                if (tokenStorage != null)
                {
                    await tokenStorage.SaveTokenAsync(token);
                    Log.Information("JWT token saved successfully");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save JWT token");
            }
        }

        /// <summary>
        /// Clears JWT token on logout
        /// </summary>
        public static async Task ClearTokenAsync()
        {
            try
            {
                var tokenStorage = ServiceProvider?.GetService(typeof(ITokenStorageService)) as ITokenStorageService;
                if (tokenStorage != null)
                {
                    await tokenStorage.ClearTokenAsync();
                    Log.Information("JWT token cleared on logout");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to clear JWT token");
            }
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
