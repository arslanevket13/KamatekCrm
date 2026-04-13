using System;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            // Seed a default warehouse if none exist
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

            // Seed a default admin user (password: 123)
            var adminUser = context.Users.FirstOrDefault(u => u.Username == "admin");
            if (adminUser == null)
            {
                adminUser = new User
                {
                    Username = "admin",
                    PasswordHash = HashPassword("123"),
                    Role = "Admin",
                    Ad = "System",
                    Soyad = "Admin",
                    IsActive = true,
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
            else
            {
                // Ensure the hash is correct in case an older version seeded a plain text or invalid hash
                var correctHash = HashPassword("123");
                
                // If it's a plain text or clearly not a 48 byte Base64 string, force update
                if (string.IsNullOrEmpty(adminUser.PasswordHash) || adminUser.PasswordHash.Length < 60) // 48 bytes base64 is 64 chars
                {
                    adminUser.PasswordHash = correctHash;
                    context.SaveChanges();
                }
            }
        }

        private static string HashPassword(string password)
        {
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32);
            byte[] hashBytes = new byte[48];
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 32);
            return Convert.ToBase64String(hashBytes);
        }
    }
}

