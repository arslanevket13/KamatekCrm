using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.API.Data
{
    public class ApiDbContext : DbContext
    {
        public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ServiceJob> ServiceJobs { get; set; }
        // Add other DbSets as needed for the shared models
    }
}
