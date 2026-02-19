# KamatekCRM Database Guide

This document details the database architecture, schema management, and data integrity patterns used in KamatekCRM.

## 1. PostgreSQL Integration

The system has migrated from SQLite to **PostgreSQL** to support high concurrency and professional-grade data management.

### Technical Stack
*   **Provider**: `Npgsql.EntityFrameworkCore.PostgreSQL`
*   **JSON Support**: `JSONB` for schema-less technical specifications.
*   **Naming Convention**: Snake_case (PostgreSQL default) mapping via EF Core.

---

## 2. Advanced Data Features

### 2.1 JSONB Technical Specifications
To handle the diverse technical requirements of different products (e.g., camera resolution, cable length) without a rigid schema, we use the `TechSpecsJson` property mapped to a PostgreSQL `jsonb` column.

```csharp
// AppDbContext.cs
entity.Property(e => e.TechSpecsJson).HasColumnType("jsonb");
```

### 2.2 Soft Delete Pattern
Entities inheriting from `ISoftDeletable` are never physically removed from the database.
*   **Implementation**: `IsDeleted` (boolean) and `DeletedAt` (datetime) flags.
*   **Filter**: High-performance Global Query Filter applied in `OnModelCreating`.

```csharp
modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
```

### 2.3 Automated Audit Trail
The `AppDbContext` automatically populates audit fields for every save operation:
*   **CreatedDate / CreatedBy**: Set only on insert.
*   **ModifiedDate / ModifiedBy**: Updated on every change.
*   **Npgsql Compatibility**: All timestamps are strictly enforced as **UTC** to prevent `Kind=Local` errors.

---

## 3. Schema Management

### Migrations
The schema is managed using **EF Core Migrations**.
*   **Creation**: `dotnet ef migrations add [Name]`
*   **Execution**: `dotnet ef database update`

### Connection Management
Connection strings are managed via `appsettings.json` and resolved at runtime through `AppSettings.cs`.

---

## 4. Concurrency Control
The system uses **Optimistic Concurrency** where necessary to prevent data loss during simultaneous edits. Key entities may include a `Version` or `RowVersion` field handled during `SaveChanges`.
