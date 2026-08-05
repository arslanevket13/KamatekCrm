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
/// Montaj V2 testleri: montaj emrine işçilik saati, malzeme/görev diff tabanlı güncelleme,
/// stok rezervasyon senkronizasyonu (çekme akışı) ve montaj tamamlama doğrulaması.
/// </summary>
public sealed class InstallationV2Tests
{
    [Fact]
    public async Task SaveInstallationAsync_UpdatesHeaderAndDiffMaterialsAndTasks()
    {
        var (service, factory, customerId) = CreateService();
        var (jobId, quotationId) = await SetupPlannedInstallationAsync(service, factory, customerId);

        var save = await service.SaveInstallationAsync(new SaveInstallationRequest(
            jobId,
            TechnicianId: null,
            TechnicianName: "Veli Usta",
            InstallationDate: new DateTime(2026, 8, 12),
            Notes: "Güncel montaj notu",
            LaborHours: 5m,
            Materials:
            [
                new InstallationMaterialInput(null, 1, "Kamera", 2m, 1000m, "Dış mekan"),
                new InstallationMaterialInput(null, null, "Özel bağlantı kutusu", 1m, 250m, null)
            ],
            Tasks:
            [
                new InstallationTaskInput(null, "Saha kontrolü", "Montaj öncesi", false),
                new InstallationTaskInput(null, "Cihaz kurulumu", null, true)
            ],
            ChangedBy: "test-user"));

        save.IsSuccess.Should().BeTrue(save.Error);

        await using var verify = await factory.CreateDbContextAsync();
        var installation = await verify.InstallationOrders
            .Include(i => i.Materials)
            .Include(i => i.Tasks)
            .SingleAsync(i => i.ServiceJobId == jobId);

        installation.TechnicianName.Should().Be("Veli Usta");
        installation.InstallationDate.Should().Be(new DateTime(2026, 8, 12));
        installation.Notes.Should().Be("Güncel montaj notu");
        installation.LaborHours.Should().Be(5m);
        installation.Materials.Should().HaveCount(2);
        installation.Materials.Should().Contain(m => m.ProductName == "Kamera" && m.Quantity == 2m && m.ProductId == 1);
        installation.Materials.Should().Contain(m => m.ProductName == "Özel bağlantı kutusu" && m.ProductId == null);
        installation.Tasks.Should().HaveCount(2);
        installation.Tasks.Single(t => t.Title == "Cihaz kurulumu").IsCompleted.Should().BeTrue();
        installation.Tasks.Single(t => t.Title == "Cihaz kurulumu").CompletedAt.Should().NotBeNull();

        // İş emri teknisyeni de güncellenir
        var job = await verify.ServiceJobs.SingleAsync();
        job.AssignedTechnician.Should().Be("Veli Usta");
    }

    [Fact]
    public async Task SaveInstallationAsync_IsIdempotent_NoDuplicateRows()
    {
        var (service, factory, customerId) = CreateService();
        var (jobId, quotationId) = await SetupPlannedInstallationAsync(service, factory, customerId);

        var request = new SaveInstallationRequest(
            jobId, null, "Ali Usta", null, "Not", 4m,
            [new InstallationMaterialInput(null, 1, "Kamera", 2m, 1000m, null)],
            [new InstallationTaskInput(null, "Saha kontrolü", null, false)],
            "test-user");

        (await service.SaveInstallationAsync(request)).IsSuccess.Should().BeTrue();
        (await service.SaveInstallationAsync(request with { ChangedBy = "test-user" })).IsSuccess.Should().BeTrue();

        await using var verify = await factory.CreateDbContextAsync();
        (await verify.InstallationMaterials.CountAsync(m => m.InstallationOrder!.ServiceJobId == jobId)).Should().Be(1);
        (await verify.InstallationTasks.CountAsync(t => t.InstallationOrder!.ServiceJobId == jobId)).Should().Be(1);
        (await verify.ServiceJobItems.CountAsync(i => i.ServiceJobId == jobId)).Should().Be(1, "stok kalemleri tek satırda");
    }

