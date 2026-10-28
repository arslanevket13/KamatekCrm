using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Repositories;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Helpers;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KamatekCrm.Tests
{
    /// <summary>
    /// Müşteri Modülü Kapsamlı Birim Testleri:
    /// - Telefon normalizasyonu
    /// - TCKN ve VKN checksum doğrulama algoritmaları
    /// - Cascading Adres ComboBox ve Türkçe kültür eşitliği
    /// - Mükerrer müşteri tespiti
    /// - Müşteri Birleştirme (MergeCustomersAsync)
    /// </summary>
    public class CustomerModuleTests
    {
        #region 1. Telefon Normalizasyon Testleri

        [Theory]
        [InlineData("+90 (532) 111-2233", "05321112233")]
        [InlineData("05321112233", "05321112233")]
        [InlineData("532 111 22 33", "05321112233")]
        [InlineData("0 (532) 111 22 33", "05321112233")]
        [InlineData("905321112233", "05321112233")]
        [InlineData("+905321112233", "05321112233")]
        [InlineData("00905321112233", "05321112233")]
        [InlineData("0212 444 0 333", "02124440333")]
        public void NormalizePhoneNumber_VariousFormats_ReturnsCanonical11Digit(string rawInput, string expectedCanonical)
        {
            var normalized = CustomerValidationHelper.NormalizePhoneNumber(rawInput);
            normalized.Should().Be(expectedCanonical);
        }

        [Theory]
        [InlineData("05321112233", true)]
        [InlineData("+90 (532) 111-2233", true)]
        [InlineData("532 111 2233", true)]
        [InlineData("02124440333", true)]
        [InlineData("123", false)]
        [InlineData("01234567890", false)] // İkinci hane 1 olamaz
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValidPhoneNumber_Validation_ReturnsExpected(string? input, bool expectedValid)
        {
            var isValid = CustomerValidationHelper.IsValidPhoneNumber(input);
            isValid.Should().Be(expectedValid);
        }

        #endregion

        #region 2. TCKN ve VKN Doğrulama Algoritma Testleri

        [Theory]
        [InlineData("10000000146", true)]
        [InlineData("01234567890", false)] // İlk hane 0 olamaz
        [InlineData("10000000147", false)] // Mod 10 tutmaz
        [InlineData("123456789", false)]   // 11 haneden kısa
        [InlineData("123456789012", false)]// 11 haneden uzun
        [InlineData("ABC12345678", false)] // Harf içerir
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValidTcKimlikNo_Algorithm_EvaluatedCorrectly(string? tc, bool expectedValid)
        {
            var isValid = CustomerValidationHelper.IsValidTcKimlikNo(tc);
            isValid.Should().Be(expectedValid);
        }

        [Theory]
        [InlineData("1234567890", true)]
        [InlineData("9876543217", true)]
        [InlineData("5555555553", true)]
        [InlineData("1234567899", false)] // Checksum tutmaz
        [InlineData("12345", false)]      // 10 haneden kısa
        [InlineData("ABC4567890", false)] // Harf içerir
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValidTaxNumber_Algorithm_EvaluatedCorrectly(string? vkn, bool expectedValid)
        {
            var isValid = CustomerValidationHelper.IsValidTaxNumber(vkn);
            isValid.Should().Be(expectedValid);
        }

        #endregion

        #region 3. Cascading Adres ve Türkçe Karakter Kültür Testleri

        [Theory]
        [InlineData("İSTANBUL", "istanbul", true)]
        [InlineData("Kadıköy", "KADIKÖY", true)]
        [InlineData("ŞİŞLİ", "şişli", true)]
        [InlineData("Isparta", "ISPARTA", true)]
        [InlineData("İzmir", "Ankara", false)]
        public void AddressService_TurkishEquals_MatchesRegardlessOfCaseAndTurkishLetters(string str1, string str2, bool expectedEqual)
        {
            var result = AddressService.TurkishEquals(str1, str2);
            result.Should().Be(expectedEqual);
        }

        [Fact]
        public void AddressService_FindCity_FindsCityCaseInsensitiveTurkish()
        {
            var city = AddressService.FindCity("iSTANBUL");
            city.Should().NotBeNull();
            city!.Name.Should().Be("İstanbul");
        }

        #endregion

        #region 4. Mükerrer Müşteri Tespiti Testleri

        [Fact]
        public async Task DuplicateCustomer_Detection_FindsExistingByNormalizedPhone()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            await using (var db = new AppDbContext(options))
            {
                db.Customers.Add(new Customer
                {
                    Id = 1,
                    FullName = "Ali Kaya",
                    PhoneNumber = "05321112233",
                    CustomerCode = "CUST-001",
                    IsDeleted = false
                });
                await db.SaveChangesAsync();
            }

            // Yeni girilen farklı formatta aynı numara
            string rawNewPhone = "+90 (532) 111-2233";
            string normNewPhone = CustomerValidationHelper.NormalizePhoneNumber(rawNewPhone);
            string phoneLast10 = normNewPhone.Length >= 10 ? normNewPhone.Substring(normNewPhone.Length - 10) : normNewPhone;

            await using (var db = new AppDbContext(options))
            {
                var duplicate = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => !c.IsDeleted && (
                    c.PhoneNumber == normNewPhone ||
                    (phoneLast10.Length >= 10 && c.PhoneNumber != null && c.PhoneNumber.Contains(phoneLast10))
                ));

                duplicate.Should().NotBeNull();
                duplicate!.FullName.Should().Be("Ali Kaya");
            }
        }

        [Fact]
        public async Task DuplicateCustomer_Detection_FindsExistingByTcOrTaxNumber()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            await using (var db = new AppDbContext(options))
            {
                db.Customers.Add(new Customer
                {
                    Id = 1,
                    FullName = "Mehmet Demir",
                    PhoneNumber = "05332223344",
                    TcKimlikNo = "10000000146",
                    CustomerCode = "CUST-002",
                    IsDeleted = false
                });
                db.Customers.Add(new Customer
                {
                    Id = 2,
                    FullName = "Kamatek Bilişim Ltd.",
                    PhoneNumber = "02123334455",
                    TaxNumber = "1234567890",
                    CustomerCode = "CUST-003",
                    IsDeleted = false
                });
                await db.SaveChangesAsync();
            }

            await using (var db = new AppDbContext(options))
            {
                var duplicateByTc = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => !c.IsDeleted && c.TcKimlikNo == "10000000146");
                duplicateByTc.Should().NotBeNull();
                duplicateByTc!.FullName.Should().Be("Mehmet Demir");

                var duplicateByTax = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => !c.IsDeleted && c.TaxNumber == "1234567890");
                duplicateByTax.Should().NotBeNull();
                duplicateByTax!.FullName.Should().Be("Kamatek Bilişim Ltd.");
            }
        }

        #endregion

        #region 5. Müşteri Birleştirme (MergeCustomersAsync) Testleri

        [Fact]
        public async Task MergeCustomersAsync_TransfersAllChildRecordsAndLedger_WithoutDataLoss()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var factoryMock = new Mock<IDbContextFactory<AppDbContext>>();
            factoryMock.Setup(f => f.CreateDbContext()).Returns(() => new AppDbContext(options));
            factoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new AppDbContext(options));

            var uow = new UnitOfWork(factoryMock.Object);
            var customerAppService = new CustomerAppService(uow);

            // Seed Source Customer (Id: 10) ve Target Customer (Id: 20)
            await using (var db = new AppDbContext(options))
            {
                var source = new Customer
                {
                    Id = 10,
                    FullName = "Kaynak Mükerrer Müşteri",
                    PhoneNumber = "05321110000",
                    CustomerCode = "CUST-SRC",
                    TotalSpent = 750m,
                    TotalPurchaseCount = 2,
                    LoyaltyPoints = 50,
                    IsDeleted = false
                };

                var target = new Customer
                {
                    Id = 20,
                    FullName = "Hedef Ana Müşteri",
                    PhoneNumber = "05321119999",
                    CustomerCode = "CUST-TGT",
                    TotalSpent = 1500m,
                    TotalPurchaseCount = 5,
                    LoyaltyPoints = 100,
                    Notes = "Mevcut notlar.",
                    IsDeleted = false
                };

                db.Customers.AddRange(source, target);

                // Alt kayıtlar (İş Emri, Cari Hareket, Satış Siparişi, Teklif, Cihaz, Etkileşim, Aktivite)
                db.ServiceJobs.Add(new ServiceJob
                {
                    Id = 101,
                    CustomerId = 10,
                    Description = "Kaynak İş Emri",
                    Price = 300m,
                    Status = JobStatus.Completed
                });

                db.Transactions.Add(new Transaction
                {
                    Id = 201,
                    CustomerId = 10,
                    Amount = 400m,
                    Type = TransactionType.Payment,
                    Description = "Kaynak Tahsilat"
                });

                db.SalesOrders.Add(new SalesOrder
                {
                    Id = 301,
                    CustomerId = 10,
                    OrderNumber = "SO-101",
                    TotalAmount = 500m,
                    Status = SalesOrderStatus.Completed
                });

                db.Quotes.Add(new Quote
                {
                    Id = 401,
                    CustomerId = 10,
                    QuoteNumber = "QT-101",
                    QuoteTitle = "Kaynak Teklif",
                    GrandTotal = 2500m,
                    Status = QuoteStatus.Approved
                });

                db.CustomerAssets.Add(new CustomerAsset
                {
                    Id = 501,
                    CustomerId = 10,
                    Brand = "Dahua",
                    Model = "Kamera",
                    SerialNumber = "DH-12345"
                });

                db.CustomerInteractions.Add(new CustomerInteraction
                {
                    Id = 601,
                    CustomerId = 10,
                    InteractionNumber = "INT-101",
                    Subject = "Teknik Talep",
                    Summary = "Kamera montajı görüşüldü."
                });

                db.CustomerActivities.Add(new CustomerActivity
                {
                    Id = 701,
                    CustomerId = 10,
                    Type = ActivityType.NoteAdded,
                    Description = "Kaynak Aktivite Notu"
                });

                await db.SaveChangesAsync();
            }

            // Act: Müşteri Birleştirme
            var result = await customerAppService.MergeCustomersAsync(10, 20);

            // Assert
            result.IsSuccess.Should().BeTrue();

            await using (var db = new AppDbContext(options))
            {
                var updatedSource = await db.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == 10);
                var updatedTarget = await db.Customers.FindAsync(20);

                // 1. Kaynak müşteri arşive alınmış (soft-deleted) olmalı
                updatedSource.Should().NotBeNull();
                updatedSource!.IsDeleted.Should().BeTrue();

                // 2. Hedef müşterinin harcama ve puanları toplanmış olmalı
                updatedTarget.Should().NotBeNull();
                updatedTarget!.TotalSpent.Should().Be(2250m); // 1500 + 750
                updatedTarget.TotalPurchaseCount.Should().Be(7); // 5 + 2
                updatedTarget.LoyaltyPoints.Should().Be(150);    // 100 + 50
                updatedTarget.Notes.Should().Contain("[BİRLEŞTİRME:");
                updatedTarget.Notes.Should().Contain("CUST-SRC");

                // 3. Tüm alt kayıtların CustomerId değeri 20'ye geçmiş olmalı
                var job = await db.ServiceJobs.FindAsync(101);
                job!.CustomerId.Should().Be(20);

                var transaction = await db.Transactions.FindAsync(201);
                transaction!.CustomerId.Should().Be(20);

                var order = await db.SalesOrders.FindAsync(301);
                order!.CustomerId.Should().Be(20);

                var quote = await db.Quotes.FindAsync(401);
                quote!.CustomerId.Should().Be(20);

                var asset = await db.CustomerAssets.FindAsync(501);
                asset!.CustomerId.Should().Be(20);

                var interaction = await db.CustomerInteractions.FindAsync(601);
                interaction!.CustomerId.Should().Be(20);

                var activity = await db.CustomerActivities.FindAsync(701);
                activity!.CustomerId.Should().Be(20);
            }
        }

        [Fact]
        public async Task MergeCustomersAsync_SameCustomer_ReturnsFailure()
        {
            var factoryMock = new Mock<IDbContextFactory<AppDbContext>>();
            var uowMock = new Mock<IUnitOfWork>();
            var customerAppService = new CustomerAppService(uowMock.Object);

            var result = await customerAppService.MergeCustomersAsync(5, 5);

            result.IsFailure.Should().BeTrue();
            result.Error.Should().Contain("aynı olamaz");
        }

        #endregion
    }
}
