using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Models.Common;
using KamatekCrm.Shared.Models.Specs;
using KamatekCrm.Shared.Models.JobDetails;
using KamatekCrm.Shared.Models.WorkOrders;

namespace KamatekCrm.Infrastructure.Data
{
    /// <summary>
    /// Entity Framework DbContext - Veri tabanı bağlantısı ve yapılandırması
    /// Hibrit mimari: SQLite (geliştirme) ve SQL Server/PostgreSQL (production) desteği
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
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
        public DbSet<StockCountSession> StockCountSessions { get; set; }
        public DbSet<StockCountSessionItem> StockCountSessionItems { get; set; }
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
        public DbSet<SalesOrderPayment> SalesOrderPayments { get; set; }
        public DbSet<SalesReturn> SalesReturns { get; set; }
        public DbSet<SalesReturnItem> SalesReturnItems { get; set; }
        public DbSet<SalesReturnPayment> SalesReturnPayments { get; set; }

        // --- Fiyat Teklifleri (Quotation) ---
        public DbSet<Quote> Quotes { get; set; }
        public DbSet<QuoteLine> QuoteLines { get; set; }

        // --- Kasa / Finans ---
        public DbSet<CashTransaction> CashTransactions { get; set; }
        public DbSet<PurchaseOrderPayment> PurchaseOrderPayments { get; set; }
        public DbSet<PurchaseReturn> PurchaseReturns { get; set; }
        public DbSet<PurchaseReturnItem> PurchaseReturnItems { get; set; }

        // --- Dijital Arşiv ---
        public DbSet<Attachment> Attachments { get; set; }

        // --- Teknisyen Web App ---
        public DbSet<TaskPhoto> TaskPhotos { get; set; }

