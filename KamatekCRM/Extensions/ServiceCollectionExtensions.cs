using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using KamatekCrm.Infrastructure;
using KamatekCrm.Services;
using KamatekCrm.Settings;
using KamatekCrm.ViewModels;
using KamatekCrm.Services.Domain;

namespace KamatekCrm.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Infrastructure Layer Services (DbContext, UnitOfWork, Repositories, DatabaseConnectionProvider)
            services.AddInfrastructureServices(configuration);

            // Application Layer Services (Use Cases, DTOs, Application Services)
            KamatekCrm.ApplicationCore.DependencyInjection.AddApplicationLayerServices(services);

            // Core Application Services
            services.AddMemoryCache();
            services.AddSingleton<NavigationService>();
            services.AddSingleton<IToastService, ToastService>();
            services.AddSingleton<ToastViewModel>();
            
            services.AddSingleton<ILoadingService, LoadingService>();
            services.AddSingleton<LoadingViewModel>();
            
            services.AddSingleton<IAuthService, AuthService>();
            services.AddTransient<AttachmentService>();
            services.AddScoped<ProjectScopeService>();
            
            services.AddTransient<EmailService>();
            services.AddSingleton<EventAggregator>();
            services.AddTransient<InvoiceScannerService>();
            services.AddTransient<NotificationService>();
            services.AddTransient<PdfInvoiceParserService>();
            services.AddTransient<PdfService>();
            services.AddTransient<ReportService>();
            services.AddTransient<SmsService>();
            services.AddTransient<StructureGeneratorService>();

            services.AddTransient<AddressService>();
            services.AddTransient<ISearchService, SearchService>();

            // Domain Services
            services.AddScoped<IInventoryDomainService, InventoryDomainService>();
            services.AddSingleton<IProductImageService, ProductImageService>();
            services.AddScoped<IPurchasingDomainService, PurchasingDomainService>();

            // Background Services
            services.AddScoped<ISlaService, SlaService>();
            services.AddScoped<IBackupService, BackupService>();

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

            services.AddTransient<AnalyticsViewModel>();
            services.AddTransient<RoutePlanningViewModel>();
            services.AddTransient<FinancialHealthViewModel>();
            services.AddTransient<PurchasingViewModel>();
            services.AddTransient<StockTransferViewModel>();
            services.AddTransient<AddUserViewModel>();

            services.AddTransient<ProjectQuoteEditorViewModel>();
            services.AddTransient<ProjectQuoteViewModel>();
            services.AddTransient<QuoteListViewModel>();
            services.AddTransient<EditUserViewModel>();
            services.AddTransient<PasswordResetViewModel>();
            services.AddTransient<PdfImportPreviewViewModel>();
            services.AddTransient<QuickAssetAddViewModel>();
            services.AddTransient<GlobalSearchViewModel>();
            
            services.AddTransient<CustomerAddViewModel>();
            services.AddTransient<QuickCustomerAddViewModel>();
            services.AddTransient<QuickNewProductForPurchaseViewModel>();
            
            services.AddTransient<PurchaseOrderViewModel>();
            services.AddTransient<QuotationViewModel>();
            services.AddTransient<NetworkSettingsViewModel>();

            // Windows
            services.AddTransient<Views.RepairTrackingWindow>();
            services.AddTransient<Views.FaultTicketWindow>();
            services.AddTransient<Views.DirectSalesWindow>();
            services.AddTransient<Views.ProjectQuoteEditorWindow>();
            services.AddTransient<Views.NetworkSettingsView>();

            return services;
        }
    }
}
