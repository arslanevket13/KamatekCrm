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
/// Keşif V2 testleri: çoklu keşif ziyareti, teknik rapor kaydı, fotoğraf yolları ve
/// keşif tamamlama doğrulaması. Keşif verileri dönüştürmeden önce (rapor + malzeme +
/// ziyaret) tek transaction'da kaydedilir; tamamlama yalnızca doğrulama geçince yapılır.
/// </summary>
public sealed class DiscoveryV2Tests
{
    [Fact]
    public async Task SaveDiscoveryAsync_CreatesReportWithMaterialsAndVisits()
    {
        var (service, factory, customerId) = CreateService();

        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));

        var result = await service.SaveDiscoveryAsync(new SaveDiscoveryRequest(
            save.Value!.JobId,
            TechnicalNotes: "Sahada 4 kat için güzergah tespit edildi.",
            RecommendedSolution: "Cat6 kablo ve IP kamera önerildi.",
            EstimatedLaborHours: 6.5,
            TechnicianName: "Ali Usta",
            PhotoPaths: ["C:\\photos\\kesif1.jpg"],
            Materials:
            [
                new DiscoveryMaterialInput(null, 1, "Kamera", 4, "Dış mekan"),
                new DiscoveryMaterialInput(null, 2, "Kablo", 120, "Cat6")
            ],
            Visits:
            [
                new DiscoveryVisitInput(null, new DateTime(2026, 8, 3, 9, 30, 0, DateTimeKind.Utc), "Ali Usta", "İlk keşif ziyareti", []),
                new DiscoveryVisitInput(null, new DateTime(2026, 8, 4, 14, 0, 0, DateTimeKind.Utc), "Veli Usta", "Kontrol ziyareti", ["C:\\photos\\kontrol.jpg"])
            ],
            ChangedBy: "test-user"));

        result.IsSuccess.Should().BeTrue(result.Error);

        await using var verify = await factory.CreateDbContextAsync();
        var report = await verify.DiscoveryReports
            .Include(r => r.Materials)
            .SingleAsync(r => r.ServiceJobId == save.Value!.JobId);

        report.TechnicalNotes.Should().Contain("güzergah");
        report.RecommendedSolution.Should().Contain("Cat6");
        report.EstimatedLaborHours.Should().Be(6.5);
        report.TechnicianName.Should().Be("Ali Usta");
        report.PhotoPathsList.Should().ContainSingle(p => p.Contains("kesif1.jpg"));
        report.Materials.Should().HaveCount(2);
        report.Materials.Should().Contain(m => m.ProductName == "Kamera" && m.Quantity == 4);
        report.Materials.Should().Contain(m => m.ProductName == "Kablo" && m.Quantity == 120);

        var visits = await verify.DiscoveryVisits
            .Where(v => v.ServiceJobId == save.Value!.JobId)
            .OrderBy(v => v.VisitDate)
            .ToListAsync();
        visits.Should().HaveCount(2, "çoklu keşif ziyareti kaydedilir");
        visits[0].TechnicianName.Should().Be("Ali Usta");
        visits[1].TechnicianName.Should().Be("Veli Usta");
        visits[1].PhotoPathsList.Should().ContainSingle(p => p.Contains("kontrol.jpg"));

        // İş emri keşif alanları raporla senkronize edilir (listeleme/görünüm tutarlılığı)
        var job = await verify.ServiceJobs.SingleAsync();
        job.DiscoveryTechnicalNotes.Should().Contain("güzergah");
        job.EstimatedLaborHours.Should().Be(6.5);
    }

    [Fact]
    public async Task SaveDiscoveryAsync_IsIdempotent_NoDuplicateRows()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));

        var request = new SaveDiscoveryRequest(
            save.Value!.JobId,
            "Not", "Çözüm", 2.0, "Ali", [],
            [new DiscoveryMaterialInput(null, 1, "Kamera", 2, null)],
            [new DiscoveryVisitInput(null, new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc), "Ali", "İlk", [])],
            "test-user");

        (await service.SaveDiscoveryAsync(request)).IsSuccess.Should().BeTrue();
        (await service.SaveDiscoveryAsync(request with { ChangedBy = "test-user" })).IsSuccess.Should().BeTrue();

        await using var verify = await factory.CreateDbContextAsync();
        (await verify.DiscoveryReports.CountAsync(r => r.ServiceJobId == save.Value!.JobId)).Should().Be(1);
        (await verify.DiscoveryMaterials.CountAsync(m => m.DiscoveryReport!.ServiceJobId == save.Value!.JobId)).Should().Be(1);
        (await verify.DiscoveryVisits.CountAsync(v => v.ServiceJobId == save.Value!.JobId)).Should().Be(1);
    }

    [Fact]
    public async Task SaveDiscoveryAsync_DiffBasedUpdate_PreservesRowIds()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));

        var request = new SaveDiscoveryRequest(
            save.Value!.JobId, "Not", "Çözüm", 2.0, "Ali", [],
            [new DiscoveryMaterialInput(null, 1, "Kamera", 2, null)],
            [new DiscoveryVisitInput(null, new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc), "Ali", "İlk", [])],
            "test-user");
        (await service.SaveDiscoveryAsync(request)).IsSuccess.Should().BeTrue();

        int materialId, visitId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            materialId = (await ctx.DiscoveryMaterials.SingleAsync()).Id;
            visitId = (await ctx.DiscoveryVisits.SingleAsync()).Id;
        }

        // Aynı ID'lerle güncelleme: miktar ve not değişir, satır çoğalmaz
        var update = new SaveDiscoveryRequest(
            save.Value!.JobId, "Güncel not", "Güncel çözüm", 3.0, "Ali", [],
            [new DiscoveryMaterialInput(materialId, 1, "Kamera", 5, "Güncel not")],
            [new DiscoveryVisitInput(visitId, new DateTime(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc), "Ali", "İkinci", [])],
            "test-user");
        var result = await service.SaveDiscoveryAsync(update);

        result.IsSuccess.Should().BeTrue(result.Error);

        await using var verify = await factory.CreateDbContextAsync();
        var material = await verify.DiscoveryMaterials.SingleAsync();
        material.Id.Should().Be(materialId);
        material.Quantity.Should().Be(5);
        material.Notes.Should().Be("Güncel not");

        var visit = await verify.DiscoveryVisits.SingleAsync();
        visit.Id.Should().Be(visitId);
        visit.Notes.Should().Be("İkinci");
    }

    [Fact]
    public async Task CompleteDiscoveryAsync_RejectsWhenReportMissing()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));

        var result = await service.CompleteDiscoveryAsync(save.Value!.JobId, "test-user");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("keşif kaydını");
        await using var verify = await factory.CreateDbContextAsync();
        (await verify.ServiceJobs.SingleAsync()).Status.Should().Be(JobStatus.DiscoveryRequest);
    }

    [Fact]
    public async Task CompleteDiscoveryAsync_RejectsWhenNoNotesOrSolution()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));

        await service.SaveDiscoveryAsync(new SaveDiscoveryRequest(
            save.Value!.JobId, null, null, 1.0, "Ali", [],
            [new DiscoveryMaterialInput(null, 1, "Kamera", 1, null)],
            [new DiscoveryVisitInput(null, new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc), "Ali", "İlk", [])],
            "test-user"));

        var result = await service.CompleteDiscoveryAsync(save.Value!.JobId, "test-user");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("teknik tespit notları");
    }

    [Fact]
    public async Task CompleteDiscoveryAsync_RejectsWhenNoMaterialAndNoVisit()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));

        await service.SaveDiscoveryAsync(new SaveDiscoveryRequest(
            save.Value!.JobId, "Teknik not", "Çözüm", 1.0, "Ali", [], [], [],
            "test-user"));

        var result = await service.CompleteDiscoveryAsync(save.Value!.JobId, "test-user");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("malzeme veya bir ziyaret");
    }

    [Fact]
    public async Task CompleteDiscoveryAsync_TransitionsToDiscoveryCompleted()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));

        await service.SaveDiscoveryAsync(new SaveDiscoveryRequest(
            save.Value!.JobId, "Teknik not", "Çözüm", 4.0, "Ali",
            ["C:\\photos\\a.jpg"],
            [new DiscoveryMaterialInput(null, 1, "Kamera", 2, null)],
            [new DiscoveryVisitInput(null, new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc), "Ali", "İlk keşif", [])],
            "test-user"));

        var result = await service.CompleteDiscoveryAsync(save.Value!.JobId, "test-user");

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentStatus.Should().Be(JobStatus.DiscoveryCompleted);
        result.Value.PreviousStatus.Should().Be(JobStatus.DiscoveryRequest);

        await using var verify = await factory.CreateDbContextAsync();
        (await verify.ServiceJobs.SingleAsync()).Status.Should().Be(JobStatus.DiscoveryCompleted);
        var history = await verify.ServiceJobHistories
            .OrderByDescending(h => h.Id)
            .FirstAsync(h => h.ServiceJobId == save.Value!.JobId);
        history.Action.Should().Be("DiscoveryCompleted");
        history.TechnicianNote.Should().Contain("1 ziyaret");
    }

    [Fact]
    public async Task CompleteDiscoveryAsync_IsIdempotent()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));

        await service.SaveDiscoveryAsync(new SaveDiscoveryRequest(
            save.Value!.JobId, "Not", "Çözüm", 1.0, "Ali", [],
            [new DiscoveryMaterialInput(null, 1, "Kamera", 1, null)],
            [new DiscoveryVisitInput(null, new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc), "Ali", "İlk", [])],
            "test-user"));

        (await service.CompleteDiscoveryAsync(save.Value!.JobId, "test-user")).IsSuccess.Should().BeTrue();
        var second = await service.CompleteDiscoveryAsync(save.Value!.JobId, "test-user");

        second.IsSuccess.Should().BeTrue("tamamlanmış keşif yeniden tamamlanabilir (idempotent)");
        await using var verify = await factory.CreateDbContextAsync();
        (await verify.ServiceJobHistories.CountAsync(h => h.Action == "DiscoveryCompleted")).Should().Be(1,
            "idempotent tamamlama yeni tarihçe kaydı oluşturmaz");
    }

    [Fact]
    public async Task GetWorkOrderWorkflowAsync_ReturnsVisitsNewestFirst()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));

        await service.SaveDiscoveryAsync(new SaveDiscoveryRequest(
            save.Value!.JobId, "Not", "Çözüm", 1.0, "Ali", [],
            [],
            [
                new DiscoveryVisitInput(null, new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc), "Ali", "İlk", []),
                new DiscoveryVisitInput(null, new DateTime(2026, 8, 5, 9, 0, 0, DateTimeKind.Utc), "Veli", "İkinci", [])
            ],
            "test-user"));

        var readService = new ServiceJobReadService(factory, new TestAuthorizationService(), new KamatekCrm.ApplicationCore.Services.WorkOrderNextActionResolver());
        var workflow = await readService.GetWorkOrderWorkflowAsync(save.Value!.JobId);

        workflow.IsSuccess.Should().BeTrue(workflow.Error);
        var visits = workflow.Value!.Visits!;
        visits.Should().HaveCount(2);
        visits.Select(v => v.TechnicianName).Should().Equal("Veli", "Ali");
    }

    // ───────────────────────────── Fixture ─────────────────────────────

    private static (ServiceJobCommandService Service, IDbContextFactory<AppDbContext> Factory, int CustomerId)
        CreateService()
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
                CustomerCode = "TEST-002",
                PhoneNumber = "0532 111 22 33"
            };
            seed.Customers.Add(customer);
            seed.Products.Add(new Product
            {
                Id = 1,
                ProductName = "Kamera",
                SKU = "CAM-002",
                TotalStockQuantity = 100
            });
            seed.Products.Add(new Product
            {
                Id = 2,
                ProductName = "Kablo",
                SKU = "CBL-002",
                TotalStockQuantity = 1000
            });
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
        CreatedDate = DateTime.UtcNow
    };
}
