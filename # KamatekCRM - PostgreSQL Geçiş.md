# KamatekCRM - PostgreSQL Geçiş ve Hibrit Mimari Kurulum Rehberi

## 📋 GENEL BİLGİLER

**Hedef:** SQLite'tan PostgreSQL'e geçiş yaparak WPF ve Web uygulamalarının aynı veritabanı üzerinde eş zamanlı, çakışmasız çalışmasını sağlamak.

**Mevcut Durum:**
- ✅ WPF Desktop Application (NET 9.0)
- ✅ Blazor Web Application (NET 9.0)
- ✅ Shared Models Library
- ❌ SQLite (Tek kullanıcı, dosya kilitleme sorunu)

**Hedef Durum:**
- ✅ PostgreSQL (Çoklu kullanıcı, ACID, güvenilir)
- ✅ WPF + Web eş zamanlı çalışma
- ✅ Connection pooling
- ✅ Migration sistemi
- ✅ Yedekleme stratejisi

**Neden PostgreSQL?**
1. **Çoklu Bağlantı**: WPF ve Web aynı anda bağlanabilir
2. **ACID Uyumluluğu**: Transaction güvenliği
3. **Performans**: Büyük veri setlerinde hızlı
4. **JSON Desteği**: Teknik spec'ler için native JSON
5. **Production Ready**: Enterprise-grade güvenilirlik
6. **Ücretsiz ve Open Source**

---

## 🏗️ MİMARİ TASARIM

### Hibrit Mimari (WPF Host + API + Web)

```
┌─────────────────────────────────────────────────────────────────┐
│                     KAMATEKCRM EKOSİSTEMİ                       │
└─────────────────────────────────────────────────────────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │                         │
          ┌─────────▼──────────┐    ┌────────▼─────────┐
          │   WPF Desktop      │    │   Blazor Web     │
          │   (Ana Uygulama)   │    │   (Teknisyen)    │
          │   Port: -          │    │   Port: 7000     │
          └─────────┬──────────┘    └────────┬─────────┘
                    │                        │
                    │   ┌────────────────────┘
                    │   │
          ┌─────────▼───▼──────────┐
          │   ASP.NET Core API     │
          │   (WPF içinde hosted)  │
          │   Port: 5050           │
          │   JWT Authentication   │
          └─────────┬──────────────┘
                    │
                    │ Connection Pool
                    │ (Min: 5, Max: 100)
                    │
          ┌─────────▼──────────────┐
          │   PostgreSQL Server    │
          │   Port: 5432           │
          │   Database: kamatekcrm │
          │   User: kamatek_admin  │
          └────────────────────────┘
```

### Veri Akış Şemaları

**WPF Veri Akışı:**
```
WPF ViewModel → Repository → AppDbContext → Npgsql → PostgreSQL
```

**Web Veri Akışı:**
```
Blazor Component → HttpClient → API Controller → AppDbContext → Npgsql → PostgreSQL
```

**Eş Zamanlı Erişim:**
```
WPF:  DbContext (Instance 1) ──┐
                                ├──> Connection Pool ──> PostgreSQL
Web:  DbContext (Instance 2) ──┘
```

---

## 🚀 GELİŞTİRME ADIMLARI

# AŞAMA 1: POSTGRESQL KURULUMU VE YAPILANDIRMA

## ADIM 1.1: POSTGRESQL SERVER KURULUMU

### 1.1.1 PostgreSQL İndirme ve Kurulum

**Windows için:**

1. **PostgreSQL İndir:**
   - https://www.postgresql.org/download/windows/
   - PostgreSQL 16.x (Son stable versiyon)
   - Download: `postgresql-16.x-windows-x64.exe`

2. **Kurulum Adımları:**

```plaintext
Installer Çalıştır:
├─ Select Components:
│  ✅ PostgreSQL Server
│  ✅ pgAdmin 4 (GUI yönetim aracı)
│  ✅ Command Line Tools
│  ❌ Stack Builder (gerekli değil)
│
├─ Installation Directory:
│  → C:\Program Files\PostgreSQL\16
│
├─ Data Directory:
│  → C:\Program Files\PostgreSQL\16\data
│
├─ Password (postgres superuser):
│  → ŞİFRE: PostgreSQL123!
│  ⚠️  BU ŞİFREYİ MUTLAKA KAYDET!
│
├─ Port:
│  → 5432 (default)
│
└─ Locale:
   → Turkish, Turkey
```

3. **Kurulum Sonrası Doğrulama:**

```bash
# Command Prompt'ta
psql --version
# Çıktı: psql (PostgreSQL) 16.x

# PostgreSQL servisini kontrol et
sc query postgresql-x64-16
# STATE: RUNNING olmalı
```

### 1.1.2 pgAdmin 4 ile İlk Bağlantı

1. **pgAdmin 4'ü Aç:**
   - Başlat Menüsü → pgAdmin 4

2. **Server Bağlantısı:**
   ```
   Servers → PostgreSQL 16
   └─ Şifre girin: PostgreSQL123!
   ```

3. **Bağlantı Testi:**
   ```sql
   -- Query Tool'da çalıştır
   SELECT version();
   ```

### 1.1.3 KamatekCRM Veritabanı Oluşturma

**pgAdmin 4'te:**

```sql
-- 1. Database Oluştur
CREATE DATABASE kamatekcrm
    WITH 
    OWNER = postgres
    ENCODING = 'UTF8'
    LC_COLLATE = 'Turkish_Turkey.1254'
    LC_CTYPE = 'Turkish_Turkey.1254'
    TABLESPACE = pg_default
    CONNECTION LIMIT = -1;

COMMENT ON DATABASE kamatekcrm
    IS 'KamatekCRM ERP Veritabanı';
```

```sql
-- 2. Kullanıcı Oluştur (Güvenlik için)
CREATE USER kamatek_admin WITH
    LOGIN
    SUPERUSER
    CREATEDB
    CREATEROLE
    REPLICATION
    PASSWORD 'Kamatek2024!';

COMMENT ON ROLE kamatek_admin
    IS 'KamatekCRM Ana Kullanıcı';
```

```sql
-- 3. Veritabanı Yetkilerini Ver
GRANT ALL PRIVILEGES ON DATABASE kamatekcrm TO kamatek_admin;

-- kamatekcrm veritabanına geç
\c kamatekcrm

-- Schema yetkileri
GRANT ALL ON SCHEMA public TO kamatek_admin;

-- Gelecekte oluşturulacak tablolar için yetki
ALTER DEFAULT PRIVILEGES IN SCHEMA public
GRANT ALL ON TABLES TO kamatek_admin;

ALTER DEFAULT PRIVILEGES IN SCHEMA public
GRANT ALL ON SEQUENCES TO kamatek_admin;
```

### 1.1.4 Connection String Hazırlama

**Üretim Connection String:**
```
Host=localhost;Port=5432;Database=kamatekcrm;Username=kamatek_admin;Password=Kamatek2024!;
```

**Development Connection String:**
```
Host=localhost;Port=5432;Database=kamatekcrm_dev;Username=kamatek_admin;Password=Kamatek2024!;Include Error Detail=true;
```

**Test Connection String:**
```
Host=localhost;Port=5432;Database=kamatekcrm_test;Username=kamatek_admin;Password=Kamatek2024!;
```

---

## ADIM 1.2: NPGSQL PAKET KURULUMU

### 1.2.1 NuGet Paketleri

