using FluentAssertions;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace KamatekCrm.Tests.Services;

public sealed class ServiceJobReadServiceTests
{
    [Fact]
    public async Task ReadModels_ReturnWorkspaceJobDetailDashboardAndDocumentWithoutTracking()
    {
        var (service, factory, options, jobId, customerId) = await CreateServiceAsync();

        var workspace = await service.GetWorkspaceAsync();
        var jobs = await service.SearchAsync(new ServiceJobSearchRequest("kamera", JobStatus.InProgress, null, null));
        var assets = await service.GetCustomerAssetsAsync(customerId);
        var projects = await service.GetCustomerProjectsAsync(customerId);
        var materials = await service.GetMaterialsAsync(jobId);
        var history = await service.GetHistoryAsync(jobId);
        var dashboard = await service.GetDashboardAsync();
        var document = await service.GetDocumentAsync(jobId);

        workspace.IsSuccess.Should().BeTrue(workspace.Error);
        workspace.Value!.Customers.Should().ContainSingle(item => item.FullName == "Servis Müşterisi");
        workspace.Value.Products.Should().ContainSingle(item => item.ProductName == "Kamera");
        workspace.Value.Technicians.Should().ContainSingle(item => item.FullName == "Teknik Personel");
        jobs.Value.Should().ContainSingle(item => item.Id == jobId && item.CustomerFullName == "Servis Müşterisi");
        assets.Value.Should().ContainSingle(item => item.FullName == "Marka Model");
        projects.Value.Should().ContainSingle(item => item.Name == "Servis Projesi");
        materials.Value.Should().ContainSingle(item => item.ProductName == "Kamera" && item.QuantityUsed == 2);
        history.Value.Should().ContainSingle(item => item.Action == "Created");
        dashboard.Value!.InProgressCount.Should().Be(1);
        document.Value!.CustomerPhone.Should().Be("0532 111 22 33");
        document.Value.CustomerAddress.Should().Contain("Kadıköy");

        await using var verify = new AppDbContext(options);
        verify.ChangeTracker.Entries().Should().BeEmpty();
        factory.Verify(item => item.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Exactly(8));
    }

    [Fact]
    public async Task SearchAsync_WhenUnauthorized_ReturnsFailureWithoutData()
    {
        var (_, factory, _, _, _) = await CreateServiceAsync();
        var service = new ServiceJobReadService(factory.Object, new TestAuthorizationService(isAuthorized: false));

        var result = await service.SearchAsync(new ServiceJobSearchRequest(null, null, null, null));

        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    private static async Task<(ServiceJobReadService Service, Mock<IDbContextFactory<AppDbContext>> Factory,
        DbContextOptions<AppDbContext> Options, int JobId, int CustomerId)> CreateServiceAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        int jobId;
        int customerId;
        await using (var seed = new AppDbContext(options))
        {
            var customer = new Customer
            {
                FullName = "Servis Müşterisi",
                CustomerCode = "SJ-READ",
                PhoneNumber = "0532 111 22 33",
                City = "İstanbul",
                District = "Kadıköy",
                Street = "Test Sokak"
            };
            var product = new Product { ProductName = "Kamera", SKU = "CAM-READ", SalePrice = 100m, PurchasePrice = 60m };
            var technician = new User
            {
                Username = "teknik",
                PasswordHash = "test",
                Role = "Personel",
                Ad = "Teknik",
                Soyad = "Personel",
                IsActive = true
            };
            seed.AddRange(customer, product, technician);
            await seed.SaveChangesAsync();
            customerId = customer.Id;
            var asset = new CustomerAsset { CustomerId = customer.Id, Brand = "Marka", Model = "Model", Category = JobCategory.CCTV };
            var project = new ServiceProject { CustomerId = customer.Id, Name = "Servis Projesi" };
            var job = new ServiceJob
            {
                CustomerId = customer.Id,
                Customer = customer,
                Description = "Kamera kurulumu",
                Status = JobStatus.InProgress,
                Priority = JobPriority.Urgent,
                WorkOrderType = WorkOrderType.Repair,
                CreatedDate = DateTime.UtcNow.AddHours(-2),
                ScheduledDate = DateTime.UtcNow.AddHours(2),
                AssignedTechnician = technician.FullName
            };
            seed.AddRange(asset, project, job);
            await seed.SaveChangesAsync();
            jobId = job.Id;
            seed.ServiceJobItems.Add(new ServiceJobItem
            {
                ServiceJobId = job.Id,
                ProductId = product.Id,
                QuantityUsed = 2,
                UnitPrice = 100m,
                UnitCost = 60m
            });
            seed.ServiceJobHistories.Add(new ServiceJobHistory
            {
                ServiceJobId = job.Id,
                Date = DateTime.UtcNow,
                TechnicianNote = "Oluşturuldu",
                Action = "Created"
            });
            await seed.SaveChangesAsync();
        }

        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(item => item.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AppDbContext(options));
        var service = new ServiceJobReadService(factory.Object, new TestAuthorizationService());
        return (service, factory, options, jobId, customerId);
    }
}