    [Fact]
    public async Task SaveInstallationAsync_DiffBasedUpdate_PreservesRowIds()
    {
        var (service, factory, customerId) = CreateService();
        var (jobId, quotationId) = await SetupPlannedInstallationAsync(service, factory, customerId);

        var request = new SaveInstallationRequest(
            jobId, null, "Ali Usta", null, "Not", 4m,
            [new InstallationMaterialInput(null, 1, "Kamera", 2m, 1000m, null)],
            [new InstallationTaskInput(null, "Saha kontrolü", null, false)],
            "test-user");
        (await service.SaveInstallationAsync(request)).IsSuccess.Should().BeTrue();

        int materialId, taskId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            materialId = (await ctx.InstallationMaterials.SingleAsync()).Id;
            taskId = (await ctx.InstallationTasks.SingleAsync()).Id;
        }

        // Aynı ID'lerle güncelleme: miktar/değerler değişir, satır çoğalmaz
        var update = new SaveInstallationRequest(
            jobId, null, "Veli Usta", null, "Güncel", 6m,
            [new InstallationMaterialInput(materialId, 1, "Kamera", 4m, 900m, "Güncel not")],
            [new InstallationTaskInput(taskId, "Saha kontrolü", "Güncel açıklama", true)],
            "test-user");
        var result = await service.SaveInstallationAsync(update);

        result.IsSuccess.Should().BeTrue(result.Error);

        await using var verify = await factory.CreateDbContextAsync();
        var material = await verify.InstallationMaterials.SingleAsync();
        material.Id.Should().Be(materialId);
        material.Quantity.Should().Be(4m);
        material.UnitPrice.Should().Be(900m);
        material.Notes.Should().Be("Güncel not");

        var task = await verify.InstallationTasks.SingleAsync();
        task.Id.Should().Be(taskId);
        task.IsCompleted.Should().BeTrue();
        task.CompletedAt.Should().NotBeNull();

