using System;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;
using KamatekCrm.ViewModels;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Services;

/// <summary>
/// İş Emri Çalışma Alanı'nın saf mantığı: durum → yaşam döngüsü aşaması eşlemesi
/// ve "sıradaki işlem" çözücüsü.
/// </summary>
public sealed class WorkOrderWorkspaceTests
{
    [Theory]
    [InlineData(JobStatus.Pending, 0)]
    [InlineData(JobStatus.InProgress, 0)]
    [InlineData(JobStatus.WaitingForParts, 0)]
    [InlineData(JobStatus.WaitingForApproval, 0)]
    [InlineData(JobStatus.DiscoveryRequest, 1)]
    [InlineData(JobStatus.PendingDiscovery, 1)]
    [InlineData(JobStatus.DiscoveryCompleted, 1)]
    [InlineData(JobStatus.ConvertedToQuote, 2)]
    [InlineData(JobStatus.Quoting, 2)]
    [InlineData(JobStatus.Rejected, 2)]
    [InlineData(JobStatus.InstallationPlanned, 3)]
    [InlineData(JobStatus.InstallationCompleted, 4)]
    [InlineData(JobStatus.Delivered, 5)]
    [InlineData(JobStatus.Completed, 5)]
    public void MapStageIndex_MapsEveryStatusToLifecycleStage(JobStatus status, int expectedStage)
    {
        WorkOrderWorkspaceViewModel.MapStageIndex(status).Should().Be(expectedStage);
    }

    [Theory]
    [InlineData(JobStatus.DiscoveryRequest, "Keşif Talebi")]
    [InlineData(JobStatus.ConvertedToQuote, "Teklife Dönüştürüldü")]
    [InlineData(JobStatus.InstallationPlanned, "Montaj Yapılacak")]
    [InlineData(JobStatus.InstallationCompleted, "Montaj Tamamlandı")]
    [InlineData(JobStatus.Delivered, "Teslim Edildi")]
    [InlineData(JobStatus.Pending, "Bekliyor")]
    [InlineData(JobStatus.InProgress, "Devam Ediyor")]
    [InlineData(JobStatus.WaitingForParts, "Parça Bekleniyor")]
    [InlineData(JobStatus.WaitingForApproval, "Onay Bekleniyor")]
    [InlineData(JobStatus.Completed, "Tamamlandı")]
    [InlineData(JobStatus.Cancelled, "İptal Edildi")]
    public void MapStatusDisplay_RendersTurkishLabelForEveryStatus(JobStatus status, string expectedPart)
    {
        ServiceJobRowDto.MapStatusDisplay(status).Should().Contain(expectedPart);
    }