        // --- ERP Major Update (POS & Purchasing) ---
        public DbSet<PosTransaction> PosTransactions { get; set; }
        public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }

        // --- Stok Görselleri ve Rezervasyon ---
        public DbSet<InventoryImage> InventoryImages { get; set; }
        public DbSet<StockReservation> StockReservations { get; set; }

        // --- Müşteri Aktiviteleri ve Görüşmeler ---
        public DbSet<CustomerActivity> CustomerActivities { get; set; }
        public DbSet<CustomerInteraction> CustomerInteractions { get; set; }
        public DbSet<CustomerInteractionHistory> CustomerInteractionHistories { get; set; }

        // --- Rota Planlama & Teknisyen Konum ---
        public DbSet<RoutePoint> RoutePoints { get; set; }
        public DbSet<TechnicianLocation> TechnicianLocations { get; set; }

        // --- İş Emri İş Akışı (Keşif → Teklif → Montaj) ---
        public DbSet<DiscoveryReport> DiscoveryReports { get; set; }
        public DbSet<DiscoveryMaterial> DiscoveryMaterials { get; set; }
        public DbSet<DiscoveryVisit> DiscoveryVisits { get; set; }
        public DbSet<WorkOrderQuotation> WorkOrderQuotations { get; set; }
        public DbSet<QuotationItem> QuotationItems { get; set; }
        public DbSet<InstallationOrder> InstallationOrders { get; set; }
        public DbSet<InstallationMaterial> InstallationMaterials { get; set; }
        public DbSet<InstallationTask> InstallationTasks { get; set; }
        public DbSet<JobDelivery> JobDeliveries { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            if (!optionsBuilder.IsConfigured)
            {
                throw new InvalidOperationException("DbContext must be configured with a valid connection string. Fallback configuration is strictly prohibited to prevent split-brain risks.");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Global Query Filter for Soft Delete
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var body = Expression.Equal(
                        Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)),
                        Expression.Constant(false)
                    );
                    var lambda = Expression.Lambda(body, parameter);
                    
                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
                }
            }

            modelBuilder.Entity<ActivityLog>(entity =>
            {
                entity.Property(log => log.IntegrityHash)
                    .HasMaxLength(64)
                    .HasDefaultValue("");
                entity.Property(log => log.IntegrityVersion)
                    .HasDefaultValue(0);
                entity.HasIndex(log => log.IntegrityHash);
            });

            modelBuilder.Entity<Warehouse>()
                .HasIndex(warehouse => warehouse.IsQuarantine)
                .IsUnique()
                .HasFilter("\"IsQuarantine\" = TRUE");

            // xmin yalnız PostgreSQL sistem sütunudur; SQLite transaction testlerinde
            // fiziksel NOT NULL sütununa dönüşmesine izin verilmez.
            if (Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            {
                foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                {
                    modelBuilder.Entity(entityType.ClrType)
                                .Property<uint>("xmin")
                                .HasColumnType("xid")
                                .ValueGeneratedOnAddOrUpdate()
                                .IsConcurrencyToken();
                }
            }

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

            modelBuilder.Entity<Warehouse>().HasData(
                new Warehouse { Id = 1, Name = "Merkez Depo", Type = WarehouseType.MainWarehouse, IsActive = true, CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Warehouse { Id = 2, Name = "Servis Aracı 1", Type = WarehouseType.Vehicle, IsActive = true, CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
             );

            modelBuilder.Entity<Category>()
                .HasOne(c => c.ParentCategory)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransaction>()
                .HasOne(t => t.SourceWarehouse)
                .WithMany()
                .HasForeignKey(t => t.SourceWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransaction>()
                .HasOne(t => t.TargetWarehouse)
                .WithMany()
                .HasForeignKey(t => t.TargetWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransaction>()
                .HasOne(t => t.Product)
                .WithMany(p => p.Transactions)
                .HasForeignKey(t => t.ProductId);

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.SKU)
                .IsUnique();

            modelBuilder.Entity<ProductSerial>()
                .HasIndex(s => s.SerialNumber)
                .IsUnique();

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Notes).HasMaxLength(2000);
                entity.Ignore(e => e.FullAddress);
            });

            modelBuilder.Entity<CustomerInteraction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.NormalizedPhone);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.AssignedToUserId);
                entity.HasIndex(e => e.FollowUpDate);
                entity.HasIndex(e => e.InteractionNumber).IsUnique();

                entity.HasOne(e => e.Customer)
                    .WithMany()
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.AssignedToUser)
                    .WithMany()
                    .HasForeignKey(e => e.AssignedToUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(e => e.Histories)
                    .WithOne(h => h.CustomerInteraction)
                    .HasForeignKey(h => h.CustomerInteractionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CustomerInteractionHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CustomerInteractionId);
            });

            // ── İş Emri İş Akışı (Keşif → Teklif → Montaj) ──
            modelBuilder.Entity<DiscoveryReport>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ServiceJobId).IsUnique();
                entity.Property(e => e.EstimatedLaborHours).HasColumnType("double precision");
                entity.HasMany(e => e.Materials)
                    .WithOne(e => e.DiscoveryReport)
                    .HasForeignKey(e => e.DiscoveryReportId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DiscoveryMaterial>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.DiscoveryReportId);
                entity.HasIndex(e => e.ProductId);
            });

            modelBuilder.Entity<DiscoveryVisit>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ServiceJobId);
                entity.HasOne(e => e.ServiceJob)
                    .WithMany()
                    .HasForeignKey(e => e.ServiceJobId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<JobDelivery>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ServiceJobId).IsUnique();
                entity.HasOne(e => e.ServiceJob)
                    .WithMany()
                    .HasForeignKey(e => e.ServiceJobId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<WorkOrderQuotation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ServiceJobId);
                entity.HasIndex(e => e.QuotationNumber).IsUnique();
                entity.Property(e => e.LaborCost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ShippingCost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxRate).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.HasMany(e => e.Items)
                    .WithOne(e => e.Quotation)
                    .HasForeignKey(e => e.QuotationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<QuotationItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.QuotationId);
                entity.HasIndex(e => e.ProductId);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountPercent).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TaxPercent).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<InstallationOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ServiceJobId).IsUnique();
                entity.HasIndex(e => e.QuotationId);
                entity.HasMany(e => e.Materials)
                    .WithOne(e => e.InstallationOrder)
                    .HasForeignKey(e => e.InstallationOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.Tasks)
                    .WithOne(e => e.InstallationOrder)
                    .HasForeignKey(e => e.InstallationOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<InstallationMaterial>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.InstallationOrderId);
                entity.HasIndex(e => e.ProductId);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<InstallationTask>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.InstallationOrderId);
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.PurchasePrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.SalePrice).HasColumnType("decimal(18,2)");
                
                entity.Property(e => e.Specifications)
                      .HasColumnType("jsonb")
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }),
                          v => JsonSerializer.Deserialize<ProductSpecBase>(v, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new GeneralSpecs()
                      );
            });

            modelBuilder.Entity<StockCountSession>(entity =>
            {
                entity.Property(item => item.IdempotencyKey).HasMaxLength(36);
                entity.Property(item => item.ReferenceNumber).HasMaxLength(50);
                entity.Property(item => item.FinancialDifference).HasColumnType("decimal(18,2)");
                entity.HasIndex(item => item.IdempotencyKey).IsUnique();
                entity.HasIndex(item => item.ReferenceNumber).IsUnique();
                entity.HasIndex(item => new { item.WarehouseId, item.CountedAt });
                entity.HasOne(item => item.Warehouse)
                    .WithMany()
                    .HasForeignKey(item => item.WarehouseId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(item => item.Items)
                    .WithOne(item => item.StockCountSession)
                    .HasForeignKey(item => item.StockCountSessionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<StockCountSessionItem>(entity =>
            {
                entity.Property(item => item.ProductCode).HasMaxLength(100);
                entity.Property(item => item.ProductName).HasMaxLength(200);
                entity.Property(item => item.UnitCost).HasColumnType("decimal(18,2)");
                entity.Property(item => item.FinancialDifference).HasColumnType("decimal(18,2)");
                entity.HasIndex(item => new { item.StockCountSessionId, item.ProductId }).IsUnique();
                entity.HasOne(item => item.Product)
                    .WithMany()
                    .HasForeignKey(item => item.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(item => item.StockTransaction)
                    .WithMany()
                    .HasForeignKey(item => item.StockTransactionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ServiceJob>(entity =>
            {
                entity.Property(e => e.JobDetails)
                      .HasColumnType("jsonb")
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }),
                          v => JsonSerializer.Deserialize<JobDetailBase>(v, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new GeneralJobDetail()
                      );
            });

            modelBuilder.Entity<Brand>().HasData(
                new Brand { Id = 1, Name = "Hikvision" },
                new Brand { Id = 2, Name = "Dahua" },
                new Brand { Id = 3, Name = "Next" }
            );

            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 3, Name = "Diafon" }
            );

            modelBuilder.Entity<TaskPhoto>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.Task)
                    .WithMany()
                    .HasForeignKey(e => e.TaskId).IsRequired(false).OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasOne(e => e.UploadedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.UploadedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.TaskId);
                entity.HasIndex(e => new { e.TaskId, e.IsDeleted });
            });

            modelBuilder.Entity<ServiceJobHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.ServiceJob)
                    .WithMany()
                    .HasForeignKey(e => e.ServiceJobId).IsRequired(false).OnDelete(DeleteBehavior.ClientSetNull);

                entity.HasIndex(e => e.ServiceJobId);
                entity.HasIndex(e => e.PerformedAt);
            });

            modelBuilder.Entity<SalesOrderPayment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.SalesOrder)
                    .WithMany(s => s.Payments)
                    .HasForeignKey(e => e.SalesOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PurchaseOrderPayment>(entity =>
            {
                entity.HasOne(payment => payment.PurchaseOrder)
                    .WithMany(order => order.Payments)
                    .HasForeignKey(payment => payment.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SalesOrder>(entity =>
            {
                entity.Property(e => e.IdempotencyKey).HasMaxLength(36);
                entity.HasIndex(e => e.IdempotencyKey)
                    .IsUnique()
                    .HasFilter("\"IdempotencyKey\" IS NOT NULL");
                entity.HasOne(order => order.Warehouse)
                    .WithMany()
                    .HasForeignKey(order => order.WarehouseId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PurchaseOrder>(entity =>
            {
                entity.Property(order => order.IdempotencyKey).HasMaxLength(36);
                entity.Property(order => order.ReceiptIdempotencyKey).HasMaxLength(36);
                entity.HasIndex(order => order.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
                entity.HasIndex(order => order.ReceiptIdempotencyKey).IsUnique().HasFilter("\"ReceiptIdempotencyKey\" IS NOT NULL");
                entity.HasOne(order => order.Warehouse)
                    .WithMany()
                    .HasForeignKey(order => order.WarehouseId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SalesReturn>(entity =>
            {
                entity.Property(item => item.IdempotencyKey).HasMaxLength(36);
                entity.Property(item => item.ReturnNumber).HasMaxLength(48);
                entity.HasIndex(item => item.IdempotencyKey).IsUnique();
                entity.HasIndex(item => item.ReturnNumber).IsUnique();
                entity.HasOne(item => item.SalesOrder).WithMany().HasForeignKey(item => item.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<SalesReturnItem>(entity =>
            {
                entity.HasOne(item => item.SalesReturn).WithMany(item => item.Items).HasForeignKey(item => item.SalesReturnId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.SalesOrderItem).WithMany().HasForeignKey(item => item.SalesOrderItemId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<SalesReturnPayment>(entity =>
            {
                entity.HasOne(item => item.SalesReturn).WithMany(item => item.Payments).HasForeignKey(item => item.SalesReturnId).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<PurchaseReturn>(entity =>
            {
                entity.Property(item => item.IdempotencyKey).HasMaxLength(36);
                entity.Property(item => item.ReturnNumber).HasMaxLength(48);
                entity.HasIndex(item => item.IdempotencyKey).IsUnique();
                entity.HasIndex(item => item.ReturnNumber).IsUnique();
                entity.HasOne(item => item.PurchaseOrder).WithMany().HasForeignKey(item => item.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<PurchaseReturnItem>(entity =>
            {
                entity.HasOne(item => item.PurchaseReturn).WithMany(item => item.Items).HasForeignKey(item => item.PurchaseReturnId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.PurchaseOrderItem).WithMany().HasForeignKey(item => item.PurchaseOrderItemId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Transaction>()
                .HasIndex(item => item.ReconciliationKey)
                .IsUnique()
                .HasFilter("\"ReconciliationKey\" IS NOT NULL");

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Barcode);

            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(e => e.AverageCost).HasColumnType("decimal(18,4)");
            });

            modelBuilder.Entity<PosTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TransactionNumber).IsUnique();
                entity.Property(e => e.SubTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.VatTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.GrandTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CashAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CardAmount).HasColumnType("decimal(18,2)");

                entity.HasMany(e => e.Lines)
                      .WithOne(l => l.PosTransaction)
                      .HasForeignKey(l => l.PosTransactionId);

                entity.HasOne(e => e.Customer)
                      .WithMany()
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.CashierUser)
                      .WithMany()
                      .HasForeignKey(e => e.CashierUserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<PosTransactionLine>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountValue).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.VatAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.NetTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PurchaseInvoice>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.InvoiceNumber).IsUnique();
                entity.Property(e => e.SubTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.VatTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.GrandTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PaidAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.RemainingAmount).HasColumnType("decimal(18,2)");

                entity.HasMany(e => e.Lines)
                      .WithOne(l => l.PurchaseInvoice)
                      .HasForeignKey(l => l.PurchaseInvoiceId);

                entity.HasOne(e => e.Supplier)
                      .WithMany()
                      .HasForeignKey(e => e.SupplierId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PurchaseInvoiceLine>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.VatAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.OldAverageCost).HasColumnType("decimal(18,4)");
                entity.Property(e => e.NewAverageCost).HasColumnType("decimal(18,4)");

                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }

        public override int SaveChanges() => SaveChanges(acceptAllChangesOnSuccess: true);

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            PrepareChanges();
            try
            {
                return base.SaveChanges(acceptAllChangesOnSuccess);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                ResolveConcurrencyConflicts(ex);
                return base.SaveChanges(acceptAllChangesOnSuccess);
            }
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

        public override async Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            PrepareChanges();
            try
            {
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                ResolveConcurrencyConflicts(ex);
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }
        }

        private void PrepareChanges()
        {
            ProtectCompletedReturns();
            ProtectAndSealActivityLogs();
            ApplyAuditInformation();
        }

        private void ProtectCompletedReturns()
        {
            var protectedTypes = new[]
            {
                typeof(SalesReturn), typeof(SalesReturnItem), typeof(SalesReturnPayment),
                typeof(PurchaseReturn), typeof(PurchaseReturnItem)
            };

            foreach (var entry in ChangeTracker.Entries())
            {
                if (protectedTypes.Contains(entry.Metadata.ClrType) &&
                    entry.State is EntityState.Modified or EntityState.Deleted)
                {
                    throw new InvalidOperationException(
                        "Tamamlanmış iade kayıtları değiştirilemez veya silinemez; düzeltme için telafi işlemi oluşturulmalıdır.");
                }
            }
        }

        private void ProtectAndSealActivityLogs()
        {
            foreach (var entry in ChangeTracker.Entries<ActivityLog>())
            {
                if (entry.State is EntityState.Modified or EntityState.Deleted)
                {
                    throw new InvalidOperationException(
                        "Denetim kayıtları değiştirilemez veya silinemez. Düzeltme gerekiyorsa yeni bir denetim kaydı ekleyin.");
                }

                if (entry.State != EntityState.Added)
                {
                    continue;
                }

                var log = entry.Entity;
                log.ActionType = string.IsNullOrWhiteSpace(log.ActionType) ? log.Action : log.ActionType;
                log.Action = string.IsNullOrWhiteSpace(log.Action) ? log.ActionType : log.Action;
                log.RecordId ??= log.ReferenceId;
                log.ReferenceId ??= log.RecordId;
                log.Timestamp = log.Timestamp == default
                    ? DateTime.UtcNow
                    : log.Timestamp.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(log.Timestamp, DateTimeKind.Utc)
                        : log.Timestamp.ToUniversalTime();
                ActivityLogIntegrity.Seal(log);
            }
        }

        private void ResolveConcurrencyConflicts(DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                try
                {
                    var databaseValues = entry.GetDatabaseValues();
                    if (databaseValues == null)
                    {
                        // Entity was deleted from database
                        entry.State = EntityState.Detached;
                    }
                    else
                    {
                        // Refresh original tracking values with latest database values (including xmin concurrency token)
                        entry.OriginalValues.SetValues(databaseValues);
                    }
                }
                catch
                {
                    // Fallback: detach entry if conflict cannot be resolved
                    entry.State = EntityState.Detached;
                }
            }
        }

        private void ApplyAuditInformation()
        {
            var entries = ChangeTracker.Entries<BaseEntity>();
            var currentUser = "System";
            var timestamp = DateTime.UtcNow;

            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedDate = timestamp;
                        entry.Entity.CreatedBy = currentUser;
                        entry.Entity.IsDeleted = false;
                        break;

                    case EntityState.Modified:
                        if (!entry.Entity.IsDeleted)
                        {
                            entry.Entity.ModifiedDate = timestamp;
                            entry.Entity.ModifiedBy = currentUser;
                        }
                        break;

                    case EntityState.Deleted:
                        entry.State = EntityState.Modified;
                        entry.Entity.IsDeleted = true;
                        entry.Entity.DeletedAt = timestamp;
                        entry.Entity.DeletedBy = currentUser;

                        foreach (var navigation in entry.Metadata.GetNavigations())
                        {
                            if (navigation.IsCollection)
                            {
                                var collection = entry.Collection(navigation.Name);
                                if (!collection.IsLoaded)
                                {
                                    collection.Load();
                                }
                                
                                if (collection.CurrentValue != null)
                                {
                                    foreach (var dependent in collection.CurrentValue)
                                    {
                                        if (dependent is ISoftDeletable sd)
                                        {
                                            var dependentEntry = Entry(dependent);
                                            dependentEntry.State = EntityState.Modified;
                                            sd.IsDeleted = true;
                                            sd.DeletedAt = timestamp;
                                            sd.DeletedBy = currentUser;
                                        }
                                    }
                                }
                            }
                        }
                        break;
                }
            }
        }
    }
}
