using FluentAssertions;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace KamatekCrm.Tests.Services;

public class ServiceJobCommandServiceTests
{
    [Fact]
    public async Task SaveAsync_StoresItemsAndCreatesStockReservationAtomically()
    {
        var (service, factory, customerId) = CreateService(stockQuantity: 10);
        var job = NewJob(customerId, JobStatus.Pending);
        var item = NewItem(quantity: 3);

        var result = await service.SaveAsync(new ServiceJobSaveRequest(job, [item], false, "test-user"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsStockReserved.Should().BeTrue();
        result.Value.ReservationCount.Should().Be(1);

        await using var verify = await factory.CreateDbContextAsync();
        (await verify.ServiceJobs.CountAsync()).Should().Be(1);
        (await verify.ServiceJobItems.SingleAsync()).QuantityUsed.Should().Be(3);
        (await verify.StockReservations.SingleAsync()).Quantity.Should().Be(3);
        (await verify.Inventories.SingleAsync()).Quantity.Should().Be(10, "rezervasyon fiziksel stoğu henüz düşürmez");
    }

    [Fact]
    public async Task SaveAsync_WithInsufficientStock_DoesNotCreatePartialJob()
    {
        var (service, factory, customerId) = CreateService(stockQuantity: 2);
        var job = NewJob(customerId, JobStatus.Pending);

        var result = await service.SaveAsync(new ServiceJobSaveRequest(job, [NewItem(quantity: 3)], false, "test-user"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("yeterli kullanılabilir stok yok");

        await using var verify = await factory.CreateDbContextAsync();
        (await verify.ServiceJobs.CountAsync()).Should().Be(0);
        (await verify.ServiceJobItems.CountAsync()).Should().Be(0);
        (await verify.StockReservations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ChangeStatusAsync_RejectsSkippingDirectlyFromPendingToCompleted()
    {
        var (service, factory, customerId) = CreateService(stockQuantity: 5);
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewJob(customerId, JobStatus.Pending),
            [NewItem(quantity: 1)],
            false,
            "test-user"));

        var result = await service.ChangeStatusAsync(save.Value!.JobId, JobStatus.Completed, "test-user");

        result.IsFailure.Should().BeTrue();
        await using var verify = await factory.CreateDbContextAsync();
        (await verify.ServiceJobs.SingleAsync()).Status.Should().Be(JobStatus.Pending);
        (await verify.Inventories.SingleAsync()).Quantity.Should().Be(5);
    }

    [Fact]
    public async Task ChangeStatusAsync_CompletesReservedJobExactlyOnce()
    {
        var (service, factory, customerId) = CreateService(stockQuantity: 5);
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewJob(customerId, JobStatus.InProgress),
            [NewItem(quantity: 2)],
            false,
            "test-user"));

        var first = await service.ChangeStatusAsync(save.Value!.JobId, JobStatus.Completed, "test-user");
        var second = await service.ChangeStatusAsync(save.Value.JobId, JobStatus.Completed, "test-user");

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();

        await using var verify = await factory.CreateDbContextAsync();
        var storedJob = await verify.ServiceJobs.SingleAsync();
        storedJob.IsStockDeducted.Should().BeTrue();
        storedJob.IsStockReserved.Should().BeFalse();
        (await verify.Inventories.SingleAsync()).Quantity.Should().Be(3);
        (await verify.StockReservations.SingleAsync()).IsActive.Should().BeFalse();
        (await verify.ServiceJobHistories.CountAsync()).Should().Be(1);
        (await verify.CustomerActivities.CountAsync()).Should().Be(1);
        (await verify.Customers.SingleAsync()).TotalSpent.Should().Be(240m);
    }

    [Fact]
    public async Task CompleteAsync_PersistsRepairCostsAndCompletionNoteInSameWorkflow()
    {
        var (service, factory, customerId) = CreateService(stockQuantity: 5);
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewJob(customerId, JobStatus.InProgress),
            [NewItem(quantity: 1)],
            false,
            "test-user"));

        var result = await service.CompleteAsync(
            save.Value!.JobId,
            laborCost: 850m,
            discountAmount: 50m,
            completionNote: "Cihaz test edilerek teslim edildi.",
            changedBy: "technician");

        result.IsSuccess.Should().BeTrue();
        await using var verify = await factory.CreateDbContextAsync();
        var job = await verify.ServiceJobs.SingleAsync();
        job.LaborCost.Should().Be(850m);
        job.DiscountAmount.Should().Be(50m);
        job.RepairStatus.Should().Be(RepairStatus.Delivered);
        (await verify.ServiceJobHistories.SingleAsync()).TechnicianNote
            .Should().Be("Cihaz test edilerek teslim edildi.");
    }

    [Fact]
    public async Task ConvertToQuoteAsync_IsPolicyCheckedAndIdempotent()
    {
        var (service, factory, customerId) = CreateService(stockQuantity: 5);
        var save = await service.SaveAsync(new ServiceJobSaveRequest(
            NewJob(customerId, JobStatus.DiscoveryCompleted),
            [],
            false,
            "test-user"));

        var first = await service.ConvertToQuoteAsync(save.Value!.JobId, "test-user");
        var second = await service.ConvertToQuoteAsync(save.Value.JobId, "test-user");

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value!.CustomerId.Should().Be(customerId);

        await using var verify = await factory.CreateDbContextAsync();
        var job = await verify.ServiceJobs.SingleAsync();
        job.Status.Should().Be(JobStatus.Quoting);
        job.IsConvertedToQuote.Should().BeTrue();
        (await verify.ServiceJobHistories.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_WhenUserIsUnauthorized_DoesNotCreateJobOrReservation()
    {
        var (service, factory, customerId) = CreateService(stockQuantity: 5, isAuthorized: false);

        var result = await service.SaveAsync(new ServiceJobSaveRequest(
            NewJob(customerId, JobStatus.Pending),
            [NewItem(quantity: 1)],
            false,
            "spoofed-user"));

        result.IsFailure.Should().BeTrue();
        await using var verify = await factory.CreateDbContextAsync();
        (await verify.ServiceJobs.CountAsync()).Should().Be(0);
        (await verify.StockReservations.CountAsync()).Should().Be(0);
    }

    private static (ServiceJobCommandService Service, IDbContextFactory<AppDbContext> Factory, int CustomerId)
        CreateService(int stockQuantity, bool isAuthorized = true)
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
            seed.Inventories.Add(new Inventory { ProductId = 1, WarehouseId = 1, Quantity = stockQuantity });
            seed.SaveChanges();
            customerId = customer.Id;
        }

        var service = new ServiceJobCommandService(
            factory.Object,
            new ServiceJobStatusPolicy(),
            new TestAuthorizationService(isAuthorized));
        return (service, factory.Object, customerId);
    }

    private static ServiceJob NewJob(int customerId, JobStatus status) => new()
    {
        CustomerId = customerId,
        Description = "Kamera bakımı",
        JobCategory = JobCategory.CCTV,
        WorkOrderType = WorkOrderType.Repair,
        Status = status,
        TotalAmount = 240m,
        CreatedDate = DateTime.UtcNow
    };

    private static ServiceJobItem NewItem(int quantity) => new()
    {
        ProductId = 1,
        QuantityUsed = quantity,
        UnitPrice = 100m,
        UnitCost = 60m
    };
}
