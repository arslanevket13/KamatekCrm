using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using System;
using System.Linq;

namespace KamatekCrm.Data
{
    public static class DbSeeder
    {
        public static void SeedDemoData(AppDbContext context)
        {
            // Eğer müşteri varsa veritabanı dolu demektir, çık.
            if (context.Customers.Any()) return;

            // 1. Müşteriler
            var customer1 = new Customer
            {
                FullName = "Ahmet Yılmaz",
                Type = CustomerType.Individual,
                CustomerCode = "M-001",
                PhoneNumber = "0555 111 22 33",
                Email = "ahmet.yilmaz@gmail.com",
                City = "İstanbul",
                District = "Kadıköy",
                Neighborhood = "Caferağa Mah.",
                Street = "Moda Cd.",
                BuildingNo = "10",
                ApartmentNo = "5",
                TcKimlikNo = "11111111111",
                Notes = "Sadık müşteri. Ödemeleri düzenli."
            };

            var customer2 = new Customer
            {
                FullName = "Gökdelen İnşaat A.Ş.",
                Type = CustomerType.Corporate,
                CustomerCode = "C-001",
                PhoneNumber = "0212 333 44 55",
                Email = "info@gokdeleninsaat.com.tr",
                City = "İstanbul",
                District = "Ataşehir",
                Neighborhood = "Barbaros Mah.",
                Street = "Halk Cd.",
                BuildingNo = "25",
                CompanyName = "Gökdelen İnşaat San. ve Tic. A.Ş.",
                TaxOffice = "Ataşehir",
                TaxNumber = "1234567890",
                Notes = "Büyük projeli inşaat firması. 2 Şantiyesi var."
            };

            context.Customers.AddRange(customer1, customer2);
            context.SaveChanges();

            // 2. Transaksiyonlar (Ahmet Yılmaz borçlu olsun)
            var transaction1 = new Transaction
            {
                CustomerId = customer1.Id,
                Date = DateTime.Now.AddDays(-10),
                Amount = 5000,
                Type = TransactionType.Debt,
                Description = "Geçmiş dönem bakiyesi devri"
            };
            
            var transaction2 = new Transaction
            {
                CustomerId = customer1.Id,
                Date = DateTime.Now.AddDays(-5),
                Amount = 1500,
                Type = TransactionType.Payment,
                Description = "Nakit ödeme"
            };

            context.Transactions.AddRange(transaction1, transaction2);
            context.SaveChanges();

            // 3. Ürünler
            var product1 = new Product
            {
                ProductName = "Hikvision 4MP IP Dome Kamera",
                SKU = "HIK-IP-4MP",
                Barcode = "869000000001",
                Unit = "Adet",
                PurchasePrice = 850,
                SalePrice = 1200,
                ProductCategoryType = ProductCategoryType.Camera,
                MinStockLevel = 5,
                TotalStockQuantity = 15
            };

            var product2 = new Product
            {
                ProductName = "Audio 7\" Diafon Şubesi (Dokunmatik)",
                SKU = "AUD-7-TOUCH",
                Barcode = "869000000002",
                Unit = "Adet",
                PurchasePrice = 1800,
                SalePrice = 2500,
                ProductCategoryType = ProductCategoryType.Intercom,
                MinStockLevel = 10,
                TotalStockQuantity = 3 // Kritik stok örneği için düşük
            };

            var product3 = new Product
            {
                ProductName = "Reçber Cat6 Network Kablosu (Halogen Free)",
                SKU = "CAT6-HFFR",
                Barcode = "869000000003",
                Unit = "Metre",
                PurchasePrice = 10,
                SalePrice = 15,
                ProductCategoryType = ProductCategoryType.Cable,
                MinStockLevel = 300,
                TotalStockQuantity = 500
            };

            context.Products.AddRange(product1, product2, product3);
            context.SaveChanges();

            // 4. Envanter (Merkez Depo - ID: 1)
            // Warehouse ID 1 (Merkez Depo) AppDbContext seed data içinde oluşuyor.
            
            var inventory1 = new Inventory { ProductId = product1.Id, WarehouseId = 1, Quantity = 15 };
            var inventory2 = new Inventory { ProductId = product2.Id, WarehouseId = 1, Quantity = 3 };
            var inventory3 = new Inventory { ProductId = product3.Id, WarehouseId = 1, Quantity = 500 };

            context.Inventories.AddRange(inventory1, inventory2, inventory3);
            context.SaveChanges();

            // 5. Servis İşleri
            var job1 = new ServiceJob
            {
                CustomerId = customer1.Id,
                JobCategory = JobCategory.SmartHome, // Telefon ekran değişimi kategorisi yoksa en yakını veya Other
                WorkOrderType = WorkOrderType.Repair,
                Status = JobStatus.WaitingForParts,
                Priority = JobPriority.Normal,
                Description = "Samsung S21 Ekran Değişimi. Yedek parça siparişi verildi.",
                CreatedDate = DateTime.Now.AddDays(-2),
                DeviceBrand = "Samsung",
                DeviceModel = "S21",
                Price = 3500,
                LaborCost = 500
            };

            var job2 = new ServiceJob
            {
                CustomerId = customer2.Id,
                JobCategory = JobCategory.CCTV,
                WorkOrderType = WorkOrderType.Inspection,
                Status = JobStatus.Pending,
                Priority = JobPriority.Urgent,
                Description = "Şantiye A Blok kamera sistemi keşfi yapılacak. Toplam 32 kamera planlanıyor.",
                CreatedDate = DateTime.Now,
                ScheduledDate = DateTime.Now.AddDays(1)
            };

            context.ServiceJobs.AddRange(job1, job2);
            context.SaveChanges();
        }
    }
}