**Tüm projelere (KamatekCrm, KamatekCrm.API, KamatekCrm.Web):**

```bash
# SQLite paketlerini KALDIR
dotnet remove package Microsoft.EntityFrameworkCore.Sqlite

# PostgreSQL paketlerini EKLE
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.0
dotnet add package Npgsql --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Tools --version 8.0.0
```

**Kontrol:**
```bash
dotnet list package
```

Çıktıda görülmeli:
```
Npgsql.EntityFrameworkCore.PostgreSQL    8.0.0
Npgsql                                   8.0.0
Microsoft.EntityFrameworkCore.Tools      8.0.0
```

---

## ADIM 1.3: APPDBCONTEXT GÜNCELLEMESİ

### 1.3.1 AppDbContext.cs (Shared veya WPF)

**Konum:** `KamatekCrm/Data/AppDbContext.cs` veya `KamatekCrm.Shared/Data/AppDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Models;
using Npgsql;

namespace KamatekCrm.Data
{
    public class AppDbContext : DbContext
    {
        // Constructor 1: Parameterless (WPF için)
        public AppDbContext()
        {
        }

        // Constructor 2: Options (API/Web için)
        public AppDbContext(DbContextOptions options) 
            : base(options)
        {
        }

        // DbSets - Mevcut entity'ler
        public DbSet Customers { get; set; } = null!;
        public DbSet Users { get; set; } = null!;
        public DbSet ServiceJobs { get; set; } = null!;
        public DbSet Products { get; set; } = null!;
        public DbSet Inventories { get; set; } = null!;
        public DbSet StockTransactions { get; set; } = null!;
        public DbSet ServiceProjects { get; set; } = null!;
        public DbSet ScopeNodes { get; set; } = null!;
        public DbSet ScopeNodeItems { get; set; } = null!;
        public DbSet Suppliers { get; set; } = null!;
        public DbSet PurchaseOrders { get; set; } = null!;
        public DbSet PurchaseOrderItems { get; set; } = null!;
        public DbSet Attachments { get; set; } = null!;
        public DbSet SalesOrders { get; set; } = null!;
        public DbSet SalesOrderItems { get; set; } = null!;
        public DbSet ServiceJobHistories { get; set; } = null!;
        public DbSet TaskPhotos { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // WPF için fallback connection string
                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
                
                var connectionString = environment == "Development"
                    ? "Host=localhost;Port=5432;Database=kamatekcrm_dev;Username=kamatek_admin;Password=Kamatek2024!;Include Error Detail=true;"
                    : "Host=localhost;Port=5432;Database=kamatekcrm;Username=kamatek_admin;Password=Kamatek2024!;";

                optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly("KamatekCrm");
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                    npgsqlOptions.CommandTimeout(60);
                });

                // Lazy loading kapalı (N+1 problemi önleme)
                optionsBuilder.UseLazyLoadingProxies(false);

                // Tracking behavior
                optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

                // Sensitive data logging (Development'ta true)
                optionsBuilder.EnableSensitiveDataLogging(environment == "Development");
                
                // Detailed errors (Development'ta true)
                optionsBuilder.EnableDetailedErrors(environment == "Development");

                // PostgreSQL-specific: Use NodaTime for date/time (opsiyonel)
                // optionsBuilder.UseNodaTime();
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // PostgreSQL-specific: Schema belirleme
            modelBuilder.HasDefaultSchema("public");

            // ============================================
            // ENTITY CONFIGURATIONS
            // ============================================

            ConfigureCustomer(modelBuilder);
            ConfigureUser(modelBuilder);
            ConfigureServiceJob(modelBuilder);
            ConfigureProduct(modelBuilder);
            ConfigureInventory(modelBuilder);
            ConfigureStockTransaction(modelBuilder);
            ConfigureServiceProject(modelBuilder);
            ConfigureScopeNode(modelBuilder);
            ConfigureSupplier(modelBuilder);
            ConfigurePurchaseOrder(modelBuilder);
            ConfigureAttachment(modelBuilder);
            ConfigureSalesOrder(modelBuilder);
            ConfigureServiceJobHistory(modelBuilder);
            ConfigureTaskPhoto(modelBuilder);

            // Seed data (opsiyonel - development için)
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                SeedInitialData(modelBuilder);
            }
        }

        // ============================================
        // CONFIGURATION METHODS
        // ============================================

        private void ConfigureCustomer(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(entity =>
            {
                entity.ToTable("customers");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn(); // PostgreSQL SERIAL

                entity.Property(e => e.CustomerCode)
                    .HasColumnName("customer_code")
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.Name)
                    .HasColumnName("name")
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.Type)
                    .HasColumnName("type")
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.Email)
                    .HasColumnName("email")
                    .HasMaxLength(100);

                entity.Property(e => e.Phone)
                    .HasColumnName("phone")
                    .HasMaxLength(20);

                entity.Property(e => e.TaxNumber)
                    .HasColumnName("tax_number")
                    .HasMaxLength(20);

                entity.Property(e => e.Address)
                    .HasColumnName("address")
                    .HasMaxLength(500);

                entity.Property(e => e.City)
                    .HasColumnName("city")
                    .HasMaxLength(100);

                entity.Property(e => e.District)
                    .HasColumnName("district")
                    .HasMaxLength(100);

                entity.Property(e => e.PostalCode)
                    .HasColumnName("postal_code")
                    .HasMaxLength(10);

                entity.Property(e => e.IsDeleted)
                    .HasColumnName("is_deleted")
                    .HasDefaultValue(false);

                entity.Property(e => e.DeletedAt)
                    .HasColumnName("deleted_at");

                entity.Property(e => e.DeletedBy)
                    .HasColumnName("deleted_by")
                    .HasMaxLength(100);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.CreatedBy)
                    .HasColumnName("created_by")
                    .HasMaxLength(100);

                entity.Property(e => e.ModifiedAt)
                    .HasColumnName("modified_at");

                entity.Property(e => e.ModifiedBy)
                    .HasColumnName("modified_by")
                    .HasMaxLength(100);

                // Indexes
                entity.HasIndex(e => e.CustomerCode).IsUnique();
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => new { e.Type, e.IsDeleted });
                entity.HasIndex(e => e.CreatedAt);
            });
        }

        private void ConfigureUser(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.Username)
                    .HasColumnName("username")
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(e => e.PasswordHash)
                    .HasColumnName("password_hash")
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(e => e.Email)
                    .HasColumnName("email")
                    .HasMaxLength(100);

                entity.Property(e => e.FullName)
                    .HasColumnName("full_name")
                    .HasMaxLength(200);

                entity.Property(e => e.Role)
                    .HasColumnName("role")
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.IsActive)
                    .HasColumnName("is_active")
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Indexes
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email);
            });
        }

        private void ConfigureServiceJob(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(entity =>
            {
                entity.ToTable("service_jobs");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.Title)
                    .HasColumnName("title")
                    .HasMaxLength(200);

                entity.Property(e => e.Description)
                    .HasColumnName("description");

                entity.Property(e => e.JobCategory)
                    .HasColumnName("job_category")
                    .HasConversion()
                    .HasMaxLength(50);

                entity.Property(e => e.Status)
                    .HasColumnName("status")
                    .HasConversion()
                    .HasMaxLength(50);

                entity.Property(e => e.Priority)
                    .HasColumnName("priority")
                    .HasConversion()
                    .HasMaxLength(50);

                entity.Property(e => e.ScheduledDate)
                    .HasColumnName("scheduled_date");

                entity.Property(e => e.EstimatedDuration)
                    .HasColumnName("estimated_duration");

                entity.Property(e => e.ActualDuration)
                    .HasColumnName("actual_duration");

                entity.Property(e => e.TotalCost)
                    .HasColumnName("total_cost")
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.IsDeleted)
                    .HasColumnName("is_deleted")
                    .HasDefaultValue(false);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.CreatedBy)
                    .HasColumnName("created_by")
                    .HasMaxLength(100);

                entity.Property(e => e.ModifiedAt)
                    .HasColumnName("modified_at");

                entity.Property(e => e.ModifiedBy)
                    .HasColumnName("modified_by")
                    .HasMaxLength(100);

                // Foreign Keys
                entity.HasOne(e => e.Customer)
                    .WithMany()
                    .HasForeignKey("customer_id")
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.AssignedTechnician)
                    .WithMany()
                    .HasForeignKey("assigned_technician_id")
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.Priority);
                entity.HasIndex(e => e.ScheduledDate);
                entity.HasIndex(e => new { e.Status, e.IsDeleted });
            });
        }

        private void ConfigureProduct(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(entity =>
            {
                entity.ToTable("products");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.ProductCode)
                    .HasColumnName("product_code")
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(e => e.Name)
                    .HasColumnName("name")
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.Category)
                    .HasColumnName("category")
                    .HasMaxLength(100);

                entity.Property(e => e.Brand)
                    .HasColumnName("brand")
                    .HasMaxLength(100);

                entity.Property(e => e.Model)
                    .HasColumnName("model")
                    .HasMaxLength(100);

                entity.Property(e => e.UnitPrice)
                    .HasColumnName("unit_price")
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.Currency)
                    .HasColumnName("currency")
                    .HasMaxLength(10)
                    .HasDefaultValue("TRY");

                // JSON field için PostgreSQL native JSON
                entity.Property(e => e.TechSpecsJson)
                    .HasColumnName("tech_specs_json")
                    .HasColumnType("jsonb"); // PostgreSQL JSONB (Binary JSON, daha hızlı)

                entity.Property(e => e.IsDeleted)
                    .HasColumnName("is_deleted")
                    .HasDefaultValue(false);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Indexes
                entity.HasIndex(e => e.ProductCode).IsUnique();
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.Name);
                
                // PostgreSQL: JSON field indexing (GIN index)
                entity.HasIndex(e => e.TechSpecsJson)
                    .HasMethod("gin");
            });
        }

        private void ConfigureInventory(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(entity =>
            {
                entity.ToTable("inventories");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.Quantity)
                    .HasColumnName("quantity")
                    .HasDefaultValue(0);

                entity.Property(e => e.ReorderLevel)
                    .HasColumnName("reorder_level")
                    .HasDefaultValue(0);

                entity.Property(e => e.LastUpdated)
                    .HasColumnName("last_updated")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Foreign Keys
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey("product_id")
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Warehouse)
                    .WithMany()
                    .HasForeignKey("warehouse_id")
                    .OnDelete(DeleteBehavior.Restrict);

                // Composite unique index
                entity.HasIndex(e => new { e.ProductId, e.WarehouseId }).IsUnique();
            });
        }

        private void ConfigureStockTransaction(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(entity =>
            {
                entity.ToTable("stock_transactions");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.TransactionType)
                    .HasColumnName("transaction_type")
                    .HasConversion()
                    .HasMaxLength(50);

                entity.Property(e => e.Quantity)
                    .HasColumnName("quantity");

                entity.Property(e => e.UnitPrice)
                    .HasColumnName("unit_price")
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.Notes)
                    .HasColumnName("notes");

                entity.Property(e => e.TransactionDate)
                    .HasColumnName("transaction_date")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.PerformedBy)
                    .HasColumnName("performed_by")
                    .HasMaxLength(100);

                // Foreign Keys
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey("product_id")
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Warehouse)
                    .WithMany()
                    .HasForeignKey("warehouse_id")
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(e => e.TransactionDate);
                entity.HasIndex(e => e.TransactionType);
                entity.HasIndex(e => new { e.ProductId, e.TransactionDate });
            });
        }

        private void ConfigureServiceProject(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(entity =>
            {
                entity.ToTable("service_projects");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.ProjectScopeJson)
                    .HasColumnName("project_scope_json")
                    .HasColumnType("jsonb"); // PostgreSQL JSONB

                entity.Property(e => e.TotalCost)
                    .HasColumnName("total_cost")
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.TotalProfit)
                    .HasColumnName("total_profit")
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.WorkflowStatus)
                    .HasColumnName("workflow_status")
                    .HasConversion()
                    .HasMaxLength(50);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Foreign Key
                entity.HasOne(e => e.ServiceJob)
                    .WithOne()
                    .HasForeignKey("service_job_id")
                    .OnDelete(DeleteBehavior.Cascade);

                // Index
                entity.HasIndex(e => e.WorkflowStatus);
            });
        }

        private void ConfigureScopeNode(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(entity =>
            {
                entity.ToTable("scope_nodes");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.Name)
                    .HasColumnName("name")
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.Type)
                    .HasColumnName("type")
                    .HasMaxLength(50);

                entity.Property(e => e.Order)
                    .HasColumnName("order");

                // Self-referencing for tree structure
                entity.HasOne(e => e.ParentNode)
                    .WithMany(e => e.ChildNodes)
                    .HasForeignKey("parent_node_id")
                    .OnDelete(DeleteBehavior.Restrict);

                // Foreign Key to ServiceProject
                entity.HasOne(e => e.ServiceProject)
                    .WithMany(p => p.ScopeNodes)
                    .HasForeignKey("service_project_id")
                    .OnDelete(DeleteBehavior.Cascade);

                // Index
                entity.HasIndex(e => new { e.ServiceProjectId, e.ParentNodeId });
            });
        }

        private void ConfigureSupplier(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(entity =>
            {
                entity.ToTable("suppliers");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.Name)
                    .HasColumnName("name")
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.SupplierType)
                    .HasColumnName("supplier_type")
                    .HasConversion()
                    .HasMaxLength(50);

                entity.Property(e => e.ContactPerson)
                    .HasColumnName("contact_person")
                    .HasMaxLength(100);

                entity.Property(e => e.Email)
                    .HasColumnName("email")
                    .HasMaxLength(100);

                entity.Property(e => e.Phone)
                    .HasColumnName("phone")
                    .HasMaxLength(20);

                entity.Property(e => e.Address)
                    .HasColumnName("address")
                    .HasMaxLength(500);

                entity.Property(e => e.PaymentTermDays)
                    .HasColumnName("payment_term_days")
                    .HasDefaultValue(30);

                entity.Property(e => e.Balance)
                    .HasColumnName("balance")
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                entity.Property(e => e.IsDeleted)
                    .HasColumnName("is_deleted")
                    .HasDefaultValue(false);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Indexes
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.Email);
            });
        }

        private void ConfigurePurchaseOrder(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(entity =>
            {
                entity.ToTable("purchase_orders");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.OrderNumber)
                    .HasColumnName("order_number")
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.OrderDate)
                    .HasColumnName("order_date")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.ExpectedDeliveryDate)
                    .HasColumnName("expected_delivery_date");

                entity.Property(e => e.Status)
                    .HasColumnName("status")
                    .HasMaxLength(50);

                entity.Property(e => e.TotalAmount)
                    .HasColumnName("total_amount")
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.Notes)
                    .HasColumnName("notes");

                entity.Property(e => e.IsDeleted)
                    .HasColumnName("is_deleted")
                    .HasDefaultValue(false);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Foreign Key
                entity.HasOne(e => e.Supplier)
                    .WithMany()
                    .HasForeignKey("supplier_id")
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(e => e.OrderNumber).IsUnique();
                entity.HasIndex(e => e.OrderDate);
                entity.HasIndex(e => e.Status);
            });

            modelBuilder.Entity(entity =>
            {
                entity.ToTable("purchase_order_items");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.Quantity)
                    .HasColumnName("quantity");

                entity.Property(e => e.UnitPrice)
                    .HasColumnName("unit_price")
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.TotalPrice)
                    .HasColumnName("total_price")
                    .HasColumnType("decimal(18,2)");

                // Foreign Keys
                entity.HasOne(e => e.PurchaseOrder)
                    .WithMany(po => po.Items)
                    .HasForeignKey("purchase_order_id")
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey("product_id")
                    .OnDelete(DeleteBehavior.Restrict);

                // Index
                entity.HasIndex(e => e.PurchaseOrderId);
            });
        }

        private void ConfigureAttachment(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(entity =>
            {
                entity.ToTable("attachments");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.EntityType)
                    .HasColumnName("entity_type")
                    .HasConversion()
                    .HasMaxLength(50);

                entity.Property(e => e.EntityId)
                    .HasColumnName("entity_id");

                entity.Property(e => e.FileName)
                    .HasColumnName("file_name")
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(e => e.FilePath)
                    .HasColumnName("file_path")
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(e => e.FileSize)
                    .HasColumnName("file_size");

                entity.Property(e => e.FileType)
                    .HasColumnName("file_type")
                    .HasMaxLength(50);

                entity.Property(e => e.Description)
                    .HasColumnName("description");

                entity.Property(e => e.UploadedAt)
                    .HasColumnName("uploaded_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UploadedBy)
                    .HasColumnName("uploaded_by")
                    .HasMaxLength(100);

                entity.Property(e => e.IsDeleted)
                    .HasColumnName("is_deleted")
                    .HasDefaultValue(false);

                // Indexes
                entity.HasIndex(e => new { e.EntityType, e.EntityId });
                entity.HasIndex(e => e.UploadedAt);
            });
        }

        private void ConfigureSalesOrder(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(entity =>
            {
                entity.ToTable("sales_orders");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.OrderNumber)
                    .HasColumnName("order_number")
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.OrderDate)
                    .HasColumnName("order_date")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.Status)
                    .HasColumnName("status")
                    .HasMaxLength(50);

                entity.Property(e => e.TotalAmount)
                    .HasColumnName("total_amount")
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.PaidAmount)
                    .HasColumnName("paid_amount")
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0);

                entity.Property(e => e.PaymentMethod)
                    .HasColumnName("payment_method")
                    .HasConversion()
                    .HasMaxLength(50);

                entity.Property(e => e.Notes)
                    .HasColumnName("notes");

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.CreatedBy)
                    .HasColumnName("created_by")
                    .HasMaxLength(100);

                // Foreign Key
                entity.HasOne(e => e.Customer)
                    .WithMany()
                    .HasForeignKey("customer_id")
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(e => e.OrderNumber).IsUnique();
                entity.HasIndex(e => e.OrderDate);
                entity.HasIndex(e => e.Status);
            });

            modelBuilder.Entity(entity =>
            {
                entity.ToTable("sales_order_items");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.Quantity)
                    .HasColumnName("quantity");

                entity.Property(e => e.UnitPrice)
                    .HasColumnName("unit_price")
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.TotalPrice)
                    .HasColumnName("total_price")
                    .HasColumnType("decimal(18,2)");

                // Foreign Keys
                entity.HasOne(e => e.SalesOrder)
                    .WithMany(so => so.Items)
                    .HasForeignKey("sales_order_id")
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey("product_id")
                    .OnDelete(DeleteBehavior.Restrict);

                // Index
                entity.HasIndex(e => e.SalesOrderId);
            });
        }

        private void ConfigureServiceJobHistory(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(entity =>
            {
                entity.ToTable("service_job_histories");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.Action)
                    .HasColumnName("action")
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.Notes)
                    .HasColumnName("notes");

                entity.Property(e => e.PerformedBy)
                    .HasColumnName("performed_by");

                entity.Property(e => e.PerformedAt)
                    .HasColumnName("performed_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Foreign Key
                entity.HasOne(e => e.ServiceJob)
                    .WithMany()
                    .HasForeignKey("service_job_id")
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes
                entity.HasIndex(e => e.ServiceJobId);
                entity.HasIndex(e => e.PerformedAt);
            });
        }

        private void ConfigureTaskPhoto(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(entity =>
            {
                entity.ToTable("task_photos");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityByDefaultColumn();

                entity.Property(e => e.FileName)
                    .HasColumnName("file_name")
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(e => e.FilePath)
                    .HasColumnName("file_path")
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(e => e.ThumbnailPath)
                    .HasColumnName("thumbnail_path")
                    .HasMaxLength(500);

                entity.Property(e => e.FileSize)
                    .HasColumnName("file_size");

                entity.Property(e => e.MimeType)
                    .HasColumnName("mime_type")
                    .HasMaxLength(100);

                entity.Property(e => e.Description)
                    .HasColumnName("description");

                entity.Property(e => e.UploadedBy)
                    .HasColumnName("uploaded_by");

                entity.Property(e => e.UploadedAt)
                    .HasColumnName("uploaded_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.IsDeleted)
                    .HasColumnName("is_deleted")
                    .HasDefaultValue(false);

                entity.Property(e => e.DeletedAt)
                    .HasColumnName("deleted_at");

                // Foreign Keys
                entity.HasOne(e => e.Task)
                    .WithMany()
                    .HasForeignKey("task_id")
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.UploadedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.UploadedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(e => e.TaskId);
                entity.HasIndex(e => new { e.TaskId, e.IsDeleted });
            });
        }

        // ============================================
        // SEED DATA (Development Only)
        // ============================================

        private void SeedInitialData(ModelBuilder modelBuilder)
        {
            // Default Admin User
            modelBuilder.Entity().HasData(new User
            {
                Id = 1,
                Username = "admin.user",
                PasswordHash = "a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3", // "123" SHA256
                Email = "admin@kamatek.com",
                FullName = "Sistem Yöneticisi",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.Now
            });

            // Demo Customers
            modelBuilder.Entity().HasData(
                new Customer
                {
                    Id = 1,
                    CustomerCode = "CUS-000001",
                    Name = "Demo Müşteri 1",
                    Type = "Bireysel",
                    Email = "demo1@example.com",
                    Phone = "05551234567",
                    City = "İstanbul",
                    District = "Kadıköy",
                    CreatedAt = DateTime.Now,
                    CreatedBy = "System",
                    IsDeleted = false
                },
                new Customer
                {
                    Id = 2,
                    CustomerCode = "CUS-000002",
                    Name = "Demo Şirket A.Ş.",
                    Type = "Kurumsal",
                    Email = "info@demosirket.com",
                    Phone = "02121234567",
                    TaxNumber = "1234567890",
                    City = "İstanbul",
                    District = "Beşiktaş",
                    CreatedAt = DateTime.Now,
                    CreatedBy = "System",
                    IsDeleted = false
                }
            );
        }

        // ============================================
        // SAVECHANGES OVERRIDE (Audit & Soft Delete)
        // ============================================

        public override async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is Customer || 
                           e.Entity is ServiceJob || 
                           e.Entity is Product)
                .ToList();

            var currentUser = GetCurrentUser();
            var timestamp = DateTime.UtcNow;

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    // CreatedAt ve CreatedBy set et
                    entry.Property("CreatedAt").CurrentValue = timestamp;
                    entry.Property("CreatedBy").CurrentValue = currentUser;
                    entry.Property("IsDeleted").CurrentValue = false;
                }
                else if (entry.State == EntityState.Modified)
                {
                    // ModifiedAt ve ModifiedBy set et
                    entry.Property("ModifiedAt").CurrentValue = timestamp;
                    entry.Property("ModifiedBy").CurrentValue = currentUser;
                }
                else if (entry.State == EntityState.Deleted)
                {
                    // Soft Delete
                    entry.State = EntityState.Modified;
                    entry.Property("IsDeleted").CurrentValue = true;
                    entry.Property("DeletedAt").CurrentValue = timestamp;
                    entry.Property("DeletedBy").CurrentValue = currentUser;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }

        private string GetCurrentUser()
        {
            // WPF'te: App.CurrentUser?.Username
            // API'de: HttpContext.User.Identity?.Name
            // Fallback: "System"
            
            try
            {
                // WPF context
                if (App.CurrentUser != null)
                {
                    return App.CurrentUser.Username;
                }
            }
            catch
            {
                // API context veya başka bağlam
            }

            return "System";
        }
    }
}
```