        // Stok kalemleri de 4'e güncellenmiş olmalı (ServiceJobItems senkronizasyonu)
        var stockItem = await verify.ServiceJobItems.SingleAsync(i => i.ServiceJobId == jobId);
        stockItem.QuantityUsed.Should().Be(4);
    }

    [Fact]
    public async Task SaveInstallationAsync_RejectsWhenNotPlanned()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));

        var result = await service.SaveInstallationAsync(new SaveInstallationRequest(
            save.Value!.JobId, null, "Ali", null, null, 2m, [], [], "test-user"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("montajı planlayın");
    }

    [Fact]
    public async Task SaveInstallationAsync_ReservesStockForProductMaterials()
    {
        var (service, factory, customerId) = CreateService(stockQuantity: 20);
        var (jobId, quotationId) = await SetupPlannedInstallationAsync(service, factory, customerId);

        var save = await service.SaveInstallationAsync(new SaveInstallationRequest(
            jobId, null, "Ali Usta", null, null, 4m,
            [new InstallationMaterialInput(null, 1, "Kamera", 5m, 1000m, null)],
            [], "test-user"));
        save.IsSuccess.Should().BeTrue(save.Error);

        await using var verify = await factory.CreateDbContextAsync();
        var reservation = await verify.StockReservations
            .SingleAsync(r => r.ReferenceType == "ServiceJob" && r.ProductId == 1 && r.IsActive);
        reservation.Quantity.Should().Be(5, "montaj malzemeleri stoktan rezerve edilir");

        // Rezervasyon sonrası iş tamamlanınca stok düşer (çekme akışı)
        var complete = await service.CompleteInstallationAsync(new CompleteInstallationRequest(
            jobId,
            DeliveryNote: "Teslim edildi",
            CompletionTechnician: "Ali Usta",
            CustomerSignature: null,
            LaborHours: 4m,
            ChangedBy: "test-user"));
        complete.IsSuccess.Should().BeTrue(complete.Error);

        await using var verify2 = await factory.CreateDbContextAsync();
        var inventory = await verify2.Inventories.SingleAsync(i => i.ProductId == 1);
        inventory.Quantity.Should().Be(15, "rezerve edilen 5 adet montaj tamamlanınca stoktan düşer");
        (await verify2.StockReservations.CountAsync(r => r.ReferenceId == jobId.ToString() && r.IsActive)).Should().Be(0);
    }

    [Fact]
    public async Task CompleteInstallationAsync_RejectsWithoutLaborHours()
    {
        var (service, factory, customerId) = CreateService();
        var (jobId, quotationId) = await SetupPlannedInstallationAsync(service, factory, customerId);

        await service.SaveInstallationAsync(new SaveInstallationRequest(
            jobId, null, "Ali Usta", null, null, 0m,
            [new InstallationMaterialInput(null, 1, "Kamera", 2m, 1000m, null)],
            [], "test-user"));

        var result = await service.CompleteInstallationAsync(new CompleteInstallationRequest(
            jobId, "Teslim", "Ali", null, 0m, "test-user"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("işçilik saati");
    }

    [Fact]
    public async Task CompleteInstallationAsync_RejectsWithoutMaterials()
    {
        var (service, factory, customerId) = CreateService();
        var (jobId, quotationId) = await SetupPlannedInstallationAsync(service, factory, customerId);

        // Malzemeleri tamamen temizle
        await service.SaveInstallationAsync(new SaveInstallationRequest(
            jobId, null, "Ali Usta", null, null, 4m, [], [], "test-user"));

        var result = await service.CompleteInstallationAsync(new CompleteInstallationRequest(
            jobId, "Teslim", "Ali", null, 4m, "test-user"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("en az bir malzeme");
    }

    [Fact]
    public async Task CompleteInstallationAsync_StoresLaborHoursOnCompletionForm()
    {
        var (service, factory, customerId) = CreateService();
        var (jobId, quotationId) = await SetupPlannedInstallationAsync(service, factory, customerId);

        await service.SaveInstallationAsync(new SaveInstallationRequest(
            jobId, null, "Ali Usta", null, null, 2m,
            [new InstallationMaterialInput(null, 1, "Kamera", 2m, 1000m, null)],
            [], "test-user"));

        var result = await service.CompleteInstallationAsync(new CompleteInstallationRequest(
            jobId,
            DeliveryNote: "Tamamlandı",
            CompletionTechnician: "Ali Usta",
            CustomerSignature: "base64-imza",
            LaborHours: 7.5m,
            ChangedBy: "test-user"));

        result.IsSuccess.Should().BeTrue(result.Error);

        await using var verify = await factory.CreateDbContextAsync();
        var installation = await verify.InstallationOrders.SingleAsync(i => i.ServiceJobId == jobId);
        installation.LaborHours.Should().Be(7.5m);
        installation.CompletedAt.Should().NotBeNull();
        installation.DeliveryNote.Should().Be("Tamamlandı");
        installation.CustomerSignature.Should().Be("base64-imza");

        var job = await verify.ServiceJobs.SingleAsync();
        job.Status.Should().Be(JobStatus.InstallationCompleted);

        // Read servisi LaborHours'u döndürür
        var readService = new ServiceJobReadService(factory, new TestAuthorizationService());
        var workflow = await readService.GetWorkOrderWorkflowAsync(jobId);
        workflow.Value!.Installation!.LaborHours.Should().Be(7.5m);
    }

    // ───────────────────────────── Fixture ─────────────────────────────

    private static async Task<(int JobId, int QuotationId)> SetupPlannedInstallationAsync(
        ServiceJobCommandService service,
        IDbContextFactory<AppDbContext> factory,
        int customerId)
    {
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
        return (save.Value!.JobId, quotationId);
    }

    private static (ServiceJobCommandService Service, IDbContextFactory<AppDbContext> Factory, int CustomerId)
        CreateService(int stockQuantity = 20)
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
                CustomerCode = "TEST-003",
                PhoneNumber = "0532 111 22 33"
            };
            seed.Customers.Add(customer);
            seed.Products.Add(new Product
            {
                Id = 1,
                ProductName = "Kamera",
                SKU = "CAM-003",
                TotalStockQuantity = stockQuantity
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