    [Fact]
    public async Task InitializeAsync_ReflectsLiveWorkflowStatusOverStaleRow()
    {
        // Satır "Pending" olarak açılır; workflow canlı durumu "InstallationPlanned" döndürür.
        var row = new ServiceJobRowDto { Id = 42, Status = JobStatus.Pending };
        var workflow = new WorkOrderWorkflowDto(42, JobStatus.InstallationPlanned, null, null, null);

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkOrderWorkflowAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workflow));
        read.Setup(r => r.GetMaterialsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));
        read.Setup(r => r.GetHistoryAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobHistoryDto>>(Array.Empty<ServiceJobHistoryDto>()));

        var pdf = new PdfService(
            new Mock<IPersonalDataProtectionService>().Object,
            new Mock<IAuditTrailService>().Object);

        var vm = new WorkOrderWorkspaceViewModel(
            row,
            read.Object,
            new Mock<IServiceJobCommandService>().Object,
            pdf,
            new Mock<IDialogService>().Object,
            new Mock<IToastService>().Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        // Workspace canlı durumu yansıtmalı — bayat satırı değil (içeriden yapılan
        // teklif kabulü / montaj planlama sonrası süreç göstergesi ve rozet doğru kalmalı).
        vm.StatusDisplay.Should().Contain("Montaj Yapılacak");
        vm.CurrentStageName.Should().Be("Montaj");
        vm.NextAction.Should().Contain("Montajı uygula");
    }

    [Fact]
    public void ResolveNextAction_GuidesWithoutQuote()
    {
        WorkOrderWorkspaceViewModel.ResolveNextAction(JobStatus.DiscoveryRequest, null)
            .Should().Be("Keşif randevusu planla");
        WorkOrderWorkspaceViewModel.ResolveNextAction(JobStatus.DiscoveryCompleted, null)
            .Should().Contain("Teklif oluştur");
        WorkOrderWorkspaceViewModel.ResolveNextAction(JobStatus.InstallationPlanned, null)
            .Should().Contain("Montajı uygula");
        WorkOrderWorkspaceViewModel.ResolveNextAction(JobStatus.InstallationCompleted, null)
            .Should().Contain("teslim et");
        WorkOrderWorkspaceViewModel.ResolveNextAction(JobStatus.Delivered, null)
            .Should().Contain("teslim edildi");
        WorkOrderWorkspaceViewModel.ResolveNextAction(JobStatus.Cancelled, null)
            .Should().Contain("iptal");
    }

    [Fact]
    public void ResolveNextAction_FollowsQuotationStatus()
    {
        var draft = new WorkOrderWorkflowDto(1, JobStatus.ConvertedToQuote, null, MakeQuote(QuotationStatus.Draft), null);
        var sent = new WorkOrderWorkflowDto(1, JobStatus.ConvertedToQuote, null, MakeQuote(QuotationStatus.Sent), null);
        var accepted = new WorkOrderWorkflowDto(1, JobStatus.ConvertedToQuote, null, MakeQuote(QuotationStatus.Accepted), null);
        var rejected = new WorkOrderWorkflowDto(1, JobStatus.ConvertedToQuote, null, MakeQuote(QuotationStatus.Rejected), null);

        WorkOrderWorkspaceViewModel.ResolveNextAction(JobStatus.ConvertedToQuote, draft)
            .Should().Contain("gönder");
        WorkOrderWorkspaceViewModel.ResolveNextAction(JobStatus.ConvertedToQuote, sent)
            .Should().Contain("cevabını bekle");
        WorkOrderWorkspaceViewModel.ResolveNextAction(JobStatus.ConvertedToQuote, accepted)
            .Should().Contain("Montajı planla");
        WorkOrderWorkspaceViewModel.ResolveNextAction(JobStatus.ConvertedToQuote, rejected)
            .Should().Contain("Revizyon");
    }

    [Fact]
    public void ResolveDiscoveryAccess_AllowsEditAndConvertInDiscoveryPhase()
    {
        foreach (var status in new[] { JobStatus.DiscoveryRequest, JobStatus.PendingDiscovery, JobStatus.DiscoveryCompleted, JobStatus.Pending })
        {
            var access = WorkOrderWorkspaceViewModel.ResolveDiscoveryAccess(status, converted: false);
            access.CanEdit.Should().BeTrue();
            access.CanConvertToQuote.Should().BeTrue();
            access.EditDisabledReason.Should().BeEmpty();
        }
    }

    [Fact]
    public void ResolveDiscoveryAccess_BlocksEditAfterConversion()
    {
        var access = WorkOrderWorkspaceViewModel.ResolveDiscoveryAccess(JobStatus.ConvertedToQuote, converted: true);

        access.CanEdit.Should().BeFalse();
        access.CanConvertToQuote.Should().BeFalse();
        access.EditDisabledReason.Should().Contain("salt okunur");
        access.ConvertDisabledReason.Should().Contain("dönüştürülmüş");
    }

    [Fact]
    public void ResolveDiscoveryAccess_BlocksEditOutsideDiscoveryPhase()
    {
        var access = WorkOrderWorkspaceViewModel.ResolveDiscoveryAccess(JobStatus.InstallationPlanned, converted: true);

        access.CanEdit.Should().BeFalse();
        access.CanConvertToQuote.Should().BeFalse();
        access.EditDisabledReason.Should().Contain("yalnızca keşif aşamasında");
    }

    [Fact]
    public void ResolveDiscoveryAccess_BlocksConvertWhenAlreadyConverted()
    {
        var access = WorkOrderWorkspaceViewModel.ResolveDiscoveryAccess(JobStatus.DiscoveryCompleted, converted: true);

        access.CanEdit.Should().BeTrue("keşif raporu hâlâ düzenlenebilir");
        access.CanConvertToQuote.Should().BeFalse("ikinci dönüştürme yapılamaz");
        access.ConvertDisabledReason.Should().Contain("dönüştürülmüş");
    }

    [Fact]
    public async Task InitializeAsync_ReflectsInstallationCompletionValidation()
    {
        // Montaj emri: 1 malzeme + işçilik saati > 0 → tamamlanmaya hazır.
        var row = new ServiceJobRowDto { Id = 7, Status = JobStatus.InstallationPlanned };
        var workflow = new WorkOrderWorkflowDto(
            7,
            JobStatus.InstallationPlanned,
            null,
            null,
            new InstallationOrderDto(
                Id: 3,
                ServiceJobId: 7,
                QuotationId: 2,
                TechnicianId: null,
                TechnicianName: "Ali Usta",
                InstallationDate: new DateTime(2026, 8, 10),
                Notes: "Kablo güzergahı hazır",
                LaborHours: 4m,
                CompletedAt: null,
                CompletionTechnician: null,
                DeliveryNote: null,
                CustomerSignature: null,
                Materials: new List<InstallationMaterialDto> { new(1, 1, "Kamera", 2m, 1000m, null) },
                Tasks: new List<InstallationTaskDto>()));

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkOrderWorkflowAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workflow));
        read.Setup(r => r.GetMaterialsAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));
        read.Setup(r => r.GetHistoryAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobHistoryDto>>(Array.Empty<ServiceJobHistoryDto>()));

        var vm = new WorkOrderWorkspaceViewModel(
            row,
            read.Object,
            new Mock<IServiceJobCommandService>().Object,
            new PdfService(new Mock<IPersonalDataProtectionService>().Object, new Mock<IAuditTrailService>().Object),
            new Mock<IDialogService>().Object,
            new Mock<IToastService>().Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        vm.HasInstallation.Should().BeTrue();
        vm.IsInstallationCompleted.Should().BeFalse();
        vm.InstallationReadyToComplete.Should().BeTrue("malzeme + işçilik saati yeterli");
        vm.InstallationLaborHoursDisplay.Should().Contain("4");
        vm.InstallationMaterialSummary.Should().Contain("1 kalem");
        vm.CanEditInstallation.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_BlocksInstallationCompletionWhenLaborHoursMissing()
    {
        var row = new ServiceJobRowDto { Id = 8, Status = JobStatus.InstallationPlanned };
        var workflow = new WorkOrderWorkflowDto(
            8,
            JobStatus.InstallationPlanned,
            null,
            null,
            new InstallationOrderDto(
                Id: 4,
                ServiceJobId: 8,
                QuotationId: 2,
                TechnicianId: null,
                TechnicianName: null,
                InstallationDate: null,
                Notes: null,
                LaborHours: 0m,
                CompletedAt: null,
                CompletionTechnician: null,
                DeliveryNote: null,
                CustomerSignature: null,
                Materials: new List<InstallationMaterialDto> { new(2, 1, "Kamera", 2m, 1000m, null) },
                Tasks: new List<InstallationTaskDto>()));

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkOrderWorkflowAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workflow));
        read.Setup(r => r.GetMaterialsAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));
        read.Setup(r => r.GetHistoryAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobHistoryDto>>(Array.Empty<ServiceJobHistoryDto>()));

        var vm = new WorkOrderWorkspaceViewModel(
            row,
            read.Object,
            new Mock<IServiceJobCommandService>().Object,
            new PdfService(new Mock<IPersonalDataProtectionService>().Object, new Mock<IAuditTrailService>().Object),
            new Mock<IDialogService>().Object,
            new Mock<IToastService>().Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        vm.InstallationReadyToComplete.Should().BeFalse("işçilik saati girilmeden tamamlanamaz");
        vm.InstallationCompletionSummary.Should().Contain("işçilik saati");
        vm.InstallationLaborHoursDisplay.Should().Contain("0");
    }

    [Fact]
    public async Task InitializeAsync_LoadsDeliverySummary()
    {
        // Teslim kaydı olan iş: durum Delivered, ödeme bilgileri sekmede görünür.
        var row = new ServiceJobRowDto { Id = 9, Status = JobStatus.Delivered };
        var workflow = new WorkOrderWorkflowDto(
            9,
            JobStatus.Delivered,
            null,
            null,
            null,
            null,
            new JobDeliveryDto(
                Id: 5,
                ServiceJobId: 9,
                DeliveryDate: new DateTime(2026, 8, 5, 16, 0, 0, DateTimeKind.Utc),
                DeliveredBy: "Ali Usta",
                DeliveryNote: "Cihaz teslim edildi.",
                CustomerSignature: "imza",
                PaymentStatus: PaymentStatus.Paid,
                PaymentMethod: PaymentMethod.CreditCard,
                PaidAmount: 2700m,
                InvoiceNumber: "INV-2026-001"));

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkOrderWorkflowAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workflow));
        read.Setup(r => r.GetMaterialsAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));
        read.Setup(r => r.GetHistoryAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobHistoryDto>>(Array.Empty<ServiceJobHistoryDto>()));

        var vm = new WorkOrderWorkspaceViewModel(
            row,
            read.Object,
            new Mock<IServiceJobCommandService>().Object,
            new PdfService(new Mock<IPersonalDataProtectionService>().Object, new Mock<IAuditTrailService>().Object),
            new Mock<IDialogService>().Object,
            new Mock<IToastService>().Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        vm.IsDelivered.Should().BeTrue();
        vm.DeliveredByDisplay.Should().Be("Ali Usta");
        vm.PaymentStatusDisplay.Should().Contain("Ödendi");
        vm.PaidAmountDisplay.Should().Contain("2.700");
        vm.InvoiceNumberDisplay.Should().Be("INV-2026-001");
        vm.CanOpenDeliveryEditor.Should().BeTrue("teslim kaydı görüntülenebilir");
        vm.DeliveryStatusLine.Should().Contain("teslim edildi");
    }

    private static WorkOrderQuotationDto MakeQuote(QuotationStatus status) => new(
        Id: 1,
        ServiceJobId: 1,
        QuotationNumber: "TEK-20260805-1",
        Status: status,
        IssuedDate: DateTime.UtcNow,
        ValidUntil: null,
        Description: null,
        Warranty: null,
        DeliveryTime: null,
        PaymentTerms: null,
        LaborCost: 0m,
        ShippingCost: 0m,
        DiscountAmount: 0m,
        TaxRate: 20m,
        TaxAmount: 0m,
        TotalAmount: 0m,
        SentDate: null,
        AcceptedAt: null,
        RejectedAt: null,
        RejectionReason: null,
        Items: new List<QuotationItemDto>());
}