**ÖNEMLİ NOTLAR:**

1. **Column Naming:** PostgreSQL convention'ı: `snake_case` (örn: `customer_code`)
2. **JSONB:** PostgreSQL'in binary JSON tipi kullanıldı (`tech_specs_json`, `project_scope_json`)
3. **Indexes:** Sık sorgulanan alanlar için index eklendi
4. **Identity Columns:** PostgreSQL `SERIAL` tipi için `UseIdentityByDefaultColumn()`
5. **Soft Delete:** Global query filter eklenmedi (performans için), manuel kontrol
6. **Timestamps:** `CURRENT_TIMESTAMP` PostgreSQL fonksiyonu

---

## ADIM 1.4: MİGRATİON OLUŞTURMA

### 1.4.1 İlk Migration

**Konum:** Solution root

```bash
# Eski migration'ları sil (varsa)
rm -rf KamatekCrm/Migrations

# Yeni migration oluştur
dotnet ef migrations add InitialPostgreSQLMigration --project KamatekCrm --startup-project KamatekCrm

# Migration'ı incele
# KamatekCrm/Migrations/[timestamp]_InitialPostgreSQLMigration.cs dosyasını aç ve kontrol et
```

**Beklenen Migration Dosyası:**

```csharp
public partial class InitialPostgreSQLMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "customers",
            columns: table => new
            {
                id = table.Column(nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                customer_code = table.Column(maxLength: 50, nullable: false),
                name = table.Column(maxLength: 200, nullable: false),
                // ... diğer kolonlar
            });

        // ... diğer tablolar

        migrationBuilder.CreateIndex(
            name: "IX_customers_customer_code",
            table: "customers",
            column: "customer_code",
            unique: true);

        // ... diğer indexler
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rollback kodları
    }
}
```

