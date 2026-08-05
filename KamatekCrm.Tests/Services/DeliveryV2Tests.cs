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
/// Teslim &amp; Faturalandırma (Paket 7) testleri: teslim doğrulaması (durum geçişi + ödeme
/// tutarlılığı), teslim kaydının kalıcılığı, teslim edilmiş işte ödeme güncellemesi ve
/// workflow projeksiyonunda teslim verisinin dönmesi.
/// </summary>
public sealed class DeliveryV2Tests
{
    [Fact]
    public async Task CompleteDeliveryAsync_RequiresInstallationCompleted()
    {
        var (service, factory, customerId) = CreateService();
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewDiscoveryJob(customerId),
            [],
            false,
            "test-user"));

        var result = await service.CompleteDeliveryAsync(new CompleteDeliveryRequest(
            save.Value!.JobId,
            DeliveredBy: "Ali Usta",
            DeliveryNote: "Teslim",
            CustomerSignature: null,
            PaymentStatus: PaymentStatus.Unpaid,
            PaymentMethod: PaymentMethod.Cash,
            PaidAmount: 0m,
            InvoiceNumber: null,
            ChangedBy: "test-user"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("geçilemez");
    }

    [Fact]
    public async Task CompleteDeliveryAsync_RejectsUnpaidWithAmount()
    {
        var (service, factory, customerId) = CreateService();
        var jobId = await SetupDeliverableJobAsync(service, factory, customerId);

        var result = await service.CompleteDeliveryAsync(new CompleteDeliveryRequest(
            jobId,
            "Ali Usta", "Teslim", null,
            PaymentStatus.Unpaid, PaymentMethod.Cash, 500m, null, "test-user"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("tahsilat tutarı");
    }

    [Fact]
    public async Task CompleteDeliveryAsync_RejectsPaidWithoutAmount()
    {
        var (service, factory, customerId) = CreateService();
        var jobId = await SetupDeliverableJobAsync(service, factory, customerId);

        var result = await service.CompleteDeliveryAsync(new CompleteDeliveryRequest(
            jobId,
            "Ali Usta", "Teslim", null,
            PaymentStatus.Paid, PaymentMethod.CreditCard, 0m, null, "test-user"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("tahsilat tutarı");
    }

    [Fact]
    public async Task CompleteDeliveryAsync_RecordsDeliveryPaymentAndStatus()
    {
        var (service, factory, customerId) = CreateService();
        var jobId = await SetupDeliverableJobAsync(service, factory, customerId);

        var result = await service.CompleteDeliveryAsync(new CompleteDeliveryRequest(
            jobId,
            DeliveredBy: "Ali Usta",
            DeliveryNote: "Cihaz kurulup test edildi; müşteriye teslim edildi.",
            CustomerSignature: "base64-imza",
            PaymentStatus: PaymentStatus.Partial,
            PaymentMethod: PaymentMethod.BankTransfer,
            PaidAmount: 1200m,
            InvoiceNumber: "INV-2026-0042",
            ChangedBy: "test-user"));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CurrentStatus.Should().Be(JobStatus.Delivered);

        await using var verify = await factory.CreateDbContextAsync();
        var job = await verify.ServiceJobs.SingleAsync();
        job.Status.Should().Be(JobStatus.Delivered);
        job.CompletedDate.Should().NotBeNull();
        job.IsCustomerApproved.Should().BeTrue("teslimde müşteri onayı alınır");
        job.CustomerSignature.Should().Be("base64-imza", "imza iş emrine de yansır");

        var delivery = await verify.JobDeliveries.SingleAsync(d => d.ServiceJobId == jobId);
        delivery.DeliveredBy.Should().Be("Ali Usta");
        delivery.DeliveryNote.Should().Contain("müşteriye teslim");
        delivery.CustomerSignature.Should().Be("base64-imza");
        delivery.PaymentStatus.Should().Be(PaymentStatus.Partial);
        delivery.PaymentMethod.Should().Be(PaymentMethod.BankTransfer);
        delivery.PaidAmount.Should().Be(1200m);
        delivery.InvoiceNumber.Should().Be("INV-2026-0042");

        var history = await verify.ServiceJobHistories
            .Where(h => h.ServiceJobId == jobId && h.Action == "DeliveryCompleted")
            .SingleAsync();
        history.JobStatusChange.Should().Be(JobStatus.Delivered);
        history.TechnicianNote.Should().Contain("Kısmi Ödendi");

        // Read servisi workflow'a teslim verisini ekler
        var readService = new ServiceJobReadService(factory, new TestAuthorizationService());
        var workflow = await readService.GetWorkOrderWorkflowAsync(jobId);
        workflow.Value!.Delivery.Should().NotBeNull();
        workflow.Value.Delivery!.PaidAmount.Should().Be(1200m);
        workflow.Value.Delivery!.PaymentStatus.Should().Be(PaymentStatus.Partial);
        workflow.Value.Delivery!.InvoiceNumber.Should().Be("INV-2026-0042");
    }

    [Fact]
    public async Task CompleteDeliveryAsync_UpdatesExistingDeliveryAfterDelivered()
    {
        var (service, factory, customerId) = CreateService();
        var jobId = await SetupDeliverableJobAsync(service, factory, customerId);

        var first = await service.CompleteDeliveryAsync(new CompleteDeliveryRequest(
            jobId, "Ali Usta", "İlk teslim", null,
            PaymentStatus.Paid, PaymentMethod.CreditCard, 2700m, "INV-1", "test-user"));
        first.IsSuccess.Should().BeTrue(first.Error);

        // Teslim edilmiş işte ödeme güncellemesi (tahsilat revizyonu) — durum tekrar değişmez
        var update = await service.CompleteDeliveryAsync(new CompleteDeliveryRequest(
            jobId, "Veli Usta", "Güncel teslim notu", "yeni-imza",
            PaymentStatus.Partial, PaymentMethod.Cash, 1500m, "INV-2", "test-user"));

        update.IsSuccess.Should().BeTrue(update.Error);
        update.Value!.CurrentStatus.Should().Be(JobStatus.Delivered);

        await using var verify = await factory.CreateDbContextAsync();
        var job = await verify.ServiceJobs.SingleAsync();
        job.Status.Should().Be(JobStatus.Delivered);

        (await verify.JobDeliveries.CountAsync(d => d.ServiceJobId == jobId)).Should().Be(1, "teslim kaydı çoğalmaz");
        var delivery = await verify.JobDeliveries.SingleAsync(d => d.ServiceJobId == jobId);
        delivery.DeliveredBy.Should().Be("Veli Usta");
        delivery.PaidAmount.Should().Be(1500m);
        delivery.PaymentStatus.Should().Be(PaymentStatus.Partial);
        delivery.InvoiceNumber.Should().Be("INV-2");
        delivery.CustomerSignature.Should().Be("yeni-imza");

        // Güncelleme ayrı bir tarihçe satırı üretir
        (await verify.ServiceJobHistories.CountAsync(h => h.ServiceJobId == jobId && h.Action == "DeliveryCompleted"))
            .Should().Be(2);
    }

    [Fact]
    public async Task CompleteDeliveryAsync_RejectsNegativeAmount()
    {
        var (service, factory, customerId) = CreateService();
        var jobId = await SetupDeliverableJobAsync(service, factory, customerId);

        var result = await service.CompleteDeliveryAsync(new CompleteDeliveryRequest(
            jobId, "Ali", "Not", null,
            PaymentStatus.Paid, PaymentMethod.Cash, -10m, null, "test-user"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("negatif");
    }

    // ───────────────────────────── Fixture ─────────────────────────────

    private static async Task<int> SetupDeliverableJobAsync(
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

        await service.SaveInstallationAsync(new SaveInstallationRequest(
            save.Value!.JobId, null, "Ali Usta", null, null, 4m,
            [new InstallationMaterialInput(null, 1, "Kamera", 2m, 1000m, null)],
            [], "test-user"));
        await service.CompleteInstallationAsync(new CompleteInstallationRequest(
            save.Value!.JobId,
            DeliveryNote: "Cihaz kuruldu.",
            CompletionTechnician: "Ali Usta",
            CustomerSignature: null,
            LaborHours: 4m,
            ChangedBy: "test-user"));

        return save.Value!.JobId;
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
                CustomerCode = "TEST-004",
                PhoneNumber = "0532 111 22 33"
            };
            seed.Customers.Add(customer);
            seed.Products.Add(new Product
            {
                Id = 1,
                ProductName = "Kamera",
                SKU = "CAM-004",
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
