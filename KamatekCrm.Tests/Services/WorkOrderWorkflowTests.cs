using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Services;

/// <summary>
/// İş emri iş akışı testleri: Keşif → Teklif → Montaj. Her aşama ayrı varlıkta saklanır
/// ve bir sonraki aşamaya veri kopyalanır; kaynak kayıtlar değişmeden kalır.
/// </summary>
public sealed class WorkOrderWorkflowTests
{
    [Fact]
    public async Task ConvertToQuoteAsync_CopiesDiscoveryMaterialsToQuotationItems()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", quantity: 3, productId: 1), NewItem("Kablo", quantity: 50, productId: 2)],
            false,
            "test-user"));

        var result = await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        result.IsSuccess.Should().BeTrue(result.Error);
        await using var verify = await factory.CreateDbContextAsync();

        var job = await verify.ServiceJobs.SingleAsync();
        job.Status.Should().Be(JobStatus.ConvertedToQuote);
        job.IsConvertedToQuote.Should().BeTrue();

        var discovery = await verify.DiscoveryReports
            .Include(d => d.Materials)
            .SingleAsync(d => d.ServiceJobId == job.Id);
        discovery.TechnicalNotes.Should().Be("Sahada tespit notu");
        discovery.RecommendedSolution.Should().Be("Önerilen çözüm notu");
        discovery.Materials.Should().HaveCount(2);
        discovery.Materials.Should().Contain(m => m.ProductName == "Kamera" && m.Quantity == 3);
        discovery.Materials.Should().Contain(m => m.ProductName == "Kablo" && m.Quantity == 50);

        var quotation = await verify.WorkOrderQuotations
            .Include(q => q.Items)
            .SingleAsync(q => q.ServiceJobId == job.Id);
        quotation.Status.Should().Be(QuotationStatus.Draft);
        quotation.Items.Should().HaveCount(2);
        quotation.Items.Should().Contain(i => i.ProductName == "Kamera" && i.Quantity == 3 && i.ProductId == 1);
        quotation.Items.Should().Contain(i => i.ProductName == "Kablo" && i.Quantity == 50);

        // Kaynak iş malzemeleri değişmeden kalmalı (kopyalama, taşıma değil)
        var originalItems = await verify.ServiceJobItems.Where(i => i.ServiceJobId == job.Id).ToListAsync();
        originalItems.Should().HaveCount(2);
        originalItems.Should().Contain(i => i.QuantityUsed == 3);
        originalItems.Should().Contain(i => i.QuantityUsed == 50);
    }

    [Fact]
    public async Task ConvertToQuoteAsync_RejectsSecondConversionWithoutCreatingDuplicate()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 2, 1)],
            false,
            "test-user"));

        var first = await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");
        var second = await service.ConvertToQuoteAsync(save.Value.JobId, "test-user");

        first.IsSuccess.Should().BeTrue();
        second.IsFailure.Should().BeTrue();
        second.Error.Should().Contain("zaten teklife dönüştürülmüş");

        await using var verify = await factory.CreateDbContextAsync();
        (await verify.WorkOrderQuotations.CountAsync()).Should().Be(1, "aynı iş emri için teklif ikinci kez oluşturulmamalı");
        (await verify.DiscoveryReports.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ChangeStatusAsync_ToInstallationPlanned_RejectsWithoutAcceptedQuote()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        var result = await service.ChangeStatusAsync(
            save.Value!.JobId, JobStatus.InstallationPlanned, "test-user");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("kabul edilmiş teklif");

        await using var verify = await factory.CreateDbContextAsync();
        (await verify.ServiceJobs.SingleAsync()).Status.Should().Be(JobStatus.ConvertedToQuote);
        (await verify.InstallationOrders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PlanInstallationAsync_CopiesQuotationItemsToInstallationMaterials()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 2, 1)],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        int quotationId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            quotationId = (await ctx.WorkOrderQuotations.SingleAsync()).Id;
        }
        await service.AcceptQuotationAsync(quotationId, "test-user");

        var plan = await service.PlanInstallationAsync(new PlanInstallationRequest(
            save.Value!.JobId,
            TechnicianId: null,
            TechnicianName: "Ali Usta",
            InstallationDate: new DateTime(2026, 8, 10),
            Notes: "Kablolama güzergahı hazır.",
            ChangedBy: "test-user"));

        plan.IsSuccess.Should().BeTrue(plan.Error);
        await using var verify = await factory.CreateDbContextAsync();

        var job = await verify.ServiceJobs.SingleAsync();
        job.Status.Should().Be(JobStatus.InstallationPlanned);
        job.AssignedTechnician.Should().Be("Ali Usta");

        var installation = await verify.InstallationOrders
            .Include(i => i.Materials)
            .Include(i => i.Tasks)
            .SingleAsync(i => i.ServiceJobId == job.Id);
        installation.QuotationId.Should().Be(quotationId);
        installation.TechnicianName.Should().Be("Ali Usta");
        installation.InstallationDate.Should().Be(new DateTime(2026, 8, 10));
        installation.Notes.Should().Be("Kablolama güzergahı hazır.");
        installation.Materials.Should().ContainSingle(m => m.ProductName == "Kamera" && m.Quantity == 2);
        installation.Tasks.Should().HaveCount(3);

        // Teklif kalemleri değişmeden kalmalı
        var quotationItems = await verify.QuotationItems.Where(i => i.QuotationId == quotationId).ToListAsync();
        quotationItems.Should().ContainSingle(i => i.Quantity == 2);
    }

    [Fact]
    public async Task PlanInstallationAsync_RejectsWithoutAcceptedQuote()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        var result = await service.PlanInstallationAsync(new PlanInstallationRequest(
            save.Value!.JobId, null, null, null, null, "test-user"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("kabul edilmiş teklif");
    }

    [Fact]
    public async Task CompleteInstallationAsync_RecordsCompletionAndDeductsStock()
    {
        var (service, factory, customerId) = CreateService(stockQuantity: 10);
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 2, 1)],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        int quotationId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            quotationId = (await ctx.WorkOrderQuotations.SingleAsync()).Id;
        }
        await service.AcceptQuotationAsync(quotationId, "test-user");
        (await service.PlanInstallationAsync(new PlanInstallationRequest(
            save.Value!.JobId, null, "Ali Usta", null, null, "test-user"))).IsSuccess.Should().BeTrue();

        var result = await service.CompleteInstallationAsync(new CompleteInstallationRequest(
            save.Value!.JobId,
            DeliveryNote: "Cihaz test edilerek teslim edildi.",
            CompletionTechnician: "Ali Usta",
            CustomerSignature: null,
            ChangedBy: "test-user"));

        result.IsSuccess.Should().BeTrue(result.Error);
        await using var verify = await factory.CreateDbContextAsync();

        var job = await verify.ServiceJobs.SingleAsync();
        job.Status.Should().Be(JobStatus.InstallationCompleted);
        job.CompletedDate.Should().NotBeNull();
        job.IsStockDeducted.Should().BeTrue();
        job.RepairStatus.Should().Be(RepairStatus.Delivered);
        (await verify.Inventories.SingleAsync()).Quantity.Should().Be(8, "montaj tamamlanınca rezerve stok tüketilir");

        var installation = await verify.InstallationOrders.SingleAsync();
        installation.CompletedAt.Should().NotBeNull();
        installation.DeliveryNote.Should().Be("Cihaz test edilerek teslim edildi.");
        installation.CompletionTechnician.Should().Be("Ali Usta");
        (await verify.CustomerActivities.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CompleteInstallationAsync_RejectsWithoutPlannedInstallation()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        var result = await service.CompleteInstallationAsync(new CompleteInstallationRequest(
            save.Value!.JobId, null, null, null, "test-user"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("montaj planlanmalı");
    }

    [Fact]
    public async Task UpdateQuotationAsync_ComputesTotalsAndPersistsTerms()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 2, 1)],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        int quotationId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            quotationId = (await ctx.WorkOrderQuotations.SingleAsync()).Id;
        }

        var update = await service.UpdateQuotationAsync(new UpdateWorkOrderQuotationRequest(
            quotationId,
            Description: "Kamera kurulumu dahil",
            Warranty: "2 yıl",
            DeliveryTime: "7 iş günü",
            PaymentTerms: "Peşin / 30 gün",
            LaborCost: 500m,
            ShippingCost: 100m,
            DiscountAmount: 50m,
            TaxRate: 20m,
            Items:
            [
                new QuotationItemInput(null, 1, "Kamera", 2, 1000m, 10m, 20m),
                new QuotationItemInput(null, null, "Kablo", 50, 10m, 0m, 20m)
            ]));

        update.IsSuccess.Should().BeTrue(update.Error);

        // Malzeme ara toplamı: 2*1000*0.9 + 50*10 = 1800 + 500 = 2300
        // İskonto 50 → net 2250; KDV %20 → 450; toplam 2700
        update.Value!.TotalAmount.Should().Be(2700m);

        await using var verify = await factory.CreateDbContextAsync();
        var quote = await verify.WorkOrderQuotations.Include(q => q.Items).SingleAsync(q => q.Id == quotationId);
        quote.Warranty.Should().Be("2 yıl");
        quote.PaymentTerms.Should().Be("Peşin / 30 gün");
        quote.TotalAmount.Should().Be(2700m);
        quote.TaxAmount.Should().Be(450m);
        quote.Items.Should().HaveCount(2);
        quote.Items.Single(i => i.ProductName == "Kamera").UnitPrice.Should().Be(1000m);
    }

    [Fact]
    public async Task AcceptAndRejectQuotation_UpdatesStatus()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        int quotationId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            quotationId = (await ctx.WorkOrderQuotations.SingleAsync()).Id;
        }

        var accepted = await service.AcceptQuotationAsync(quotationId, "test-user");
        var repeated = await service.AcceptQuotationAsync(quotationId, "test-user");

        accepted.IsSuccess.Should().BeTrue();
        repeated.IsSuccess.Should().BeTrue("kabul idempotent olmalı");
        await using var verify = await factory.CreateDbContextAsync();
        (await verify.WorkOrderQuotations.SingleAsync()).Status.Should().Be(QuotationStatus.Accepted);

        var rejected = await service.RejectQuotationAsync(quotationId, "Fiyat uygun bulunmadı.", "test-user");
        rejected.IsFailure.Should().BeTrue("kabul edilmiş teklif reddedilemez");
    }

    [Fact]
    public async Task RejectQuotationAsync_StoresReason()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        int quotationId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            quotationId = (await ctx.WorkOrderQuotations.SingleAsync()).Id;
        }

        var result = await service.RejectQuotationAsync(quotationId, "Rakip daha uygun.", "test-user");

        result.IsSuccess.Should().BeTrue();
        await using var verify = await factory.CreateDbContextAsync();
        var quote = await verify.WorkOrderQuotations.SingleAsync();
        quote.Status.Should().Be(QuotationStatus.Rejected);
        quote.RejectionReason.Should().Be("Rakip daha uygun.");
        quote.RejectedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetWorkOrderWorkflowAsync_ReturnsCompleteAggregate()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 2, 1)],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        int quotationId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            quotationId = (await ctx.WorkOrderQuotations.SingleAsync()).Id;
        }
        await service.AcceptQuotationAsync(quotationId, "test-user");
        await service.PlanInstallationAsync(new PlanInstallationRequest(
            save.Value!.JobId, null, "Ali Usta", null, null, "test-user"));

        var readService = new ServiceJobReadService(factory, new TestAuthorizationService());
        var workflow = await readService.GetWorkOrderWorkflowAsync(save.Value!.JobId);

        workflow.IsSuccess.Should().BeTrue(workflow.Error);
        workflow.Value!.JobStatus.Should().Be(JobStatus.InstallationPlanned);
        workflow.Value.Discovery.Should().NotBeNull();
        workflow.Value.Discovery!.Materials.Should().ContainSingle(m => m.ProductName == "Kamera");
        workflow.Value.Quotation.Should().NotBeNull();
        workflow.Value.Quotation!.Status.Should().Be(QuotationStatus.Accepted);
        workflow.Value.Quotation.Items.Should().ContainSingle(i => i.ProductName == "Kamera");
        workflow.Value.Installation.Should().NotBeNull();
        workflow.Value.Installation!.Materials.Should().ContainSingle(m => m.ProductName == "Kamera");
        workflow.Value.Installation.Tasks.Should().HaveCount(3);
    }

    // ───────────────────────────── Fixture ─────────────────────────────

    private static (ServiceJobCommandService Service, IDbContextFactory<AppDbContext> Factory, int CustomerId)
        CreateService(int stockQuantity = 10)
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
                FullName = "Test Müşteri",
                CustomerCode = "TEST-001",
                PhoneNumber = "0532 111 22 33"
            };
            seed.Customers.Add(customer);
            seed.Products.Add(new Product
            {
                Id = 1,
                ProductName = "Test Kamera",
                SKU = "CAM-001",
                TotalStockQuantity = stockQuantity
            });
            seed.Products.Add(new Product
            {
                Id = 2,
                ProductName = "Kablo",
                SKU = "CBL-001",
                TotalStockQuantity = 1000
            });
            seed.Inventories.Add(new Inventory { ProductId = 1, WarehouseId = 1, Quantity = stockQuantity });
            seed.SaveChanges();
            customerId = customer.Id;
        }

        var service = new ServiceJobCommandService(
            factory.Object,
            new KamatekCrm.ApplicationCore.Services.ServiceJobStatusPolicy(),
            new TestAuthorizationService());
        return (service, factory.Object, customerId);
    }

    private static ServiceJob NewDiscoveryJob(int customerId) => new()
    {
        CustomerId = customerId,
        Description = "Kamera sistemi keşfi",
        JobCategory = JobCategory.CCTV,
        WorkOrderType = WorkOrderType.Discovery,
        Status = JobStatus.DiscoveryRequest,
        DiscoveryTechnicalNotes = "Sahada tespit notu",
        TechnicianNotes = "Önerilen çözüm notu",
        AssignedTechnician = "Teknisyen",
        CreatedDate = DateTime.UtcNow
    };

    private static ServiceJobItem NewItem(string name, int quantity, int? productId) => new()
    {
        ProductId = productId,
        QuantityUsed = quantity,
        UnitPrice = productId.HasValue ? 100m : 0m,
        UnitCost = productId.HasValue ? 60m : 0m
    };
}
