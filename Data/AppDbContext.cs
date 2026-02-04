using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Settings;

namespace KamatekCrm.Data
{
    /// <summary>
    /// Entity Framework DbContext - Veri tabanı bağlantısı ve yapılandırması
    /// Hibrit mimari: SQLite (geliştirme) ve SQL Server (production) desteği
    /// </summary>
    public class AppDbContext : DbContext
    {

        public AppDbContext()
        {
            // EnsureCreated migration ile çakışır, kaldırıldı
            // Database migration ile oluşturulur: dotnet ef database update
        }

        // --- Mevcut DbSetler ---
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ServiceJob> ServiceJobs { get; set; }
        public DbSet<MaintenanceContract> MaintenanceContracts { get; set; }
        public DbSet<ServiceJobItem> ServiceJobItems { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        // --- Yeni Envanter Modülü DbSetleri ---
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<StockTransaction> StockTransactions { get; set; }
        public DbSet<ProductSerial> ProductSerials { get; set; }

        // --- Kullanıcı Yönetimi ---
        public DbSet<User> Users { get; set; }

        // --- Audit Logging ---
        public DbSet<ActivityLog> ActivityLogs { get; set; }

        // --- Service Command Center ---
        public DbSet<ServiceProject> ServiceProjects { get; set; }
        public DbSet<CustomerAsset> CustomerAssets { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
        public DbSet<ServiceJobHistory> ServiceJobHistories { get; set; }

        // --- Perakende Satış (POS) ---
        public DbSet<SalesOrder> SalesOrders { get; set; }
        public DbSet<SalesOrderItem> SalesOrderItems { get; set; }

        // --- Kasa / Finans ---
        public DbSet<CashTransaction> CashTransactions { get; set; }

        // --- Dijital Arşiv ---
        public DbSet<Attachment> Attachments { get; set; }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Hibrit veritabanı desteği: appsettings.json'dan oku
                if (AppSettings.UseSqlServer)
                {
                    // SQL Server (Production / Multi-user)
                    optionsBuilder.UseSqlServer(AppSettings.SqlServerConnectionString);
                }
                else
                {
                    // SQLite (Geliştirme / Tek kullanıcı)
                    optionsBuilder.UseSqlite(AppSettings.SqliteConnectionString);
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- Inventory Modülü İlişkileri ---

            // Inventory - Composite Key (ProductId + WarehouseId)
            modelBuilder.Entity<Inventory>()
                .HasKey(i => new { i.ProductId, i.WarehouseId });

            modelBuilder.Entity<Inventory>()
                .HasOne(i => i.Product)
                .WithMany(p => p.Inventories)
                .HasForeignKey(i => i.ProductId);

            modelBuilder.Entity<Inventory>()
                .HasOne(i => i.Warehouse)
                .WithMany(w => w.Inventories)
                .HasForeignKey(i => i.WarehouseId);

            // Warehouse Yapılandırması
            modelBuilder.Entity<Warehouse>().HasData(
                new Warehouse { Id = 1, Name = "Merkez Depo", Type = WarehouseType.MainWarehouse, IsActive = true },
                new Warehouse { Id = 2, Name = "Servis Aracı 1", Type = WarehouseType.Vehicle, IsActive = true }
             );

            // Category - Tree Structure
            modelBuilder.Entity<Category>()
                .HasOne(c => c.ParentCategory)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);


            // StockTransaction Yapılandırması
            modelBuilder.Entity<StockTransaction>()
                .HasOne(t => t.SourceWarehouse)
                .WithMany()
                .HasForeignKey(t => t.SourceWarehouseId)
                .OnDelete(DeleteBehavior.Restrict); // Döngüsel silmeyi önlemek için

            modelBuilder.Entity<StockTransaction>()
                .HasOne(t => t.TargetWarehouse)
                .WithMany()
                .HasForeignKey(t => t.TargetWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransaction>()
                .HasOne(t => t.Product)
                .WithMany(p => p.Transactions)
                .HasForeignKey(t => t.ProductId);

            // ProductUnique SKU
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.SKU)
                .IsUnique();

            // ProductSerial Unique SerialNumber
            modelBuilder.Entity<ProductSerial>()
                .HasIndex(s => s.SerialNumber)
                .IsUnique();

            // --- Mevcut Konfigürasyonlar (Güncellendi) ---

            // Customer Yapılandırması
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Notes).HasMaxLength(2000);
                entity.Ignore(e => e.FullAddress);
            });

            // Product Yapılandırması
            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.PurchasePrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.SalePrice).HasColumnType("decimal(18,2)");
            });

            // Seed Data
            modelBuilder.Entity<Brand>().HasData(
                new Brand { Id = 1, Name = "Hikvision" },
                new Brand { Id = 2, Name = "Dahua" },
                new Brand { Id = 3, Name = "Next" }
            );

            // Mevcut kategori verileri varsa çakışabilir, ancak parentId null olacağı için sorun olmaz.
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Kamera" },
                new Category { Id = 2, Name = "Kablo" },
                new Category { Id = 3, Name = "Diafon" }
            );
        }
    }
}
