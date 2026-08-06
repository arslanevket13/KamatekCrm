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
            [NewItem("Kamera", 1, 1)],
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
            [NewItem("Kamera", 1, 1)],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        var result = await service.PlanInstallationAsync(new PlanInstallationRequest(
            save.Value!.JobId, null, null, null, null, "test-user"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("kabul edilmiş teklif");
    }

    [Fact]
    public async Task PlanInstallationAsync_CopiesFractionalQuantitiesWithoutLoss()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 1, 1)],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        int quotationId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            quotationId = (await ctx.WorkOrderQuotations.SingleAsync()).Id;
        }

        await service.UpdateQuotationAsync(new UpdateWorkOrderQuotationRequest(
            quotationId, null, null, null, null, 0m, 0m, 0m, 20m,
            [new QuotationItemInput(null, 1, "Kablo", 2.5m, 10m, 0m, 20m)]));
        await service.AcceptQuotationAsync(quotationId, "test-user");

        var plan = await service.PlanInstallationAsync(new PlanInstallationRequest(
            save.Value!.JobId, null, "Ali Usta", null, null, "test-user"));

        plan.IsSuccess.Should().BeTrue(plan.Error);
        await using var verify = await factory.CreateDbContextAsync();
        var installation = await verify.InstallationOrders
            .Include(i => i.Materials)
            .SingleAsync(i => i.ServiceJobId == save.Value!.JobId);
        installation.Materials.Should().ContainSingle(m => m.Quantity == 2.5m);
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
            LaborHours: 6m,
            ChangedBy: "test-user"));

        result.IsSuccess.Should().BeTrue(result.Error);
        await using var verify = await factory.CreateDbContextAsync();

        var job = await verify.ServiceJobs.SingleAsync();
        job.Status.Should().Be(JobStatus.InstallationCompleted);
        job.CompletedDate.Should().NotBeNull();
        job.IsStockDeducted.Should().BeTrue();
        job.RepairStatus.Should().Be(RepairStatus.Delivered);
        (await verify.Inventories.SingleAsync(i => i.ProductId == 1)).Quantity.Should().Be(8, "montaj tamamlanınca rezerve stok tüketilir");

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
            [NewItem("Kamera", 1, 1)],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        var result = await service.CompleteInstallationAsync(new CompleteInstallationRequest(
            save.Value!.JobId, null, null, null, 0m, "test-user"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().MatchRegex("montaj planlanmalı|durumuna geçilemez");
    }

    [Fact]
    public async Task CompleteInstallationAsync_RejectsWhenLaborHoursMissing()
    {
        // Kapı malzeme bazlı olsa da servis, tamamlama anında işçilik saatinin > 0
        // olduğunu doğrular — saat yalnızca tamamlama formunda girilir.
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 1, 1)],
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
            save.Value!.JobId, null, "Ali Usta", null, LaborHours: 0m, "test-user"));

        result.IsFailure.Should().BeTrue("işçilik saati girilmeden montaj tamamlanamaz");
        result.Error.Should().Contain("işçilik saati");
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

        // Malzeme net toplamı: 2*1000*0.9 + 50*10 = 1800 + 500 = 2300
        // Net: 2300 - 50 (iskonto) + 500 (işçilik) + 100 (nakliye) = 2850
        // Satır bazlı KDV: 1800*0.20 + 500*0.20 = 360 + 100 = 460
        // Genel Toplam → 2850 + 460 = 3310
        update.Value!.TotalAmount.Should().Be(3310m);

        await using var verify = await factory.CreateDbContextAsync();
        var quote = await verify.WorkOrderQuotations.Include(q => q.Items).SingleAsync(q => q.Id == quotationId);
        quote.Warranty.Should().Be("2 yıl");
        quote.PaymentTerms.Should().Be("Peşin / 30 gün");
        quote.TotalAmount.Should().Be(3310m);
        quote.TaxAmount.Should().Be(460m);
        quote.Items.Should().HaveCount(2);
        quote.Items.Single(i => i.ProductName == "Kamera").UnitPrice.Should().Be(1000m);
    }

    [Fact]
    public async Task UpdateQuotationAsync_PreservesItemIdsOnDiffUpdate()
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

        // 1. Güncelleme: iki satır eklenir (henüz ID'siz)
        var first = await service.UpdateQuotationAsync(new UpdateWorkOrderQuotationRequest(
            quotationId, null, null, null, null, 0m, 0m, 0m, 20m,
            [
                new QuotationItemInput(null, 1, "Kamera", 2, 1000m, 0m, 20m),
                new QuotationItemInput(null, null, "Kablo", 50, 10m, 0m, 20m)
            ]));
        first.IsSuccess.Should().BeTrue(first.Error);

        int kameraId, kabloId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            var items = await ctx.QuotationItems.Where(i => i.QuotationId == quotationId).ToListAsync();
            items.Should().HaveCount(2);
            kameraId = items.Single(i => i.ProductName == "Kamera").Id;
            kabloId = items.Single(i => i.ProductName == "Kablo").Id;
        }

        // 2. Güncelleme: aynı satırlar ID'leriyle gönderilir, miktar değişir
        var second = await service.UpdateQuotationAsync(new UpdateWorkOrderQuotationRequest(
            quotationId, null, null, null, null, 0m, 0m, 0m, 20m,
            [
                new QuotationItemInput(kameraId, 1, "Kamera", 4, 1000m, 0m, 20m),
                new QuotationItemInput(kabloId, null, "Kablo", 60, 10m, 0m, 20m)
            ]));
        second.IsSuccess.Should().BeTrue(second.Error);

        await using var verify = await factory.CreateDbContextAsync();
        var persisted = await verify.QuotationItems.Where(i => i.QuotationId == quotationId).ToListAsync();
        persisted.Should().HaveCount(2, "diff güncelleme satır çoğaltmamalı");
        persisted.Single(i => i.ProductName == "Kamera").Id.Should().Be(kameraId, "satır kimliği korunmalı");
        persisted.Single(i => i.ProductName == "Kamera").Quantity.Should().Be(4);
        persisted.Single(i => i.ProductName == "Kablo").Id.Should().Be(kabloId, "satır kimliği korunmalı");
        persisted.Single(i => i.ProductName == "Kablo").Quantity.Should().Be(60);
    }

    [Fact]
    public async Task UpdateQuotationAsync_LineBasedTax_AppliesPerLineRates()
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

        // Farklı satır KDV oranları: Kamera %10, Kablo %20
        var update = await service.UpdateQuotationAsync(new UpdateWorkOrderQuotationRequest(
            quotationId, null, null, null, null,
            LaborCost: 500m,
            ShippingCost: 100m,
            DiscountAmount: 50m,
            TaxRate: 20m,
            Items:
            [
                new QuotationItemInput(null, 1, "Kamera", 2, 1000m, 10m, 10m),
                new QuotationItemInput(null, null, "Kablo", 50, 10m, 0m, 20m)
            ]));

        update.IsSuccess.Should().BeTrue(update.Error);

        // Kamera: 2*1000*0.9 = 1800 net, %10 → 180 KDV
        // Kablo: 50*10 = 500 net, %20 → 100 KDV
        // Net: 2300 - 50 + 500 + 100 = 2850; KDV: 280; Toplam: 3130
        update.Value!.TotalAmount.Should().Be(3130m);

        await using var verify = await factory.CreateDbContextAsync();
        var quote = await verify.WorkOrderQuotations.SingleAsync(q => q.Id == quotationId);
        quote.TaxAmount.Should().Be(280m);
        quote.TotalAmount.Should().Be(3130m);
    }

    [Fact]
    public async Task UpdateQuotationAsync_RemovesDroppedItems()
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

        await service.UpdateQuotationAsync(new UpdateWorkOrderQuotationRequest(
            quotationId, null, null, null, null, 0m, 0m, 0m, 20m,
            [
                new QuotationItemInput(null, 1, "Kamera", 2, 1000m, 0m, 20m),
                new QuotationItemInput(null, null, "Kablo", 50, 10m, 0m, 20m)
            ]));

        // Kablo satırı listeden çıkarılıyor
        var update = await service.UpdateQuotationAsync(new UpdateWorkOrderQuotationRequest(
            quotationId, null, null, null, null, 0m, 0m, 0m, 20m,
            [
                new QuotationItemInput(null, 1, "Kamera", 2, 1000m, 0m, 20m)
            ]));
        update.IsSuccess.Should().BeTrue(update.Error);

        await using var verify = await factory.CreateDbContextAsync();
        var persisted = await verify.QuotationItems.Where(i => i.QuotationId == quotationId).ToListAsync();
        persisted.Should().ContainSingle(i => i.ProductName == "Kamera");
        persisted.Should().NotContain(i => i.ProductName == "Kablo");
    }

    [Fact]
    public async Task CreateRevisionAsync_CopiesQuoteAsDraft_AndPreservesSource()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 2, 1)],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        int sourceId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            sourceId = (await ctx.WorkOrderQuotations.SingleAsync()).Id;
        }

        await service.UpdateQuotationAsync(new UpdateWorkOrderQuotationRequest(
            sourceId, "Kamera kurulumu", "2 yıl", null, null, 500m, 100m, 0m, 20m,
            [
                new QuotationItemInput(null, 1, "Kamera", 2, 1000m, 0m, 20m, 0),
                new QuotationItemInput(null, null, "Kablo", 50, 10m, 0m, 20m, 1)
            ]));
        await service.AcceptQuotationAsync(sourceId, "test-user");

        var revision = await service.CreateRevisionAsync(sourceId, "test-user");

        revision.IsSuccess.Should().BeTrue(revision.Error);
        revision.Value!.RevisionNumber.Should().Be(1);
        revision.Value.NewQuotationId.Should().NotBe(sourceId);

        await using var verify = await factory.CreateDbContextAsync();
        var quotes = await verify.WorkOrderQuotations.Include(q => q.Items).OrderBy(q => q.Id).ToListAsync();
        quotes.Should().HaveCount(2);

        var source = quotes.Single(q => q.Id == sourceId);
        source.Status.Should().Be(QuotationStatus.Accepted, "kaynak teklif değişmeden kalır");
        source.RevisionNumber.Should().Be(0);
        source.ParentQuotationId.Should().BeNull();
        source.Items.Should().HaveCount(2);

        var copy = quotes.Single(q => q.Id == revision.Value.NewQuotationId);
        copy.ParentQuotationId.Should().Be(sourceId);
        copy.RevisionNumber.Should().Be(1);
        copy.Status.Should().Be(QuotationStatus.Draft);
        copy.QuotationNumber.Should().Be(source.QuotationNumber);
        copy.Items.Should().HaveCount(2);
        copy.Items.OrderBy(i => i.Sequence).Select(i => i.ProductName).Should().Equal("Kamera", "Kablo");
        copy.Items.Single(i => i.ProductName == "Kamera").Quantity.Should().Be(2m);
        copy.TotalAmount.Should().Be(source.TotalAmount);
    }

    [Fact]
    public async Task CreateRevisionAsync_RejectsDraftQuote()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 1, 1)],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        int quotationId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            quotationId = (await ctx.WorkOrderQuotations.SingleAsync()).Id;
        }

        var result = await service.CreateRevisionAsync(quotationId, "test-user");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Taslak");
    }

    [Fact]
    public async Task CreateRevisionAsync_RejectsDuplicatePendingRevision()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 1, 1)],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        int quotationId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            quotationId = (await ctx.WorkOrderQuotations.SingleAsync()).Id;
        }
        await service.AcceptQuotationAsync(quotationId, "test-user");

        var first = await service.CreateRevisionAsync(quotationId, "test-user");
        var second = await service.CreateRevisionAsync(quotationId, "test-user");

        first.IsSuccess.Should().BeTrue();
        second.IsFailure.Should().BeTrue("bekleyen taslak revizyon varken mükerrer revizyon oluşturulmamalı");
        second.Error.Should().Contain("revizyon");

        await using var verify = await factory.CreateDbContextAsync();
        (await verify.WorkOrderQuotations.CountAsync()).Should().Be(2, "yalnızca bir revizyon oluşturulmalı");
    }

    [Fact]
    public async Task UpdateQuotationAsync_RejectsAcceptedQuote()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 1, 1)],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        int quotationId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            quotationId = (await ctx.WorkOrderQuotations.SingleAsync()).Id;
        }
        await service.AcceptQuotationAsync(quotationId, "test-user");

        var update = await service.UpdateQuotationAsync(new UpdateWorkOrderQuotationRequest(
            quotationId, null, null, null, null, 0m, 0m, 0m, 20m,
            [new QuotationItemInput(null, 1, "Kamera", 2, 1000m, 0m, 20m)]));

        update.IsFailure.Should().BeTrue("kabul edilmiş teklif doğrudan düzenlenemez");
        update.Error.Should().Contain("düzenlenemez");
    }

    [Fact]
    public async Task UpdateQuotationAsync_PersistsSequenceAndDecimalQuantity()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 1, 1)],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        int quotationId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            quotationId = (await ctx.WorkOrderQuotations.SingleAsync()).Id;
        }

        var update = await service.UpdateQuotationAsync(new UpdateWorkOrderQuotationRequest(
            quotationId, null, null, null, null, 0m, 0m, 0m, 20m,
            [
                new QuotationItemInput(null, 1, "Kamera-A", 2.5m, 100m, 0m, 20m, 2),
                new QuotationItemInput(null, null, "Kablo-B", 1, 10m, 0m, 20m, 0),
                new QuotationItemInput(null, null, "Kablo-C", 3, 10m, 0m, 20m, 1)
            ]));
        update.IsSuccess.Should().BeTrue(update.Error);

        var readService = new ServiceJobReadService(factory, new TestAuthorizationService(), new KamatekCrm.ApplicationCore.Services.WorkOrderNextActionResolver());
        var workflow = await readService.GetWorkOrderWorkflowAsync(save.Value!.JobId);

        workflow.IsSuccess.Should().BeTrue(workflow.Error);
        var items = workflow.Value!.Quotation!.Items;
        items.Select(i => i.ProductName).Should().Equal("Kablo-B", "Kablo-C", "Kamera-A");
        items.Single(i => i.ProductName == "Kamera-A").Quantity.Should().Be(2.5m, "kesirli miktar korunur");
        items.Single(i => i.ProductName == "Kamera-A").Sequence.Should().Be(2);
    }

    [Fact]
    public async Task AcceptAndRejectQuotation_UpdatesStatus()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 1, 1)],
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
    public async Task SendQuotationAsync_MovesDraftToSent_AndIsIdempotent()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 1, 1)],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        int quotationId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            quotationId = (await ctx.WorkOrderQuotations.SingleAsync()).Id;
        }

        var sent = await service.SendQuotationAsync(quotationId, "test-user");
        var repeated = await service.SendQuotationAsync(quotationId, "test-user");

        sent.IsSuccess.Should().BeTrue(sent.Error);
        sent.Value!.Status.Should().Be(QuotationStatus.Sent);
        repeated.IsSuccess.Should().BeTrue("zaten gönderilmiş teklifte tekrarlanan gönderim idempotent olmalı");

        await using var verify = await factory.CreateDbContextAsync();
        var quote = await verify.WorkOrderQuotations.SingleAsync();
        quote.Status.Should().Be(QuotationStatus.Sent);
        quote.SentDate.Should().NotBeNull();
        (await verify.ServiceJobHistories.CountAsync(h => h.ServiceJobId == save.Value!.JobId && h.Action == "QuotationSent"))
            .Should().Be(1, "gönderim tarihçeye yalnızca bir kez yazılır");

        // Gönderildikten sonra müşteri cevabı kaydedilebilir (Sent → Accepted).
        var accept = await service.AcceptQuotationAsync(quotationId, "test-user");
        accept.IsSuccess.Should().BeTrue(accept.Error);
    }

    [Fact]
    public async Task SendQuotationAsync_RejectsAcceptedQuote()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 1, 1)],
            false,
            "test-user"));
        await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        int quotationId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            quotationId = (await ctx.WorkOrderQuotations.SingleAsync()).Id;
        }
        await service.AcceptQuotationAsync(quotationId, "test-user");

        var result = await service.SendQuotationAsync(quotationId, "test-user");

        result.IsFailure.Should().BeTrue("kabul edilmiş teklif gönderilemez");
        result.Error.Should().Contain("gönderilemez");
    }

    [Fact]
    public async Task RejectQuotationAsync_StoresReason()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [NewItem("Kamera", 1, 1)],
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

        var readService = new ServiceJobReadService(factory, new TestAuthorizationService(), new KamatekCrm.ApplicationCore.Services.WorkOrderNextActionResolver());
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

    [Fact]
    public async Task ConvertToQuoteAsync_RejectsWhenDiscoveryNotesMissing()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            new ServiceJob
            {
                CustomerId = customerId,
                Description = "Keşif testi",
                JobCategory = JobCategory.CCTV,
                WorkOrderType = WorkOrderType.Discovery,
                Status = JobStatus.DiscoveryRequest,
                AssignedTechnician = "Teknisyen"
            },
            [NewItem("Kamera", 2, 1)],
            false,
            "test-user"));

        var result = await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        result.IsFailure.Should().BeTrue("teknik tespit ve önerilen çözüm olmadan teklif oluşturulamaz");
        result.Error.Should().Contain("Teknik tespit girilmedi");
        result.Error.Should().Contain("Önerilen çözüm girilmedi");
    }

    [Fact]
    public async Task ConvertToQuoteAsync_RejectsWhenNoMaterials()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));

        var result = await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        result.IsFailure.Should().BeTrue("tahmini malzeme/hizmet kalemi olmadan teklif oluşturulamaz");
        result.Error.Should().Contain("malzeme/hizmet kalemi");
    }

    [Fact]
    public async Task ConvertToQuoteAsync_RejectsWhenNoTechnician()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            new ServiceJob
            {
                CustomerId = customerId,
                Description = "Keşif testi",
                JobCategory = JobCategory.CCTV,
                WorkOrderType = WorkOrderType.Discovery,
                Status = JobStatus.DiscoveryRequest,
                DiscoveryTechnicalNotes = "Sahada tespit notu",
                TechnicianNotes = "Önerilen çözüm notu"
            },
            [NewItem("Kamera", 2, 1)],
            false,
            "test-user"));

        var result = await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");

        result.IsFailure.Should().BeTrue("keşfi yapan teknisyen belirtilmeden teklif oluşturulamaz");
        result.Error.Should().Contain("teknisyen belirtilmedi");
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
                ProductName = "Kamera",
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
            seed.Inventories.Add(new Inventory { ProductId = 2, WarehouseId = 1, Quantity = 1000 });
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
