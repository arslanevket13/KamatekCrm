using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Repositories;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Repositories;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IDatabaseConnectionProvider, DatabaseConnectionProvider>();

            services.AddDbContextFactory<AppDbContext>((sp, options) =>
            {
                var connectionProvider = sp.GetRequiredService<IDatabaseConnectionProvider>();
                string connString;
                try
                {
                    connString = connectionProvider.GetConnectionString();
                }
                catch
                {
                    connString = configuration.GetConnectionString("PostgreSQL") 
                        ?? "Host=127.0.0.1;Database=kamatekcrm;Username=postgres;Password=1313;Port=5432;";
                }

                options.UseNpgsql(connString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                })
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

            return services;
        }
    }
}
