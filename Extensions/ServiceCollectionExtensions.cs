using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using KamatekCrm.Services;
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
                // Retrieve connection strings from configuration or AppSettings class
                // For now, using logic similar to current AppDbContext
                // Ideally, configuration should be used.
                 options.UseSqlite("Data Source=kamatek.db");
            }, ServiceLifetime.Scoped); // Scoped is default for DbContext

             // Services
            services.AddScoped<NavigationService>(); // Singleton as it holds state
            services.AddSingleton<IToastService, ToastService>();
            services.AddSingleton<ToastViewModel>();
            
            services.AddSingleton<ILoadingService, LoadingService>();
            services.AddSingleton<LoadingViewModel>();
            
            // Registering AuthService. Since it was static, we need to handle it. 
            // We will register IAuthService implementation.
            services.AddScoped<IAuthService, AuthService>();
            services.AddTransient<AttachmentService>();

            // Repositories - Commented out until UnitOfWork is implemented
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Domain Services
            services.AddScoped<IInventoryDomainService, InventoryDomainService>();
            services.AddScoped<ISalesDomainService, SalesDomainService>();

            // MediatR Registration
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(KamatekCrm.App).Assembly));

            // Views (Register as needed, usually via ViewModel)
            
            // MainWindow'u DI container'a ekle (Scoped)
            services.AddScoped<MainWindow>();

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

            return services;
        }
    }
}
