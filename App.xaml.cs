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
            // PostgreSQL Legacy Timestamp Behavior (Fix for Kind=Local error)
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

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
                        services.AddTransient<MainWindow>();

                        // JWT Authentication Configuration
                        var jwtKey = context.Configuration["Jwt:Key"];
                        var jwtIssuer = context.Configuration["Jwt:Issuer"] ?? "KamatekCRM";
                        var jwtAudience = context.Configuration["Jwt:Audience"] ?? "KamatekCRM-Users";
                        
                        // JWT Key validasyonu
                        if (string.IsNullOrWhiteSpace(jwtKey))
                        {
                            throw new InvalidOperationException("JWT Key is not configured. Please set 'Jwt:Key' in appsettings.json with at least 32 characters.");
                        }
                        
                        if (jwtKey.Length < 32)
                        {
                            throw new InvalidOperationException($"JWT Key must be at least 32 characters long. Current length: {jwtKey.Length}");
                        }
                        
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
                                ValidIssuer = jwtIssuer,
                                ValidAudience = jwtAudience,
                                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                                    System.Text.Encoding.UTF8.GetBytes(jwtKey))
                            };
                        });
                    })
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseUrls(KamatekCrm.Helpers.ProcessManager.API_BIND_URL);
                        webBuilder.Configure((context, app) =>
                        {
                            // Swagger sadece Development ortamında
                            if (context.HostingEnvironment.IsDevelopment())
                            {
                                app.UseSwagger();
                                app.UseSwaggerUI();
                            }
                            
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
                        // Detaylı hata bilgisi oluştur
                        var errorDetails = GetExceptionDetails(ex);
                        Log.Error(ex, "Veritabanı güncellenirken hata oluştu: {ErrorDetails}", errorDetails);
                        MessageBox.Show($"Veritabanı güncellenirken hata oluştu:\n\n{errorDetails}", "Veritabanı Hatası", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    
                    // 5. Varsayılan admin kullanıcısı oluştur
                    var authService = services.GetRequiredService<IAuthService>();
                    authService.CreateDefaultUser(); 

                    // 6. SLA Otomasyon Servisini Başlat (DI üzerinden)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var slaScope = _host.Services.CreateScope();
                            var slaService = slaScope.ServiceProvider.GetRequiredService<ISlaService>();
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
                
                // Sunucu süreçlerini kapat
                ProcessManager.StopServices();
                
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
