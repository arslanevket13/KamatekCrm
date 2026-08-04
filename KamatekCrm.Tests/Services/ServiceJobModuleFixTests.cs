using System;
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

public sealed class ServiceJobModuleFixTests
{
    [Fact]
    public async Task SearchAsync_WithIsSlaBreachedOnly_ReturnsOnlyBreachedNonCompletedJobs()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SlaTestDb_{Guid.NewGuid()}")
            .Options;

        await using var context = new AppDbContext(options);

        var customer = new Customer
        {
            CustomerCode = "CUST-SLA-01",
            FullName = "SLA Test Müşterisi",
            PhoneNumber = "05550001122",
            City = "İstanbul"
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var expiredJob = new ServiceJob
        {
            CustomerId = customer.Id,
            WorkOrderType = WorkOrderType.Repair,
            Status = JobStatus.Pending,
            Priority = JobPriority.High,
            Description = "SLA Süresi Dolmuş İş",
            SlaDeadline = DateTime.UtcNow.AddHours(-5),
            CreatedDate = DateTime.UtcNow.AddDays(-2)
        };

        var normalJob = new ServiceJob
        {
            CustomerId = customer.Id,
            WorkOrderType = WorkOrderType.Repair,
            Status = JobStatus.Pending,
            Priority = JobPriority.Normal,
            Description = "SLA Zamanında İş",
            SlaDeadline = DateTime.UtcNow.AddHours(24),
            CreatedDate = DateTime.UtcNow
        };

        var completedExpiredJob = new ServiceJob
        {
            CustomerId = customer.Id,
            WorkOrderType = WorkOrderType.Repair,
            Status = JobStatus.Completed,
            Priority = JobPriority.Urgent,
            Description = "SLA Dolmuş Tamamlanmış İş",
            SlaDeadline = DateTime.UtcNow.AddHours(-10),
            CompletedDate = DateTime.UtcNow.AddHours(-1),
            CreatedDate = DateTime.UtcNow.AddDays(-3)
        };

        context.ServiceJobs.AddRange(expiredJob, normalJob, completedExpiredJob);
        await context.SaveChangesAsync();

        var factoryMock = new Mock<IDbContextFactory<AppDbContext>>();
        factoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AppDbContext(options));

        var readService = new ServiceJobReadService(factoryMock.Object, new TestAuthorizationService(isAuthorized: true));

        // Act
        var result = await readService.SearchAsync(new ServiceJobSearchRequest(
            SearchText: null,
            Status: null,
            StartDate: null,
            EndDate: null,
            Take: 50,
            IsSlaBreachedOnly: true));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().ContainSingle(j => j.Id == expiredJob.Id);
        result.Value.Should().NotContain(j => j.Id == normalJob.Id);
        result.Value.Should().NotContain(j => j.Id == completedExpiredJob.Id);
    }

    [Fact]
    public void StatusFilter_SlaBreached_EnumValuation_ShouldBeDefined()
    {
        Enum.IsDefined(typeof(StatusFilter), StatusFilter.SlaBreached).Should().BeTrue();
    }

    [Fact]
    public void ChangeServiceJobStatusCommandParameter_Record_ShouldBindCorrectly()
    {
        var row = new ServiceJobRowDto
        {
            Id = 42,
            CustomerFullName = "Ahmet Yılmaz",
            Status = JobStatus.Pending
        };

        var param = new ChangeServiceJobStatusCommandParameter(row, JobStatus.InProgress);

        param.Job.Should().Be(row);
        param.Status.Should().Be(JobStatus.InProgress);
    }

    [Fact]
    public void NewWorkflowEnumValues_ShouldBeDefined()
    {
        Enum.IsDefined(typeof(JobStatus), JobStatus.DiscoveryRequest).Should().BeTrue();
        Enum.IsDefined(typeof(JobStatus), JobStatus.ConvertedToQuote).Should().BeTrue();
        Enum.IsDefined(typeof(JobStatus), JobStatus.InstallationPlanned).Should().BeTrue();
        Enum.IsDefined(typeof(JobStatus), JobStatus.InstallationCompleted).Should().BeTrue();
    }
}