### 1.4.2 Migration Uygulama

```bash
# Development database'e uygula
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet ef database update --project KamatekCrm --startup-project KamatekCrm

# Production database'e uygula
$env:ASPNETCORE_ENVIRONMENT="Production"
dotnet ef database update --project KamatekCrm --startup-project KamatekCrm
```

### 1.4.3 Migration Doğrulama

**pgAdmin 4'te kontrol:**

```sql
-- Tabloları listele
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'public' 
ORDER BY table_name;

-- Beklenen çıktı:
-- attachments
-- customers
-- inventories
-- products
-- purchase_order_items
-- purchase_orders
-- sales_order_items
-- sales_orders
-- scope_node_items
-- scope_nodes
-- service_job_histories
-- service_jobs
-- service_projects
-- stock_transactions
-- suppliers
-- task_photos
-- users

-- Migration history
SELECT * FROM "__EFMigrationsHistory";
```

---

# AŞAMA 2: WPF UYGULAMASI YAPILANDIRMASI

## ADIM 2.1: DEPENDENCY INJECTION KURULUMU (WPF)

### 2.1.1 App.xaml.cs Güncellemesi

**Konum:** `KamatekCrm/App.xaml.cs`

```csharp
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Data;
using KamatekCrm.Services;
using KamatekCrm.ViewModels;
using Serilog;
using KamatekCrm.Configuration;

namespace KamatekCrm
{
    public partial class App : Application
    {
        private IHost? _host;
        public static IServiceProvider ServiceProvider { get; private set; } = null!;
        public static User? CurrentUser { get; set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Logging'i ilk iş olarak yapılandır
            LoggingConfiguration.ConfigureLogging();

            try
            {
                Log.Information("=== KamatekCRM WPF Starting ===");
                
                base.OnStartup(e);

                // Host Builder ile DI Container oluştur
                _host = Host.CreateDefaultBuilder()
                    .UseSerilog()
                    .ConfigureAppConfiguration((context, config) =>
                    {
                        config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
                        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                        config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                        config.AddEnvironmentVariables();
                    })
                    .ConfigureServices((context, services) =>
                    {
                        // Configuration
                        services.Configure(context.Configuration.GetSection("DatabaseSettings"));
                        
                        // DbContext (PostgreSQL)
                        var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
                        services.AddDbContext(options =>
                        {
                            options.UseNpgsql(connectionString, npgsqlOptions =>
                            {
                                npgsqlOptions.EnableRetryOnFailure(
                                    maxRetryCount: 5,
                                    maxRetryDelay: TimeSpan.FromSeconds(30),
                                    errorCodesToAdd: null);
                                npgsqlOptions.CommandTimeout(60);
                                npgsqlOptions.MigrationsAssembly("KamatekCrm");
                            });

                            options.UseLazyLoadingProxies(false);
                            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                            
                            if (context.HostingEnvironment.IsDevelopment())
                            {
                                options.EnableSensitiveDataLogging();
                                options.EnableDetailedErrors();
                            }
                        });

                        // Repositories
                        services.AddScoped();

                        // Services
                        services.AddSingleton();
                        services.AddSingleton();
                        services.AddSingleton();
                        services.AddScoped();
                        services.AddScoped();
                        services.AddScoped();
                        services.AddScoped();
                        services.AddScoped();
                        services.AddScoped();
                        services.AddScoped();

                        // Domain Services
                        services.AddScoped();
                        services.AddScoped();

                        // ViewModels
                        RegisterViewModels(services);
                    })
                    .Build();

                ServiceProvider = _host.Services;
                
                // Global Exception Handler
                GlobalExceptionHandler.Initialize(ServiceProvider.GetService<ILogger>());
                
                await _host.StartAsync();

                // Database migration check
                await EnsureDatabaseCreatedAsync();

                // Login penceresini aç
                var loginWindow = new LoginView
                {
                    DataContext = ServiceProvider.GetRequiredService()
                };
                loginWindow.Show();
                
                Log.Information("WPF application started successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "WPF application startup failed");
                MessageBox.Show($"Uygulama başlatılamadı:\n\n{ex.Message}\n\nDetaylar için log dosyasını kontrol edin.", 
                    "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private static void RegisterViewModels(IServiceCollection services)
        {
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
            services.AddTransient();
        }

        private async Task EnsureDatabaseCreatedAsync()
        {
            try
            {
                using var scope = ServiceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService();
                
                Log.Information("Checking database connection...");
                
                // Connection test
                var canConnect = await context.Database.CanConnectAsync();
                
                if (!canConnect)
                {
                    throw new Exception("PostgreSQL veritabanına bağlanılamıyor! Lütfen bağlantı ayarlarını kontrol edin.");
                }

                // Pending migrations check
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                
                if (pendingMigrations.Any())
                {
                    Log.Warning("Pending migrations found: {MigrationCount}", pendingMigrations.Count());
                    
                    var result = MessageBox.Show(
                        $"Veritabanında {pendingMigrations.Count()} bekleyen migration var.\n\nOtomatik olarak uygulansın mı?",
                        "Migration Gerekli",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        Log.Information("Applying pending migrations...");
                        await context.Database.MigrateAsync();
                        Log.Information("Migrations applied successfully");
                        
                        MessageBox.Show("Veritabanı güncellemeleri başarıyla uygulandı.", 
                            "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                Log.Information("Database connection successful");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Database initialization failed");
                throw;
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                Log.Information("WPF application shutting down...");
                
                if (_host != null)
                {
                    await _host.StopAsync();
                    _host.Dispose();
                }
                
                Log.Information("=== KamatekCRM WPF Stopped ===");
            }
            finally
            {
                Log.CloseAndFlush();
                base.OnExit(e);
            }
        }
    }
}
```

