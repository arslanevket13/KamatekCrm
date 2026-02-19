using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace KamatekCrm.Data
{
    // Design-time factory for EF Core migrations
    // Allows running 'dotnet ef' commands without needing to run the full application
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            // Build configuration from appsettings.json
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            // Default connection string if not found in config
            var connectionString = configuration.GetConnectionString("PostgreSQL") 
                ?? "Host=localhost;Port=5432;Database=kamatekcrm;Username=postgres;Password=postgres";
            
            optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            {
                // Ensure migrations are stored in the main project (KamatekCrm)
                npgsqlOptions.MigrationsAssembly("KamatekCrm");
            })
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
