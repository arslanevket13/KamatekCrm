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
            services.AddSingleton<KamatekCrm.ApplicationCore.ErrorHandling.IExceptionClassifier, ErrorHandling.ExceptionMapper>();

            services.AddDbContextFactory<AppDbContext>((sp, options) =>
            {
                var connectionProvider = sp.GetRequiredService<IDatabaseConnectionProvider>();
                var connString = connectionProvider.GetConnectionString();

                options.UseNpgsql(connString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                });
            });

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

            return services;
        }
    }
}