### 2.1.2 appsettings.json (WPF)

**Konum:** `KamatekCrm/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=kamatekcrm;Username=kamatek_admin;Password=Kamatek2024!;Pooling=true;MinPoolSize=5;MaxPoolSize=100;Timeout=30;CommandTimeout=60;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "DatabaseSettings": {
    "Provider": "PostgreSQL",
    "ConnectionTimeout": 30,
    "CommandTimeout": 60,
    "EnableRetryOnFailure": true,
    "MaxRetryCount": 5,
    "MaxRetryDelay": 30
  },
  "Application": {
    "Environment": "Production",
    "EnableDetailedErrors": false,
    "CacheDurationMinutes": 30,
    "MaxPageSize": 100
  }
}
```

**Konum:** `KamatekCrm/appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=kamatekcrm_dev;Username=kamatek_admin;Password=Kamatek2024!;Pooling=true;MinPoolSize=2;MaxPoolSize=20;Include Error Detail=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "Application": {
    "Environment": "Development",
    "EnableDetailedErrors": true
  }
}
```

### 2.1.3 DatabaseSettings Model

**Konum:** `KamatekCrm/Configuration/DatabaseSettings.cs`

```csharp
namespace KamatekCrm.Configuration
{
    public class DatabaseSettings
    {
        public string Provider { get; set; } = "PostgreSQL";
        public int ConnectionTimeout { get; set; } = 30;
        public int CommandTimeout { get; set; } = 60;
        public bool EnableRetryOnFailure { get; set; } = true;
        public int MaxRetryCount { get; set; } = 5;
        public int MaxRetryDelay { get; set; } = 30;
    }
}
```

