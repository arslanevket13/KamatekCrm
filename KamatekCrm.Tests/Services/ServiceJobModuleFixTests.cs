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

        var readService = new ServiceJobReadService(factoryMock.Object, new TestAuthorizationService(isAuthorized: true), new KamatekCrm.ApplicationCore.Services.WorkOrderNextActionResolver());

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

    [Fact]
    public void ServiceJobRowDto_WorkflowProperties_BindCorrectly()
    {
        var row = new ServiceJobRowDto
        {
            Id = 10,
            Status = JobStatus.ConvertedToQuote,
            DiscoveryReportId = 1,
            QuotationId = 5,
            QuotationStatus = QuotationStatus.Accepted,
            InstallationOrderId = 12,
            IsInstallationCompleted = false
        };

        row.DiscoveryReportId.Should().Be(1);
        row.QuotationId.Should().Be(5);
        row.QuotationStatus.Should().Be(QuotationStatus.Accepted);
        row.InstallationOrderId.Should().Be(12);
        row.IsInstallationCompleted.Should().BeFalse();
    }

    [Fact]
    public void ServiceJobRowDto_WorkflowCommandEnablement_ReflectsStageRules()
    {
        // Keşif aşaması, henüz teklif yok → yalnızca teklife dönüştürme aktif
        var discovery = new ServiceJobRowDto
        {
            Id = 1,
            Status = JobStatus.DiscoveryRequest,
            QuotationId = null,
            QuotationStatus = null,
            InstallationOrderId = null,
            IsInstallationCompleted = false
        };
        discovery.CanConvertToQuote.Should().BeTrue();
        discovery.CanEditQuote.Should().BeFalse();
        discovery.CanAcceptQuote.Should().BeFalse();
        discovery.CanSetInstallationPlanned.Should().BeFalse();
        discovery.CanSetInstallationCompleted.Should().BeFalse();
        discovery.CanCancelJob.Should().BeTrue();
        discovery.ConvertToQuoteDisabledReason.Should().NotBeNullOrWhiteSpace();

        // Teklife dönüştürülmüş, taslak teklif var → teklif işlemleri aktif, montaj kapalı
        var quoted = new ServiceJobRowDto
        {
            Id = 2,
            Status = JobStatus.ConvertedToQuote,
            QuotationId = 10,
            QuotationStatus = QuotationStatus.Draft,
            InstallationOrderId = null,
            IsInstallationCompleted = false
        };
        quoted.CanConvertToQuote.Should().BeFalse("zaten teklife dönüştürülmüş");
        quoted.CanEditQuote.Should().BeTrue();
        quoted.CanAcceptQuote.Should().BeTrue();
        quoted.CanRejectQuote.Should().BeTrue();
        quoted.CanSetInstallationPlanned.Should().BeFalse("teklif henüz kabul edilmedi");

        // Kabul edilmiş teklif → montaj planlama aktif
        var accepted = new ServiceJobRowDto
        {
            Id = 3,
            Status = JobStatus.ConvertedToQuote,
            QuotationId = 11,
            QuotationStatus = QuotationStatus.Accepted,
            InstallationOrderId = null,
            IsInstallationCompleted = false
        };
        accepted.CanSetInstallationPlanned.Should().BeTrue();
        accepted.CanEditQuote.Should().BeFalse("kabul edilmiş teklif düzenlenemez");
        accepted.CanAcceptQuote.Should().BeFalse();

        // Montaj planlanmış → montaj tamamlama aktif; kabul edilmiş teklif tekrar planlanamaz
        var planned = new ServiceJobRowDto
        {
            Id = 4,
            Status = JobStatus.InstallationPlanned,
            QuotationId = 12,
            QuotationStatus = QuotationStatus.Accepted,
            InstallationOrderId = 20,
            IsInstallationCompleted = false
        };
        planned.CanSetInstallationPlanned.Should().BeFalse("montaj zaten planlanmış");
        planned.CanSetInstallationCompleted.Should().BeTrue();
        planned.CanCancelJob.Should().BeTrue();

        // Montaj tamamlanmış → iptal ve silme kapalı
        var done = new ServiceJobRowDto
        {
            Id = 5,
            Status = JobStatus.InstallationCompleted,
            QuotationId = 13,
            QuotationStatus = QuotationStatus.Accepted,
            InstallationOrderId = 21,
            IsInstallationCompleted = true
        };
        done.CanSetInstallationCompleted.Should().BeFalse();
        done.CanCancelJob.Should().BeFalse();
        done.CanDeleteJob.Should().BeFalse();
    }
}
