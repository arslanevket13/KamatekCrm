using FluentAssertions;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Models.WorkOrders;
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
    public async Task GetQuotationRevisionsAsync_ReturnsHistoryNewestFirstWithCurrentFlag()
    {
        var (service, factory, options, jobId, _) = await CreateServiceAsync();

        await using (var seed = new AppDbContext(options))
        {
            seed.WorkOrderQuotations.AddRange(
                new WorkOrderQuotation
                {
                    ServiceJobId = jobId,
                    QuotationNumber = $"TEK-{jobId}-0",
                    RevisionNumber = 0,
                    Status = QuotationStatus.Rejected,
                    IssuedDate = DateTime.UtcNow.AddDays(-6),
                    TotalAmount = 1000m
                },
                new WorkOrderQuotation
                {
                    ServiceJobId = jobId,
                    QuotationNumber = $"TEK-{jobId}-1",
                    RevisionNumber = 1,
                    Status = QuotationStatus.Sent,
                    IssuedDate = DateTime.UtcNow.AddDays(-3),
                    TotalAmount = 1100m,
                    SentDate = DateTime.UtcNow.AddDays(-3)
                },
                new WorkOrderQuotation
                {
                    ServiceJobId = jobId,
                    QuotationNumber = $"TEK-{jobId}-2",
                    RevisionNumber = 2,
                    Status = QuotationStatus.Accepted,
                    IssuedDate = DateTime.UtcNow.AddDays(-1),
                    TotalAmount = 1200m,
                    AcceptedAt = DateTime.UtcNow,
                    ParentQuotationId = null
                });
            await seed.SaveChangesAsync();
        }

        var result = await service.GetQuotationRevisionsAsync(jobId);

        result.IsSuccess.Should().BeTrue(result.Error);
        var revisions = result.Value!;
        revisions.Select(r => r.RevisionNumber).Should().Equal(2, 1, 0);
        revisions.Single(r => r.IsCurrent).RevisionNumber.Should().Be(2);
        revisions.Single(r => r.RevisionNumber == 2).Status.Should().Be(QuotationStatus.Accepted);
        revisions.Single(r => r.RevisionNumber == 0).AcceptedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetQuotationByIdAsync_ReturnsRevisionWithItemsOrderedBySequence()
    {
        var (service, factory, options, jobId, _) = await CreateServiceAsync();
        int quotationId;

        await using (var seed = new AppDbContext(options))
        {
            var quotation = new WorkOrderQuotation
            {
                ServiceJobId = jobId,
                QuotationNumber = $"TEK-{jobId}-1",
                RevisionNumber = 1,
                Status = QuotationStatus.Draft,
                IssuedDate = DateTime.UtcNow,
                TotalAmount = 250m
            };
            seed.WorkOrderQuotations.Add(quotation);
            await seed.SaveChangesAsync();
            quotationId = quotation.Id;
            seed.QuotationItems.AddRange(
                new QuotationItem { QuotationId = quotationId, ProductName = "Kablo", Quantity = 50m, UnitPrice = 10m, Sequence = 1, LineTotal = 500m },
                new QuotationItem { QuotationId = quotationId, ProductName = "Kamera", Quantity = 2m, UnitPrice = 1000m, Sequence = 0, LineTotal = 2000m });
            await seed.SaveChangesAsync();
        }

        var result = await service.GetQuotationByIdAsync(quotationId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.RevisionNumber.Should().Be(1);
        result.Value.Items.Select(i => i.ProductName).Should().Equal("Kamera", "Kablo");
        result.Value.Items.First().Sequence.Should().Be(0);

        var missing = await service.GetQuotationByIdAsync(999999);
        missing.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SearchAsync_WhenUnauthorized_ReturnsFailureWithoutData()
    {
        var (_, factory, _, _, _) = await CreateServiceAsync();
        var service = new ServiceJobReadService(factory.Object, new TestAuthorizationService(isAuthorized: false), new KamatekCrm.ApplicationCore.Services.WorkOrderNextActionResolver());

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
        var service = new ServiceJobReadService(factory.Object, new TestAuthorizationService(), new KamatekCrm.ApplicationCore.Services.WorkOrderNextActionResolver());
        return (service, factory, options, jobId, customerId);
    }
}