---

# AŞAMA 3: API PROJESI YAPILANDIRMASI

## ADIM 3.1: API PROGRAM.CS GÜNCELLEMESİ

### 3.1.1 Program.cs (API)

**Konum:** `KamatekCrm.API/Program.cs` (veya WPF içinde hosted API)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using KamatekCrm.Data;
using MediatR;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console()
        .WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day);
});

// PostgreSQL DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
        npgsqlOptions.CommandTimeout(60);
    });

    options.UseLazyLoadingProxies(false);
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// MediatR
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Authentication (JWT)
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is missing");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "KamatekCRM API", Version = "v1" });
    
    // JWT Bearer Authorization
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty()
        }
    });
});

var app = builder.Build();

// Database migration check
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService();
    
    try
    {
        await context.Database.MigrateAsync();
        Log.Information("API Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "API Database migration failed");
    }
}

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Bind to all interfaces
app.Urls.Add("http://0.0.0.0:5050");

Log.Information("API Server starting on http://0.0.0.0:5050");

await app.RunAsync();
```

### 3.1.2 appsettings.json (API)

**Konum:** `KamatekCrm.API/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=kamatekcrm;Username=kamatek_admin;Password=Kamatek2024!;Pooling=true;MinPoolSize=5;MaxPoolSize=100;"
  },
  "JwtSettings": {
    "SecretKey": "YourSuperSecretKeyForJWTTokenGeneration2024!",
    "Issuer": "KamatekCRM",
    "Audience": "KamatekCRMClients",
    "ExpirationMinutes": 1440
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

---

# AŞAMA 4: BLAZOR WEB UYGULAMASI YAPILANDIRMASI

## ADIM 4.1: WEB PROGRAM.CS GÜNCELLEMESİ

### 4.1.1 Program.cs (Web)

**Konum:** `KamatekCrm.Web/Program.cs`

```csharp
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;
using Blazored.LocalStorage;
using KamatekCrm.Web.Services;
using KamatekCrm.Web.Authentication;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console()
        .WriteTo.File("logs/web-.log", rollingInterval: RollingInterval.Day);
});

// Blazor Server
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// MudBlazor
builder.Services.AddMudServices();

// Local Storage
builder.Services.AddBlazoredLocalStorage();

// HttpClient (API bağlantısı)
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5050";

builder.Services.AddHttpClient("API", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Application Services
builder.Services.AddScoped();
builder.Services.AddScoped();
builder.Services.AddScoped();
builder.Services.AddScoped();
builder.Services.AddScoped();

// Authentication
builder.Services.AddScoped();
builder.Services.AddAuthorizationCore();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Bind to all interfaces
app.Urls.Add("http://0.0.0.0:7000");

Log.Information("Web Server starting on http://0.0.0.0:7000");

await app.RunAsync();
```

### 4.1.2 appsettings.json (Web)

**Konum:** `KamatekCrm.Web/appsettings.json`

```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5050"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

---

# AŞAMA 5: VERİ TAŞIMA (SQLite → PostgreSQL)

## ADIM 5.1: VERİ EXPORT (SQLite'tan)

### 5.1.1 SQLite Veri Export Script

**Konum:** `scripts/export-sqlite-data.sql`

```sql
-- SQLite veritabanından veriyi CSV olarak export et

.headers on
.mode csv

.output customers.csv
SELECT * FROM Customers;

.output users.csv
SELECT * FROM Users;

.output products.csv
SELECT * FROM Products;

.output service_jobs.csv
SELECT * FROM ServiceJobs;

.output inventories.csv
SELECT * FROM Inventories;

.output stock_transactions.csv
SELECT * FROM StockTransactions;

-- Diğer tablolar için de tekrarla...

.output stdout
```

### 5.1.2 C# Veri Taşıma Utility

**Konum:** `scripts/DataMigrationUtility.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data.SQLite;

public class DataMigrationUtility
{
    private readonly string _sqliteConnectionString;
    private readonly string _postgresConnectionString;

    public DataMigrationUtility(string sqliteDb, string postgresCs)
    {
        _sqliteConnectionString = $"Data Source={sqliteDb}";
        _postgresConnectionString = postgresCs;
    }

    public async Task MigrateAllDataAsync()
    {
        Console.WriteLine("=== KamatekCRM Veri Taşıma Başlıyor ===\n");

        await MigrateUsersAsync();
        await MigrateCustomersAsync();
        await MigrateProductsAsync();
        await MigrateSuppliersAsync();
        await MigrateServiceJobsAsync();
        await MigrateInventoriesAsync();
        await MigrateStockTransactionsAsync();
        // Diğer tablolar...

        Console.WriteLine("\n=== Veri Taşıma Tamamlandı ===");
    }

    private async Task MigrateUsersAsync()
    {
        Console.WriteLine("Users tablosu taşınıyor...");

        using var sqliteConn = new SQLiteConnection(_sqliteConnectionString);
        await sqliteConn.OpenAsync();

        using var pgConn = new NpgsqlConnection(_postgresConnectionString);
        await pgConn.OpenAsync();

        // SQLite'tan veri çek
        var cmd = new SQLiteCommand("SELECT * FROM Users", sqliteConn);
        using var reader = await cmd.ExecuteReaderAsync();

        int count = 0;
        while (await reader.ReadAsync())
        {
            // PostgreSQL'e ekle
            var insertCmd = new NpgsqlCommand(@"
                INSERT INTO users (id, username, password_hash, email, full_name, role, is_active, created_at)
                VALUES (@id, @username, @passwordHash, @email, @fullName, @role, @isActive, @createdAt)
                ON CONFLICT (id) DO NOTHING", pgConn);

            insertCmd.Parameters.AddWithValue("id", reader["Id"]);
            insertCmd.Parameters.AddWithValue("username", reader["Username"]);
            insertCmd.Parameters.AddWithValue("passwordHash", reader["PasswordHash"]);
            insertCmd.Parameters.AddWithValue("email", reader["Email"] ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("fullName", reader["FullName"] ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("role", reader["Role"]);
            insertCmd.Parameters.AddWithValue("isActive", reader["IsActive"]);
            insertCmd.Parameters.AddWithValue("createdAt", reader["CreatedAt"]);

            await insertCmd.ExecuteNonQueryAsync();
            count++;
        }

        // Sequence reset (ID auto-increment için)
        var seqCmd = new NpgsqlCommand($"SELECT setval('users_id_seq', (SELECT MAX(id) FROM users))", pgConn);
        await seqCmd.ExecuteNonQueryAsync();

        Console.WriteLine($"  ✓ {count} kullanıcı taşındı\n");
    }

    private async Task MigrateCustomersAsync()
    {
        Console.WriteLine("Customers tablosu taşınıyor...");

        using var sqliteConn = new SQLiteConnection(_sqliteConnectionString);
        await sqliteConn.OpenAsync();

        using var pgConn = new NpgsqlConnection(_postgresConnectionString);
        await pgConn.OpenAsync();

        var cmd = new SQLiteCommand("SELECT * FROM Customers WHERE IsDeleted = 0", sqliteConn);
        using var reader = await cmd.ExecuteReaderAsync();

        int count = 0;
        while (await reader.ReadAsync())
        {
            var insertCmd = new NpgsqlCommand(@"
                INSERT INTO customers (
                    id, customer_code, name, type, email, phone, tax_number,
                    address, city, district, postal_code,
                    is_deleted, created_at, created_by
                )
                VALUES (
                    @id, @customerCode, @name, @type, @email, @phone, @taxNumber,
                    @address, @city, @district, @postalCode,
                    @isDeleted, @createdAt, @createdBy
                )
                ON CONFLICT (id) DO NOTHING", pgConn);

            insertCmd.Parameters.AddWithValue("id", reader["Id"]);
            insertCmd.Parameters.AddWithValue("customerCode", reader["CustomerCode"]);
            insertCmd.Parameters.AddWithValue("name", reader["Name"]);
            insertCmd.Parameters.AddWithValue("type", reader["Type"]);
            insertCmd.Parameters.AddWithValue("email", reader["Email"] ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("phone", reader["Phone"] ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("taxNumber", reader["TaxNumber"] ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("address", reader["Address"] ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("city", reader["City"] ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("district", reader["District"] ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("postalCode", reader["PostalCode"] ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("isDeleted", reader["IsDeleted"]);
            insertCmd.Parameters.AddWithValue("createdAt", reader["CreatedAt"]);
            insertCmd.Parameters.AddWithValue("createdBy", reader["CreatedBy"]);

            await insertCmd.ExecuteNonQueryAsync();
            count++;
        }

        // Sequence reset
        var seqCmd = new NpgsqlCommand($"SELECT setval('customers_id_seq', (SELECT MAX(id) FROM customers))", pgConn);
        await seqCmd.ExecuteNonQueryAsync();

        Console.WriteLine($"  ✓ {count} müşteri taşındı\n");
    }

    // Diğer tablolar için benzer metodlar...
}

// Kullanım
class Program
{
    static async Task Main(string[] args)
    {
        var utility = new DataMigrationUtility(
            sqliteDb: "kamatek.db",
            postgresCs: "Host=localhost;Port=5432;Database=kamatekcrm;Username=kamatek_admin;Password=Kamatek2024!;"
        );

        await utility.MigrateAllDataAsync();
    }
}
```

---

# AŞAMA 6: CONNECTION POOLING VE PERFORMANS

## ADIM 6.1: CONNECTION STRING OPTİMİZASYONU

### 6.1.1 Production Connection String

```
Host=localhost;
Port=5432;
Database=kamatekcrm;
Username=kamatek_admin;
Password=Kamatek2024!;
Pooling=true;
MinPoolSize=5;
MaxPoolSize=100;
ConnectionIdleLifetime=300;
ConnectionPruningInterval=10;
Timeout=30;
CommandTimeout=60;
KeepAlive=30;
```

**Parametre Açıklamaları:**

- **Pooling=true**: Connection pooling aktif
- **MinPoolSize=5**: Havuzda her zaman hazırda 5 connection
- **MaxPoolSize=100**: Maksimum 100 eş zamanlı connection (WPF + Web + API için yeterli)
- **ConnectionIdleLifetime=300**: Boşta 5 dakika bekleyen connection'lar kapanır
- **ConnectionPruningInterval=10**: Her 10 saniyede bir boşta connection'ları kontrol et
- **Timeout=30**: Bağlantı timeout 30 saniye
- **CommandTimeout=60**: Sorgu timeout 60 saniye
- **KeepAlive=30**: Her 30 saniyede keep-alive paketi gönder

### 6.1.2 DbContext Pooling (API için)

**Konum:** `Program.cs` (API)

```csharp
// DbContext yerine DbContextPooling kullan
builder.Services.AddDbContextPool(options =>
{
    options.UseNpgsql(connectionString);
}, poolSize: 128); // Pool size
```

---

# AŞAMA 7: EŞ ZAMANLI ÇALIŞMA TESTİ

## ADIM 7.1: TEST SENARYOLARI

### 7.1.1 Senaryo 1: WPF CRUD + Web API Okuma

**Test:**

1. WPF'te yeni müşteri oluştur
2. Aynı anda Web'den müşteri listesini görüntüle
3. WPF'te müşteriyi düzenle
4. Web'den tekrar listele (güncel veri gelecek)

**Beklenen:** Hiçbir deadlock veya connection timeout olmadan her iki taraf da çalışmalı.

### 7.1.2 Senaryo 2: Eş Zamanlı Stok Güncelleme

**Test:**

1. WPF'te stok hareketi ekle (örn: 10 adet çıkış)
2. Aynı anda Web'den aynı ürüne stok hareketi (örn: 5 adet giriş)
3. Her iki işlem de commit edilecek

**Beklenen:** Transaction isolation sayesinde her iki işlem de başarılı ve final stok doğru hesaplanmış olmalı.

### 7.1.3 Senaryo 3: WPF Kapanırken Web Devam Etmeli

**Test:**

1. WPF ve Web'i aynı anda aç
2. WPF'i kapat
3. Web hala çalışıyor olmalı
4. Web'den CRUD işlemleri yapabilmeli

**Beklenen:** WPF kapansa bile API ve PostgreSQL ayakta, Web çalışmaya devam ediyor.

---

# AŞAMA 8: YEDEKLEME STRATEJİSİ

## ADIM 8.1: POSTGRESQL YEDEKLEME

### 8.1.1 Manuel Yedekleme (pg_dump)

```bash
# Command Prompt veya PowerShell

# Full backup
pg_dump -h localhost -p 5432 -U kamatek_admin -F c -b -v -f "C:\Backups\kamatekcrm_backup_%date:~-4,4%%date:~-10,2%%date:~-7,2%.dump" kamatekcrm

# SQL format backup
pg_dump -h localhost -p 5432 -U kamatek_admin -F p -f "C:\Backups\kamatekcrm_backup.sql" kamatekcrm
```

### 8.1.2 Otomatik Yedekleme (PowerShell Script)

**Konum:** `scripts/PostgreSQL-Backup.ps1`

```powershell
# PostgreSQL Otomatik Yedekleme Scripti

$backupDir = "C:\Backups\KamatekCRM"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = "$backupDir\kamatekcrm_$timestamp.dump"
$logFile = "$backupDir\backup_log.txt"

# Klasörü oluştur
if (!(Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir
}

# Yedek al
Write-Host "PostgreSQL yedeği alınıyor..." -ForegroundColor Cyan

$env:PGPASSWORD = "Kamatek2024!"

& "C:\Program Files\PostgreSQL\16\bin\pg_dump.exe" `
    -h localhost `
    -p 5432 `
    -U kamatek_admin `
    -F c `
    -b `
    -v `
    -f $backupFile `
    kamatekcrm

if ($LASTEXITCODE -eq 0) {
    $message = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - Yedekleme başarılı: $backupFile"
    Write-Host $message -ForegroundColor Green
    Add-Content -Path $logFile -Value $message
} else {
    $message = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - Yedekleme HATALI!"
    Write-Host $message -ForegroundColor Red
    Add-Content -Path $logFile -Value $message
}

# Eski yedekleri sil (30 günden eski)
Write-Host "Eski yedekler temizleniyor..." -ForegroundColor Cyan
Get-ChildItem -Path $backupDir -Filter "*.dump" | 
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | 
    Remove-Item -Force

Write-Host "İşlem tamamlandı." -ForegroundColor Green
```

### 8.1.3 Windows Task Scheduler ile Otomatik Yedekleme

```powershell
# Görev Zamanlayıcı'ya ekle (PowerShell Admin)

$action = New-ScheduledTaskAction -Execute "PowerShell.exe" `
    -Argument "-ExecutionPolicy Bypass -File C:\scripts\PostgreSQL-Backup.ps1"

$trigger = New-ScheduledTaskTrigger -Daily -At 02:00AM

$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopOnIdleEnd

Register-ScheduledTask -TaskName "KamatekCRM PostgreSQL Backup" `
    -Action $action `
    -Trigger $trigger `
    -Principal $principal `
    -Settings $settings `
    -Description "Günlük PostgreSQL veritabanı yedeği"
```

### 8.1.4 Geri Yükleme (pg_restore)

```bash
# Dump dosyasından geri yükleme
pg_restore -h localhost -p 5432 -U kamatek_admin -d kamatekcrm -v "C:\Backups\kamatekcrm_backup.dump"

# SQL dosyasından geri yükleme
psql -h localhost -p 5432 -U kamatek_admin -d kamatekcrm < "C:\Backups\kamatekcrm_backup.sql"
```

---

# AŞAMA 9: GELIŞTIRME ORTAMI AYARLARI

## ADIM 9.1: MULTIPLE STARTUP PROJECTS

### 9.1.1 Visual Studio Solution Properties

**Adımlar:**

1. Solution Explorer → Solution'a sağ tık → Properties
2. Common Properties → Startup Project
3. Multiple startup projects seç
4. Projeleri şu şekilde ayarla:

```
KamatekCrm        → Start
KamatekCrm.API    → Start (eğer ayrı proje ise)
KamatekCrm.Web    → Start
```

### 9.1.2 launchSettings.json (API)

**Konum:** `KamatekCrm.API/Properties/launchSettings.json`

```json
{
  "profiles": {
    "KamatekCrm.API": {
      "commandName": "Project",
      "launchBrowser": true,
      "launchUrl": "swagger",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "applicationUrl": "http://localhost:5050"
    }
  }
}
```

### 9.1.3 launchSettings.json (Web)

**Konum:** `KamatekCrm.Web/Properties/launchSettings.json`

```json
{
  "profiles": {
    "KamatekCrm.Web": {
      "commandName": "Project",
      "launchBrowser": true,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "applicationUrl": "http://localhost:7000"
    }
  }
}
```

---

# AŞAMA 10: DOĞRULAMA VE TEST

## ADIM 10.1: BAĞLANTI TESTİ

### 10.1.1 PostgreSQL Bağlantı Testi

**Test Script:** `scripts/test-connection.ps1`

```powershell
Write-Host "PostgreSQL Bağlantı Testi" -ForegroundColor Cyan
Write-Host "=" * 50

$env:PGPASSWORD = "Kamatek2024!"

# Connection test
Write-Host "`n1. Connection Test..." -ForegroundColor Yellow
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" `
    -h localhost `
    -p 5432 `
    -U kamatek_admin `
    -d kamatekcrm `
    -c "SELECT version();"

if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✓ Bağlantı başarılı!" -ForegroundColor Green
} else {
    Write-Host "   ✗ Bağlantı başarısız!" -ForegroundColor Red
    exit 1
}

# Table count
Write-Host "`n2. Tablo Sayısı..." -ForegroundColor Yellow
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" `
    -h localhost `
    -p 5432 `
    -U kamatek_admin `
    -d kamatekcrm `
    -c "SELECT count(*) as table_count FROM information_schema.tables WHERE table_schema='public';"

# Sample data count
Write-Host "`n3. Örnek Veri Sayıları..." -ForegroundColor Yellow
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" `
    -h localhost `
    -p 5432 `
    -U kamatek_admin `
    -d kamatekcrm `
    -c "
        SELECT 'Customers' as table_name, count(*) as count FROM customers UNION ALL
        SELECT 'Users', count(*) FROM users UNION ALL
        SELECT 'Products', count(*) FROM products UNION ALL
        SELECT 'ServiceJobs', count(*) FROM service_jobs;
    "

Write-Host "`n" + "=" * 50
Write-Host "Test tamamlandı!" -F