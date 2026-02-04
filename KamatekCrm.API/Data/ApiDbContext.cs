using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.API.Data
{
    public class ApiDbContext : DbContext
    {
        public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options) { }

        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<ServiceJob> ServiceJobs { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<ServiceJobHistory> ServiceJobHistories { get; set; }
        // Stubs added
        public DbSet<ServiceProject> ServiceProjects { get; set; }
        public DbSet<CustomerAsset> CustomerAssets { get; set; }
        public DbSet<ServiceJobItem> ServiceJobItems { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ServiceJob>()
                .Property(p => p.Price)
                .HasColumnType("decimal(18,2)");
                
            modelBuilder.Entity<ServiceJob>()
                .Property(p => p.LaborCost)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<ServiceJob>()
                .Property(p => p.DiscountAmount)
                .HasColumnType("decimal(18,2)");

           // Add other configurations if needed, e.g. keys
        }
    }
}
