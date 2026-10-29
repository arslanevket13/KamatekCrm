using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Infrastructure;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Diagnostics.Core;
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

            // Non-Breaking DI Bridge: Henüz refactor edilmemiş eski ViewModel'ların kırılmaması için
            // AppDbContext doğrudan istendiğinde fabrikadan yeni bir örnek döndüren Transient köprü kaydı
            services.AddTransient<AppDbContext>(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

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
            services.AddTransient<IDirectSalesService, DirectSalesService>();
            services.AddTransient<IThermalReceiptPrintService, ThermalReceiptPrintService>();
            services.AddSingleton<IDraftStorageService, DraftStorageService>();

            // Domain Services
            services.AddTransient<IInventoryDomainService, InventoryDomainService>();
            services.AddSingleton<IProductImageService, ProductImageService>();

            // Diagnostics (Geliştirici Merkezi altyapısı)
            // ConfigureLogging() App.OnStartup başında çalıştığı için örnekler hazırdır.
            // Tanılama kapalıysa NullDiagnosticSink devreye girer; tüketiciler null kontrolü yapmaz.
            services.AddSingleton<Configuration.DiagnosticOptions>(_ => DiagnosticRuntime.Options);
            services.AddSingleton<IDiagnosticStore>(_ =>
                (IDiagnosticStore?)DiagnosticRuntime.Store ?? NullDiagnosticSink.Instance);
            services.AddSingleton<IDiagnosticSink>(sp =>
                (IDiagnosticSink)sp.GetRequiredService<IDiagnosticStore>());

            services.AddSingleton<Diagnostics.Authorization.DiagnosticAccessGuard>();
            services.AddSingleton<DiagnosticDataService>();

            // Background & System Services
            services.AddTransient<ISlaService, SlaService>();
            services.AddTransient<IBackupService, BackupService>();
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

            // Geliştirici Merkezi (Dev Center) — sekmeler her açılışta taze örnek
            services.AddTransient<ViewModels.DevCenter.OverviewTabViewModel>();
            services.AddTransient<ViewModels.DevCenter.LiveLogsTabViewModel>();
            services.AddTransient<ViewModels.DevCenter.ErrorCenterTabViewModel>();
            services.AddTransient<ViewModels.DevCenter.CrashReportsTabViewModel>();
            services.AddTransient<ViewModels.DevCenter.PerformanceTabViewModel>();
            services.AddTransient<ViewModels.DevCenter.BindingIssuesTabViewModel>();
            services.AddTransient<ViewModels.DevCenter.BuildAnalysisTabViewModel>();
            services.AddTransient<ViewModels.DevCenter.AutomatedTestsTabViewModel>();
            services.AddTransient<ViewModels.DevCenter.HealthTabViewModel>();
            services.AddTransient<ViewModels.DevCenter.DiagnosticPackageTabViewModel>();
            services.AddTransient<ViewModels.DevCenter.DevCenterViewModel>();

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
            services.AddTransient<Views.DevCenter.DevCenterWindow>();


            return services;
        }
    }
}
