using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Enums;
using System.Security.Cryptography;
using System.Text;

namespace KamatekCrm.API.Data
{
    public static class ApiDbSeeder
    {
        public static void Seed(ApiDbContext context)
        {
            // Ensure database is created
            context.Database.EnsureCreated();

            // Check if data already exists
            if (context.Users.Any())
                return;

            // Create test technician user (Using Ad/Soyad, no Email field)
            var technician = new User
            {
                Id = 1,
                Username = "teknisyen",
                PasswordHash = HashPassword("123456"),
                Ad = "Ahmet",
                Soyad = "Teknisyen",
                Role = "Technician",
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            // Create admin user
            var admin = new User
            {
                Id = 2,
                Username = "admin",
                PasswordHash = HashPassword("admin123"),
                Ad = "Sistem",
                Soyad = "Admin",
                Role = "Admin",
                IsActive = true,
                CreatedDate = DateTime.Now,
                CanViewFinance = true,
                CanViewAnalytics = true,
                CanDeleteRecords = true,
                CanApprovePurchase = true,
                CanAccessSettings = true
            };

            context.Users.AddRange(technician, admin);
            context.SaveChanges();

            // Create test customer
            var customer = new Customer
            {
                Id = 1,
                FullName = "Test Müşteri A.Ş.",
                PhoneNumber = "2121234567",
                Email = "info@testmusteri.com",
                City = "İstanbul",
                District = "Kadıköy",
                Street = "Atatürk Cad.",
                BuildingNo = "123",
                Type = CustomerType.Corporate,
                Latitude = 40.9906,
                Longitude = 29.0230
            };

            context.Customers.Add(customer);
            context.SaveChanges();

            // Create sample ServiceJobs assigned to technician
            var jobs = new List<ServiceJob>
            {
                new ServiceJob
                {
                    Id = 1,
                    CustomerId = 1,
                    AssignedUserId = 1,
                    Description = "Kamera sistemi arızası - 4 kamera görüntü vermiyor",
                    ServiceJobType = ServiceJobType.Fault,
                    JobCategory = JobCategory.CCTV,
                    Status = JobStatus.Pending,
                    Priority = JobPriority.Urgent,
                    ScheduledDate = DateTime.Now.AddDays(1),
                    CreatedDate = DateTime.Now,
                    Price = 1500,
                    LaborCost = 500
                },
                new ServiceJob
                {
                    Id = 2,
                    CustomerId = 1,
                    AssignedUserId = 1,
                    Description = "Yangın alarm paneli bakımı",
                    ServiceJobType = ServiceJobType.Fault,
                    JobCategory = JobCategory.FireAlarm,
                    Status = JobStatus.InProgress,
                    Priority = JobPriority.Normal,
                    ScheduledDate = DateTime.Now,
                    CreatedDate = DateTime.Now.AddDays(-2),
                    Price = 800,
                    LaborCost = 300
                },
                new ServiceJob
                {
                    Id = 3,
                    CustomerId = 1,
                    AssignedUserId = 1,
                    Description = "Diafon sistemi montajı - 16 daire",
                    ServiceJobType = ServiceJobType.Project,
                    JobCategory = JobCategory.VideoIntercom,
                    Status = JobStatus.Pending,
                    Priority = JobPriority.Normal,
                    ScheduledDate = DateTime.Now.AddDays(3),
                    CreatedDate = DateTime.Now,
                    Price = 12000,
                    LaborCost = 3000
                }
            };

            context.ServiceJobs.AddRange(jobs);
            context.SaveChanges();
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
