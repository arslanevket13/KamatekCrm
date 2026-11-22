using System;
using System.Linq;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            if (!context.Warehouses.Any())
            {
                context.Warehouses.Add(new Warehouse
                {
                    Name = "Ana Depo",
                    Type = WarehouseType.MainWarehouse,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "System"
                });

                context.SaveChanges();
            }

            var adminUser = context.Users.FirstOrDefault(u => u.Username == "admin");
            if (adminUser == null)
            {
                adminUser = new User
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!", workFactor: 11),
                    Role = "Admin",
                    Ad = "System",
                    Soyad = "Admin",
                    IsActive = true,
                    MustChangePassword = true,
                    CanViewFinance = true,
                    CanViewAnalytics = true,
                    CanDeleteRecords = true,
                    CanApprovePurchase = true,
                    CanAccessSettings = true,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "System"
                };
                context.Users.Add(adminUser);
                context.SaveChanges();
            }
        }
    }
}
