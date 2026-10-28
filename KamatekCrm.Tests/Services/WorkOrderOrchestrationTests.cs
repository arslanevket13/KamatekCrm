using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Services;

/// <summary>
/// İş Emri orkestrasyonu, aşama geçişleri, idempotent kapanış (stok/cari) ve
/// teknisyen çakışma tespiti için birim testleri.
/// </summary>
public sealed class WorkOrderOrchestrationTests
{
    [Fact]
    public async Task StagePromotionPipeline_FullCycle_ShouldSucceedAndRegisterCustomerAsset()
    {
        var (commandService, readService, factory, customerId) = CreateServices(stockQuantity: 20);

        // 1. Keşif Kaydı Oluştur
        var saveResult = await commandService.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));
        saveResult.IsSuccess.Should().BeTrue();
        int jobId = saveResult.Value!.JobId;

        // Keşif Raporunu Kaydet (1 adet Seri Numaralı Ürün)
        await commandService.SaveDiscoveryAsync(new SaveDiscoveryRequest(
            jobId,
            "Teknik inceleme yapıldı.",
            "CCTV montajı önerildi.",
            2.5,
            "Ahmet Teknisyen",
            [],
            [new DiscoveryMaterialInput(null, 1, "IP Kamera", 2, "SN: CAM-999888")],
            [],
            "test-user"));

        // 2. Keşiften Teklife Dönüştür
        var convertResult = await commandService.ConvertToQuoteAsync(jobId, "test-user");
        convertResult.IsSuccess.Should().BeTrue();

        var revisions = await readService.GetQuotationRevisionsAsync(jobId);
        revisions.IsSuccess.Should().BeTrue();
        revisions.Value.Should().NotBeEmpty();
        int quoteId = revisions.Value![0].Id;

        // Teklifi Gönder ve Kabul Et
        await commandService.SendQuotationAsync(quoteId, "test-user");
        var acceptResult = await commandService.AcceptQuotationAsync(quoteId, "test-user");
        acceptResult.IsSuccess.Should().BeTrue();

        // 3. Tekliften Kuruluma Aktarım (Montaj Planla)
        var planResult = await commandService.PlanInstallationAsync(new PlanInstallationRequest(
            jobId,
            null,
            "Ahmet Teknisyen",
            DateTime.UtcNow.AddDays(1),
            "Kurulum planlandı",
            "test-user"));
        planResult.IsSuccess.Should().BeTrue();

        // Montajı Tamamla (2 adet kamera kullanıldı, seri no notta)
        await commandService.SaveInstallationAsync(new SaveInstallationRequest(
            jobId,
            null,
            "Ahmet Teknisyen",
            DateTime.UtcNow,
            "Montaj yapıldı",
            3.0m,
            [new InstallationMaterialInput(null, 1, "IP Kamera", 2m, 1200m, "SN: CAM-999888")],
            [],
            "test-user"));

        var completeInstallResult = await commandService.CompleteInstallationAsync(new CompleteInstallationRequest(
            jobId,
            "Kamera kuruldu ve devreye alındı.",
            "Ahmet Teknisyen",
            "İmza Alındı",
            3.0m,
            "test-user"));
        completeInstallResult.IsSuccess.Should().BeTrue();
        completeInstallResult.Value!.CurrentStatus.Should().Be(JobStatus.InstallationCompleted);

        // 4. Teslim Et (Delivery)
        var deliveryResult = await commandService.CompleteDeliveryAsync(new CompleteDeliveryRequest(
            jobId,
            "Ahmet Teknisyen",
            "Müşteriye eksiksiz teslim edildi.",
            "Müşteri İmzası",
            PaymentStatus.Paid,
            PaymentMethod.Cash,
            2400m,
            "FAT-2026-001",
            "test-user"));

        deliveryResult.IsSuccess.Should().BeTrue();
        deliveryResult.Value!.CurrentStatus.Should().Be(JobStatus.Delivered);

        // Doğrulama: Müşteri Demirbaşı (Asset) teslim tarihi ile otomatik oluşturuldu mu?
        using var checkContext = await factory.CreateDbContextAsync();
        var asset = await checkContext.CustomerAssets
            .FirstOrDefaultAsync(a => a.CustomerId == customerId && a.SerialNumber == "CAM-999888");

        asset.Should().NotBeNull();
        asset!.Model.Should().Be("IP Kamera");
        asset.Status.Should().Be(AssetStatus.Active);
    }

    [Fact]
    public async Task IdempotentClosing_ShouldNotDeductStockOrDebitLedgerTwice()
    {
        var (commandService, readService, factory, customerId) = CreateServices(stockQuantity: 10);

        // İşi montaj tamamlandı aşamasına getir
        var saveResult = await commandService.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));
        int jobId = saveResult.Value!.JobId;

        await commandService.SaveDiscoveryAsync(new SaveDiscoveryRequest(
            jobId, "Not", "Çözüm", 1.0, "Teknisyen", [],
            [new DiscoveryMaterialInput(null, 1, "IP Kamera", 2, null)], [], "test-user"));

        var convertResult = await commandService.ConvertToQuoteAsync(jobId, "test-user");
        var revisions = await readService.GetQuotationRevisionsAsync(jobId);
        int quoteId = revisions.Value![0].Id;

        await commandService.SendQuotationAsync(quoteId, "test-user");
        await commandService.AcceptQuotationAsync(quoteId, "test-user");

        await commandService.PlanInstallationAsync(new PlanInstallationRequest(
            jobId, null, "Teknisyen", null, null, "test-user"));

        await commandService.SaveInstallationAsync(new SaveInstallationRequest(
            jobId, null, "Teknisyen", null, null, 2m,
            [new InstallationMaterialInput(null, 1, "IP Kamera", 2m, 500m, null)], [], "test-user"));

        await commandService.CompleteInstallationAsync(new CompleteInstallationRequest(
            jobId, "Tamam", "Teknisyen", null, 2m, "test-user"));

        // 1. Kapanış: Teslim Et (Stok düşülür: 10 - 2 = 8, Cari borçlandırılır)
        var delivery1 = await commandService.CompleteDeliveryAsync(new CompleteDeliveryRequest(
            jobId, "Teknisyen", "Teslim 1", null,
            PaymentStatus.Unpaid, PaymentMethod.Cash, 0m, null, "test-user"));
        delivery1.IsSuccess.Should().BeTrue();

        using (var ctx1 = await factory.CreateDbContextAsync())
        {
            var prod1 = await ctx1.Products.FirstAsync(p => p.Id == 1);
            prod1.TotalStockQuantity.Should().Be(8);

            var job1 = await ctx1.ServiceJobs.FirstAsync(j => j.Id == jobId);
            job1.IsStockDeducted.Should().BeTrue();

            var debitTransactions = await ctx1.Transactions
                .Where(t => t.ReconciliationKey == $"JOB_DEBIT_{jobId}")
                .ToListAsync();
            debitTransactions.Should().HaveCount(1);
        }

        // 2. Kapanış: Aynı iş emri ikinci kez teslimat veya durum güncellemesi çağrıldığında
        var delivery2 = await commandService.CompleteDeliveryAsync(new CompleteDeliveryRequest(
            jobId, "Teknisyen", "Teslim 2 (güncelleme)", null,
            PaymentStatus.Paid, PaymentMethod.Cash, 1000m, null, "test-user"));
        delivery2.IsSuccess.Should().BeTrue();

        using (var ctx2 = await factory.CreateDbContextAsync())
        {
            var prod2 = await ctx2.Products.FirstAsync(p => p.Id == 1);
            // Stok MÜKERRER DÜŞMEMELİ (hala 8 olmalı, 6 değil!)
            prod2.TotalStockQuantity.Should().Be(8);

            // Müşteri borçlandırma kaydı tekil kalmalı
            var debitTransactions2 = await ctx2.Transactions
                .Where(t => t.ReconciliationKey == $"JOB_DEBIT_{jobId}")
                .ToListAsync();
            debitTransactions2.Should().HaveCount(1);
        }
    }

    [Fact]
    public async Task TechnicianConflict_Detection_ShouldIdentifyOverlappingSchedule()
    {
        var (_, readService, factory, customerId) = CreateServices(stockQuantity: 10);

        DateTime baseTime = new DateTime(2026, 11, 15, 14, 0, 0, DateTimeKind.Utc);
        int techId = 42;

        using (var seed = await factory.CreateDbContextAsync())
        {
            var activeJob = new ServiceJob
            {
                CustomerId = customerId,
                Title = "Mevcut Randevulu İş",
                Description = "Kamera bakım",
                Status = JobStatus.InProgress,
                AssignedTechnicianId = techId,
                AssignedTechnician = "Mehmet Usta",
                ScheduledDate = baseTime,
                CreatedDate = DateTime.UtcNow
            };
            seed.ServiceJobs.Add(activeJob);
            await seed.SaveChangesAsync();
        }

        // 1. Aynı teknisyene +1 saat sonra (15:00) iş verilmeye çalışıldığında (+-2 saat penceresinde) çakışmalı
        var conflictCheck1 = await readService.CheckTechnicianConflictAsync(
            techId, baseTime.AddHours(1));
        conflictCheck1.IsSuccess.Should().BeTrue();
        conflictCheck1.Value.Should().HaveCount(1);
        conflictCheck1.Value![0].AssignedTechnicianId.Should().Be(techId);

        // 2. Farklı bir zaman (+5 saat sonra, 19:00) çakışma OLMAMALI
        var conflictCheck2 = await readService.CheckTechnicianConflictAsync(
            techId, baseTime.AddHours(5));
        conflictCheck2.IsSuccess.Should().BeTrue();
        conflictCheck2.Value.Should().BeEmpty();

        // 3. Farklı bir teknisyen için çakışma OLMAMALI
        var conflictCheck3 = await readService.CheckTechnicianConflictAsync(
            999, baseTime);
        conflictCheck3.IsSuccess.Should().BeTrue();
        conflictCheck3.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task MaterialReadiness_ShouldGenerateWarning_WhenStockIsInsufficient()
    {
        // 1 adet stok ile başlat
        var (commandService, readService, factory, customerId) = CreateServices(stockQuantity: 1);

        var save = await commandService.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId), [], false, "test-user"));
        int jobId = save.Value!.JobId;

        // 5 adet malzeme gereksinimi gir
        await commandService.SaveDiscoveryAsync(new SaveDiscoveryRequest(
            jobId, "Keşif", "Çözüm", 1.0, "Teknisyen", [],
            [new DiscoveryMaterialInput(null, 1, "IP Kamera", 5, null)],
            [], "test-user"));

        // Workspace projeksiyonunu oku
        var wsResult = await readService.GetWorkspaceAsync(jobId);
        wsResult.IsSuccess.Should().BeTrue();

        // Malzeme yetersiz stok uyarısı bulunmalı
        var shortageWarning = wsResult.Value!.Warnings
            .FirstOrDefault(w => w.Code == "MaterialStockShortage");

        shortageWarning.Should().NotBeNull();
        shortageWarning!.Message.Should().Contain("Yetersiz Stok");
        shortageWarning.Message.Should().Contain("IP Kamera");
    }

    private static (ServiceJobCommandService Command, ServiceJobReadService Read, IDbContextFactory<AppDbContext> Factory, int CustomerId)
        CreateServices(int stockQuantity = 20)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(item => item.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AppDbContext(options));

        int customerId;
        using (var seed = new AppDbContext(options))
        {
            var customer = new Customer
            {
                FullName = "Orkestrasyon Müşterisi",
                CustomerCode = "ORK-001",
                PhoneNumber = "0532 999 88 77"
            };
            seed.Customers.Add(customer);
            seed.Products.Add(new Product
            {
                Id = 1,
                ProductName = "IP Kamera",
                SKU = "CAM-IP-01",
                SalePrice = 1200m,
                PurchasePrice = 800m,
                VatRate = 20,
                TotalStockQuantity = stockQuantity
            });
            seed.Inventories.Add(new Inventory { ProductId = 1, WarehouseId = 1, Quantity = stockQuantity });
            seed.SaveChanges();
            customerId = customer.Id;
        }

        var auth = new TestAuthorizationService();
        var statusPolicy = new ServiceJobStatusPolicy();
        var nextActionResolver = new WorkOrderNextActionResolver();

        var commandService = new ServiceJobCommandService(
            factory.Object,
            statusPolicy,
            auth);

        var readService = new ServiceJobReadService(
            factory.Object,
            auth,
            nextActionResolver);

        return (commandService, readService, factory.Object, customerId);
    }

    private static ServiceJob NewDiscoveryJob(int customerId) => new()
    {
        CustomerId = customerId,
        Description = "Entegre Güvenlik Keşfi",
        JobCategory = JobCategory.CCTV,
        WorkOrderType = WorkOrderType.Discovery,
        Status = JobStatus.DiscoveryRequest,
        DiscoveryTechnicalNotes = "Saha tespit notu",
        TechnicianNotes = "Teknik çözüm",
        AssignedTechnician = "Ahmet Teknisyen",
        CreatedDate = DateTime.UtcNow
    };
}
