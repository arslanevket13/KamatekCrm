using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Repositories;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Repositories;
using KamatekCrm.Shared.Services;
using KamatekCrm.ApplicationCore.Interfaces;

namespace KamatekCrm.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IDatabaseConnectionProvider, DatabaseConnectionProvider>();
            services.AddSingleton<IDatabaseInitializationService, DatabaseInitializationService>();
            services.AddTransient<IServiceJobCommandService, ServiceJobCommandService>();
            services.AddTransient<IServiceJobReadService, ServiceJobReadService>();
            services.AddTransient<IWorkOrderIntelligenceService, WorkOrderIntelligenceService>();
            services.AddTransient<IRetailTransactionService, RetailTransactionService>();
            services.AddTransient<IPurchasingCommandService, PurchasingCommandService>();
            services.AddTransient<IAuditTrailService, AuditTrailService>();
            services.AddTransient<ITransactionReadService, TransactionReadService>();
            services.AddTransient<IStockCountCommandService, StockCountCommandService>();
            services.AddTransient<IStockCountReadService, StockCountReadService>();
            services.AddTransient<IProjectQuoteCommandService, ProjectQuoteCommandService>();
            services.AddTransient<IProjectQuoteReadService, ProjectQuoteReadService>();
            services.AddTransient<IStandardQuoteCommandService, StandardQuoteCommandService>();
            services.AddTransient<IStandardQuoteReadService, StandardQuoteReadService>();
            services.AddTransient<ICustomerInteractionCommandService, CustomerInteractionCommandService>();
            services.AddTransient<ICustomerInteractionReadService, CustomerInteractionReadService>();
            services.AddTransient<ICustomerReadService, CustomerReadService>();
            services.AddSingleton<KamatekCrm.ApplicationCore.ErrorHandling.IExceptionClassifier, ErrorHandling.ExceptionMapper>();

            services.AddSingleton<IDbContextFactory<AppDbContext>, DynamicAppDbContextFactory>();

            services.AddTransient<IUnitOfWork, UnitOfWork>();
            services.AddTransient(typeof(IRepository<>), typeof(Repository<>));

            return services;
        }

        /// <summary>
        /// Her CreateDbContext çağrısında aktif IDatabaseConnectionProvider üzerinden canlı bağlantı dizesini okur.
        /// Böylece İlk Kurulum Sihirbazı veya Ağ Ayarları bağlantı dizesini güncellediğinde
        /// Singleton servisler dahi anında yeni veritabanına bağlanır.
        /// </summary>
        private sealed class DynamicAppDbContextFactory : IDbContextFactory<AppDbContext>
        {
            private readonly IDatabaseConnectionProvider _connectionProvider;

            public DynamicAppDbContextFactory(IDatabaseConnectionProvider connectionProvider)
            {
                _connectionProvider = connectionProvider;
            }

            public AppDbContext CreateDbContext()
            {
                var connString = _connectionProvider.GetConnectionString();
                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                optionsBuilder.UseNpgsql(connString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                });
                optionsBuilder.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

                return new AppDbContext(optionsBuilder.Options);
            }
        }
    }
}
