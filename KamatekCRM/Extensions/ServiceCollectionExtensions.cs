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

            // Core Application Services & UI Abstractions
            services.AddHttpClient();
            services.AddSingleton<KamatekCrm.Shared.Services.IDialogService, Services.WpfDialogService>();
            services.AddSingleton<KamatekCrm.Shared.Services.IUIService, Services.WpfUIService>();

            services.AddMemoryCache();
            services.AddSingleton<NavigationService>();
            services.AddSingleton<IToastService, ToastService>();
            services.AddSingleton<ToastViewModel>();
            
            services.AddSingleton<ILoadingService, LoadingService>();
            services.AddSingleton<LoadingViewModel>();
            
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<KamatekCrm.ApplicationCore.Interfaces.ICurrentUserContext, DesktopCurrentUserContext>();
            services.AddTransient<IForcedPasswordChangeService, ForcedPasswordChangeService>();
            services.AddTransient<AttachmentService>();
            services.AddTransient<EmailService>();
            services.AddSingleton<IProjectQuoteEditorLauncher, ProjectQuoteEditorLauncher>();
            services.AddSingleton<IQuotationLauncher, QuotationLauncher>();
            services.AddSingleton<EventAggregator>();
            services.AddTransient<InvoiceScannerService>();
            services.AddTransient<NotificationService>();
            services.AddTransient<PdfInvoiceParserService>();

            // PDF Engine Services
            services.AddTransient<PdfService>();
            services.AddTransient<KamatekCrm.Shared.Services.IQuotePdfService>(sp => sp.GetRequiredService<PdfService>());
            services.AddTransient<KamatekCrm.Shared.Services.IPurchaseOrderPdfService>(sp => sp.GetRequiredService<PdfService>());
            services.AddTransient<KamatekCrm.Shared.Services.IInvoicePdfService>(sp => sp.GetRequiredService<PdfService>());
            services.AddTransient<KamatekCrm.Shared.Services.IServiceReportPdfService>(sp => sp.GetRequiredService<PdfService>());
            services.AddTransient<KamatekCrm.Shared.Services.IDiscoveryPdfService>(sp => sp.GetRequiredService<PdfService>());
            services.AddTransient<KamatekCrm.Shared.Services.IQuotationPdfService>(sp => sp.GetRequiredService<PdfService>());
            services.AddTransient<KamatekCrm.Shared.Services.IInstallationPdfService>(sp => sp.GetRequiredService<PdfService>());

            services.AddTransient<ReportService>();
            services.AddTransient<SmsService>();
            services.AddTransient<StructureGeneratorService>();

            services.AddTransient<AddressService>();
            services.AddTransient<ISearchService, SearchService>();
            services.AddScoped<IDirectSalesService, DirectSalesService>();
            services.AddTransient<IThermalReceiptPrintService, ThermalReceiptPrintService>();

            // Domain Services
            services.AddScoped<IInventoryDomainService, InventoryDomainService>();
            services.AddSingleton<IProductImageService, ProductImageService>();

            // Background & System Services
            services.AddScoped<ISlaService, SlaService>();
            services.AddScoped<IBackupService, BackupService>();
            services.AddSingleton<IBackupIntegrityService, BackupIntegrityService>();
            services.AddSingleton<Services.Update.IUpdateService, Services.Update.VelopackUpdateService>();

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
            services.AddTransient<SalesReturnViewModel>();
            services.AddTransient<FinanceViewModel>();
            services.AddTransient<RepairViewModel>();
            services.AddTransient<MainContentViewModel>();
            services.AddTransient<SuppliersViewModel>();

            services.AddTransient<AnalyticsViewModel>();
            services.AddTransient<RoutePlanningViewModel>();
            services.AddTransient<FinancialHealthViewModel>();
            services.AddTransient<PurchasingViewModel>();
            services.AddTransient<PurchaseReturnViewModel>();
            services.AddTransient<StockTransferViewModel>();
            services.AddTransient<AddUserViewModel>();

            services.AddTransient<ProjectQuoteEditorViewModel>();
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
            services.AddTransient<QuickInteractionAddViewModel>();
            services.AddTransient<CustomerInteractionsViewModel>();
            services.AddTransient<ManagerAgendaViewModel>();

            // Windows
            services.AddTransient<Views.RepairTrackingWindow>();
            services.AddTransient<Views.FaultTicketWindow>();
            services.AddTransient<Views.DirectSalesWindow>();
            services.AddTransient<Views.ProjectQuoteEditorWindow>();
            services.AddTransient<Views.QuotationWindow>();
            services.AddTransient<Views.NetworkSettingsView>();
            services.AddTransient<Views.AddUserView>();
            services.AddTransient<Views.EditUserView>();
            services.AddTransient<Views.PasswordResetView>();
            services.AddTransient<Views.QuickInteractionAddWindow>();
            services.AddTransient<Views.CustomerInteractionsView>();
            services.AddTransient<Views.ManagerAgendaView>();

            return services;
        }
    }
}
