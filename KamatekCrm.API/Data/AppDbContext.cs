using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Data
{
    /// <summary>
    /// API için DbContext - Ana projedeki modelleri kullanır
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Ana Entity'ler
        public DbSet<ServiceJob> ServiceJobs { get; set; }
        public DbSet<ServiceJobHistory> ServiceJobHistories { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerAsset> CustomerAssets { get; set; }
        public DbSet<ServiceProject> ServiceProjects { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<TaskPhoto> TaskPhotos { get; set; }
        public DbSet<Category> Categories { get; set; }

        // ERP Entity'ler
        public DbSet<Product> Products { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<StockTransaction> StockTransactions { get; set; }
        public DbSet<ServiceJobItem> ServiceJobItems { get; set; }
        public DbSet<SalesOrder> SalesOrders { get; set; }
        public DbSet<SalesOrderItem> SalesOrderItems { get; set; }
        public DbSet<CashTransaction> CashTransactions { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
        public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }

        // GPS / Harita
        public DbSet<TechnicianLocation> TechnicianLocations { get; set; }
        public DbSet<RoutePoint> RoutePoints { get; set; }

        // CRM
        public DbSet<CustomerNote> CustomerNotes { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Global Query Filter for Soft Delete
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(KamatekCrm.Shared.Models.Common.ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                    var body = System.Linq.Expressions.Expression.Equal(
                        System.Linq.Expressions.Expression.Property(parameter, nameof(KamatekCrm.Shared.Models.Common.ISoftDeletable.IsDeleted)),
                        System.Linq.Expressions.Expression.Constant(false)
                    );
                    var lambda = System.Linq.Expressions.Expression.Lambda(body, parameter);
                    
                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
                }
            }

            // ServiceJob konfigürasyonu
            modelBuilder.Entity<ServiceJob>(entity =>
            {
                entity.ToTable("ServiceJobs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.CompletedDate).IsRequired(false);
                
                entity.HasOne(e => e.Customer)
                    .WithMany(c => c.ServiceJobs)
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.AssignedUser)
                    .WithMany()
                    .HasForeignKey(e => e.AssignedUserId)
                    .OnDelete(DeleteBehavior.SetNull);
                
                // Fix ServiceJob <=> ServiceJobItem Mapping
                entity.HasMany(e => e.ServiceJobItems)
                    .WithOne(e => e.ServiceJob)
                    .HasForeignKey(e => e.ServiceJobId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ServiceJobHistory konfigürasyonu
            modelBuilder.Entity<ServiceJobHistory>(entity =>
            {
                entity.ToTable("ServiceJobHistories");
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.ServiceJob)
                    .WithMany()
                    .HasForeignKey(e => e.ServiceJobId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Customer konfigürasyonu
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("Customers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            });

            // User konfigürasyonu
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.Username).IsUnique();
            });

            // TaskPhoto konfigürasyonu
            modelBuilder.Entity<TaskPhoto>(entity =>
            {
                entity.ToTable("TaskPhotos");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
                entity.Property(e => e.MimeType).HasMaxLength(100);
            });

            // Category konfigürasyonu
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("Categories");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            });
        }
    }
}

