using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using KamatekCrm.Services;
using KamatekCrm.Extensions;
using KamatekCrm.Helpers;
using KamatekCrm.ViewModels;
using Microsoft.Extensions.Configuration;
using KamatekCrm.Data;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Configuration;
using Serilog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer; // Added

namespace KamatekCrm
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// WPF Launcher - DI Container ve Startup yönetimi
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private IHost? _host;

        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        public static KamatekCrm.Shared.Models.User? CurrentUser { get; set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Logging'i ilk iş olarak yapılandır
            LoggingConfiguration.ConfigureLogging();
            
            try
            {
                Log.Information("=== KamatekCRM Starting ===");
                
                base.OnStartup(e);

                // Host Builder'ı yapılandır
                _host = Host.CreateDefaultBuilder()
                    .UseSerilog() // Serilog'u DI'a ekle
                    .ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    })
                    .ConfigureServices((context, services) =>
                    {
                        // Tüm servisleri kaydet
                        services.AddApplicationServices(context.Configuration);
                        
                        // MainWindow'u DI container'a ekle (Scoped veya Transient)
                        services.AddSingleton<MainWindow>();

                        // JWT Authentication Configuration
                        var jwtKey = context.Configuration["Jwt:Key"] ?? "KamatekCrm_SecretKey_MinimumLength_32Chars";
                        services.AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
                            options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
                        })
                        .AddJwtBearer(options =>
                        {
                            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidateAudience = true,
                                ValidateLifetime = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer = context.Configuration["Jwt:Issuer"] ?? "KamatekCrm",
                                ValidAudience = context.Configuration["Jwt:Audience"] ?? "KamatekCrm.TechnicianApp",
                                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                                    System.Text.Encoding.UTF8.GetBytes(jwtKey))
                            };
                        });
                    })
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseUrls(KamatekCrm.Helpers.ProcessManager.API_URL);
                        webBuilder.Configure(app =>
                        {
                            // if (context.HostingEnvironment.IsDevelopment()) // Hard to access context here easily without fuller config
                            // {
                                app.UseSwagger();
                                app.UseSwaggerUI();
                            // }
                            
                            app.UseStaticFiles(); // Enable serving static files (photos)
                            app.UseRouting();
                            app.UseAuthentication();
                            app.UseAuthorization();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapControllers();
                            });
                        });
                    })
                    .Build();

                // Service Provider'ı global erişime aç (gerekirse)
                ServiceProvider = _host.Services;

                // Global Exception Handler'ı başlat
                var logger = ServiceProvider.GetService<Microsoft.Extensions.Logging.ILogger<App>>();
                KamatekCrm.Infrastructure.GlobalExceptionHandler.Initialize(logger);
                
                // Host'u başlat
                await _host.StartAsync();

                // 3. API ve Web Sunucularını Başlat (ProcessManager)
                ProcessManager.StartServices();

                using (var scope = _host.Services.CreateScope())
                {
                    var services = scope.ServiceProvider;

                    // 4. Veritabanını başlat (Scope içinde)
                    try
                    {
                        var context = services.GetRequiredService<AppDbContext>();
                        context.Database.Migrate();
                        DbSeeder.SeedDemoData(context);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Veritabanı güncellenirken hata oluştu");
                        MessageBox.Show($"Veritabanı güncellenirken hata oluştu: {ex.Message}", "Veritabanı Hatası");
                    }
                    
                    // 5. Varsayılan admin kullanıcısı oluştur
                    var authService = services.GetRequiredService<IAuthService>();
                    authService.CreateDefaultUser(); 

                    // 6. SLA Otomasyon Servisini Başlat
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // SLA Service manual instantiation for now
                            var slaService = new SlaService(); 
                            await slaService.CheckAndGenerateJobsAsync();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "SLA Service Background Error");
                            Debug.WriteLine($"SLA Service Background Error: {ex.Message}");
                        }
                    });
                }

                // 7. MainWindow'u DI'dan al ve göster
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                MainWindow = mainWindow;
                
                // 8. Login ekranını aktif et
                var navigationService = _host.Services.GetRequiredService<NavigationService>();
                navigationService.NavigateToLogin();

                mainWindow.Show();
                
                Log.Information("Application started successfully");

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
                
                // Uygulama kapanırken otomatik yedek al
                var backupService = new BackupService();
                backupService.BackupDatabase();

                // Sunucu süreçlerini kapat
                ProcessManager.StopServices();
                
                if (_host != null)
                {
                    await _host.StopAsync();
                    _host.Dispose();
                }

                Log.Information("=== KamatekCRM Stopped ===");
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
