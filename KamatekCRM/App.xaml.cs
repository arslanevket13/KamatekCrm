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
                        // WPF servisleri kaydet (DB, ViewModels, Navigation vs.)
                        services.AddApplicationServices(context.Configuration);
                        
                        // MainWindow'u DI container'a ekle
                        services.AddTransient<MainWindow>();

                        // HttpClient — API iletişimi için (http://localhost:5050)
                        services.AddHttpClient("KamatekAPI", client =>
                        {
                            var apiUrl = context.Configuration["Api:BaseUrl"] ?? "http://localhost:5050";
                            client.BaseAddress = new Uri(apiUrl);
                            client.Timeout = TimeSpan.FromSeconds(30);
                            client.DefaultRequestHeaders.Add("Accept", "application/json");
                        });
                    })
                    .Build();

                // Service Provider'ı global erişime aç
                ServiceProvider = _host.Services;

                // Global WPF exception handler'ları kur
                DispatcherUnhandledException += (_, args) =>
                {
                    Log.Error(args.Exception, "Beklenmeyen UI thread hatası");
                    args.Handled = true;
                };
                AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                {
                    Log.Fatal(args.ExceptionObject as Exception, "Kritik uygulama hatası");
                };

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

        /// <summary>
        /// Exception ve tüm inner exception'ların detaylarını toplar
        /// </summary>
        private string GetExceptionDetails(Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            var currentEx = ex;
            int level = 0;

            while (currentEx != null)
            {
                if (level > 0)
                    sb.AppendLine($"\n--- Inner Exception (Level {level}) ---");
                
                sb.AppendLine($"Message: {currentEx.Message}");
                sb.AppendLine($"Type: {currentEx.GetType().FullName}");
                
                if (!string.IsNullOrEmpty(currentEx.StackTrace))
                {
                    sb.AppendLine($"\nStackTrace:\n{currentEx.StackTrace}");
                }

                currentEx = currentEx.InnerException;
                level++;
            }

            return sb.ToString();
        }
    }
}
