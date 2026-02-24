using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using KamatekCrm.Services;
using KamatekCrm.Settings;
using KamatekCrm.ViewModels;
using KamatekCrm.Data;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Repositories;
using KamatekCrm.Services.Domain;

namespace KamatekCrm.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configuration Setup
            // Assuming configuration is loaded from appsettings.json in App.xaml.cs and passed here

            // DbContext
            services.AddDbContext<AppDbContext>(options =>
            {
                 options.UseNpgsql(AppSettings.PostgreSqlConnectionString)
                        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            }, ServiceLifetime.Scoped);

             // Services
            services.AddSingleton<NavigationService>(); // Singleton as it holds state
            services.AddSingleton<IToastService, ToastService>();
            services.AddSingleton<ToastViewModel>();
            
            services.AddSingleton<ILoadingService, LoadingService>();
            services.AddSingleton<LoadingViewModel>();
            
            // Registering AuthService. Since it was static, we need to handle it. 
            // We will register IAuthService implementation.
            services.AddScoped<IAuthService, AuthService>();
            services.AddTransient<AttachmentService>();
            services.AddScoped<ProjectScopeService>();
            
            // Repositories
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Domain Services
            services.AddScoped<IInventoryDomainService, InventoryDomainService>();
            services.AddScoped<ISalesDomainService, SalesDomainService>();
            services.AddSingleton<IProductImageService, ProductImageService>();
            services.AddScoped<IPurchasingDomainService, PurchasingDomainService>();

            // Background Services
            services.AddScoped<ISlaService, SlaService>();
            services.AddScoped<IBackupService, BackupService>();

            // Views (Register as needed, usually via ViewModel)
            

            // MainWindow is registered in App.xaml.cs as Singleton

            // MainWindow is registered in App.xaml.cs as Singleton

            // ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<CustomersViewModel>();
            services.AddTransient<CustomerDetailViewModel>();
            services.AddTransient<ProductViewModel>();
            services.AddTransient<AddProductViewModel>();
            services.AddTransient<ServiceJobViewModel>();
            services.AddTransient<FaultTicketViewModel>();
            services.AddTransient<RepairListViewModel>();
            services.AddTransient<FieldJobListViewModel>();
            services.AddTransient<StockCountViewModel>();
            services.AddTransient<StockReportsViewModel>();
            services.AddTransient<UsersViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SystemLogsViewModel>();
            services.AddTransient<DirectSalesViewModel>();
            services.AddTransient<FinanceViewModel>();
            services.AddTransient<RepairViewModel>();
            services.AddTransient<MainContentViewModel>();
            services.AddTransient<SuppliersViewModel>();

            // v8.3 — Missing ViewModel Registrations (Navigation Crash Fix)
            services.AddTransient<AnalyticsViewModel>();
            services.AddTransient<RoutePlanningViewModel>();
            services.AddTransient<FinancialHealthViewModel>();
            services.AddTransient<PurchaseOrderViewModel>();
            services.AddTransient<StockTransferViewModel>();
            services.AddTransient<AddUserViewModel>();

            // v8.4 — Additional Missing ViewModel Registrations (Complete DI Coverage)
            services.AddTransient<ProjectQuoteEditorViewModel>();
            services.AddTransient<ProjectQuoteViewModel>();
            services.AddTransient<EditUserViewModel>();
            services.AddTransient<PasswordResetViewModel>();
            services.AddTransient<PdfImportPreviewViewModel>();
            services.AddTransient<QuickAssetAddViewModel>();
            services.AddTransient<GlobalSearchViewModel>();
            services.AddTransient<RepairListViewModel>();
            services.AddTransient<FieldJobListViewModel>();
            
            // Window'ların da DI container'da kayıtlı olması gerekir
            services.AddTransient<Views.RepairRegistrationWindow>();
            services.AddTransient<Views.RepairTrackingWindow>();
            services.AddTransient<Views.FaultTicketWindow>();
            services.AddTransient<Views.DirectSalesWindow>();
            services.AddTransient<Views.ProjectQuoteEditorWindow>();

            return services;
        }
    }
}
