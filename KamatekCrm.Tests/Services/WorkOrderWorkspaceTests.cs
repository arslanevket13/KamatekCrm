using System;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;
using KamatekCrm.ViewModels;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Services;

/// <summary>
/// İş Emri Çalışma Alanı shell + sekme ViewModel'leri. Workspace DTO'su, okuma servisiyle
/// aynı yöntemle (WorkOrderWorkspaceInput → gerçek resolver) üretilir; böylece UI'nin
/// gördüğü NextAction/AllowedActions, servis kurallarıyla birebir aynı kaynaktan gelir.
/// </summary>
public sealed class WorkOrderWorkspaceTests
{
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
    public async Task InitializeAsync_ReflectsLiveWorkspaceStatusOverStaleRow()
    {
        // Satır "Pending" olarak açılır; workspace projeksiyonu canlı durumu "InstallationPlanned" döndürür.
        var row = new ServiceJobRowDto { Id = 42, Status = JobStatus.Pending };
        var workspace = MakeWorkspace(JobStatus.InstallationPlanned, installation: MakeInstallation(laborHours: 4m));

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkspaceAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workspace));
        read.Setup(r => r.GetMaterialsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));

        var vm = CreateVm(row, read.Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        // Workspace canlı durumu yansıtmalı — bayat satırı değil.
        vm.StatusDisplay.Should().Contain("Montaj Yapılacak");
        vm.CurrentStageName.Should().Be("Montaj");
        vm.NextActionTitle.Should().Contain("Montajı uygula");
        vm.NextAction!.Action.Should().Be(WorkOrderAction.EditInstallation);
    }

    [Fact]
    public async Task InitializeAsync_ReflectsInstallationCompletionValidation()
    {
        // Montaj emri: 1 malzeme + işçilik saati > 0 → CompleteInstallation etkin.
        var row = new ServiceJobRowDto { Id = 7, Status = JobStatus.InstallationPlanned };
        var workspace = MakeWorkspace(JobStatus.InstallationPlanned, installation: MakeInstallation(laborHours: 4m));

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkspaceAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workspace));
        read.Setup(r => r.GetMaterialsAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));

        var vm = CreateVm(row, read.Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        vm.InstallationTab.HasInstallation.Should().BeTrue();
        vm.InstallationTab.IsCompleted.Should().BeFalse();
        vm.InstallationTab.LaborHoursDisplay.Should().Contain("4");
        vm.InstallationTab.MaterialSummary.Should().Contain("1 kalem");

        var complete = vm.InstallationTab.GetAction(WorkOrderAction.CompleteInstallation);
        complete.Should().NotBeNull();
        complete!.IsEnabled.Should().BeTrue("malzeme + işçilik saati yeterli");
    }

    [Fact]
    public async Task InitializeAsync_AllowsCompletionWhenPlannedLaborHoursZero()
    {
        // Planlanan işçilik saati 0 olsa bile "Montajı Tamamla" erişilebilirdir: fiili saat
        // tamamlama formunda girilir ve servis (> 0) orada doğrular — buton formu kilitlerken
        // formun kendisine erişilemez kalmaz.
        var row = new ServiceJobRowDto { Id = 8, Status = JobStatus.InstallationPlanned };
        var workspace = MakeWorkspace(JobStatus.InstallationPlanned, installation: MakeInstallation(laborHours: 0m));

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkspaceAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workspace));
        read.Setup(r => r.GetMaterialsAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));

        var vm = CreateVm(row, read.Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        var complete = vm.InstallationTab.GetAction(WorkOrderAction.CompleteInstallation);
        complete.Should().NotBeNull();
        complete!.IsEnabled.Should().BeTrue("işçilik saati tamamlama formundan girilir");
        complete.DisabledReason.Should().BeEmpty();
        vm.InstallationTab.LaborHoursDisplay.Should().Contain("0");
    }

    [Fact]
    public async Task DocumentsTab_BuildsSixDocuments_WithCorrectAvailability()
    {
        // Keşif + taslak teklif + planlanmış (tamamlanmamış) montaj → belge durumları doğru.
        var discovery = new DiscoveryReportDto(
            Id: 1,
            ServiceJobId: 42,
            TechnicalNotes: "Sahada tespit notu",
            RecommendedSolution: "Önerilen çözüm",
            PhotoPaths: Array.Empty<string>(),
            EstimatedLaborHours: 2d,
            TechnicianName: "Teknisyen",
            Materials: new List<DiscoveryMaterialDto>());
        var row = new ServiceJobRowDto { Id = 12, Status = JobStatus.InstallationPlanned };
        var workspace = MakeWorkspace(
            JobStatus.InstallationPlanned,
            discovery: discovery,
            quote: MakeQuote(QuotationStatus.Draft),
            installation: MakeInstallation(laborHours: 4m));

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkspaceAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workspace));
        read.Setup(r => r.GetMaterialsAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));

        var vm = CreateVm(row, read.Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        vm.DocumentsTab.Documents.Should().HaveCount(6);
        vm.DocumentsTab.Documents.Single(d => d.Key == "Discovery").IsAvailable.Should().BeTrue();
        vm.DocumentsTab.Documents.Single(d => d.Key == "Quotation").IsAvailable.Should().BeTrue();
        vm.DocumentsTab.Documents.Single(d => d.Key == "Installation").IsAvailable.Should().BeTrue();
        vm.DocumentsTab.Documents.Single(d => d.Key == "CompletionForm").IsAvailable.Should().BeFalse("montaj tamamlanmadı");
        vm.DocumentsTab.Documents.Single(d => d.Key == "Invoice").IsAvailable.Should().BeFalse("taslak teklif için fatura üretilemez");
        vm.DocumentsTab.Documents.Single(d => d.Key == "ServiceReport").IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task DocumentsTab_AggregatesDiscoveryAndVisitPhotos()
    {
        var discovery = new DiscoveryReportDto(
            Id: 1,
            ServiceJobId: 42,
            TechnicalNotes: "Sahada tespit notu",
            RecommendedSolution: "Önerilen çözüm",
            PhotoPaths: new[] { "C:\\keşif_fotoğrafı.jpg" },
            EstimatedLaborHours: 2d,
            TechnicianName: "Teknisyen",
            Materials: new List<DiscoveryMaterialDto>());
        var visits = new List<DiscoveryVisitDto>
        {
            new(Id: 1, VisitDate: new DateTime(2026, 8, 4), TechnicianName: "Ali", Notes: null,
                PhotoPaths: new[] { "C:\\ziyaret.jpg" })
        };

        var row = new ServiceJobRowDto { Id = 13, Status = JobStatus.DiscoveryCompleted };
        var workspace = MakeWorkspace(JobStatus.DiscoveryCompleted, discovery: discovery, visits: visits);

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkspaceAsync(13, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workspace));
        read.Setup(r => r.GetMaterialsAsync(13, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));

        var vm = CreateVm(row, read.Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        vm.DocumentsTab.Photos.Should().HaveCount(2);
        vm.DocumentsTab.Photos.Should().Contain(p => p.Source == "Keşif raporu");
        vm.DocumentsTab.Photos.Should().Contain(p => p.Source.Contains("Ziyaret"));
    }

    [Fact]
    public async Task InitializeAsync_LoadsDeliverySummary()
    {
        // Teslim kaydı olan iş: durum Delivered, ödeme bilgileri sekmede görünür,
        // teslim kaydı hâlâ düzenlenebilir ve fatura üretilebilir.
        var row = new ServiceJobRowDto { Id = 9, Status = JobStatus.Delivered };
        var delivery = new JobDeliveryDto(
            Id: 5,
            ServiceJobId: 9,
            DeliveryDate: new DateTime(2026, 8, 5, 16, 0, 0, DateTimeKind.Utc),
            DeliveredBy: "Ali Usta",
            DeliveryNote: "Cihaz teslim edildi.",
            CustomerSignature: "imza",
            PaymentStatus: PaymentStatus.Paid,
            PaymentMethod: PaymentMethod.CreditCard,
            PaidAmount: 2700m,
            InvoiceNumber: "INV-2026-001");
        var workspace = MakeWorkspace(JobStatus.Delivered, delivery: delivery);

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkspaceAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workspace));
        read.Setup(r => r.GetMaterialsAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));

        var vm = CreateVm(row, read.Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        vm.DeliveryTab.IsDelivered.Should().BeTrue();
        vm.DeliveryTab.DeliveredByDisplay.Should().Be("Ali Usta");
        vm.DeliveryTab.PaymentStatusDisplay.Should().Contain("Ödendi");
        vm.DeliveryTab.PaidAmountDisplay.Should().Contain("2.700");
        vm.DeliveryTab.InvoiceNumberDisplay.Should().Be("INV-2026-001");
        vm.DeliveryTab.StatusLine.Should().Contain("teslim edildi");

        // Kapalı aşamada teslim kaydı düzenlenebilir ve fatura üretilebilir (servis kuralları).
        vm.DeliveryTab.GetAction(WorkOrderAction.CompleteDelivery)!.IsEnabled.Should().BeTrue();
        vm.DeliveryTab.DocumentActions.Should().Contain(a => a.Action == WorkOrderAction.GenerateInvoice && a.IsEnabled);
    }

    [Fact]
    public async Task QuotationTab_HidesEditActionWhenNoQuotation()
    {
        // Keşif tamamlandı ama teklif yok → EditQuotation action dönmez (görünmez).
        var row = new ServiceJobRowDto { Id = 3, Status = JobStatus.DiscoveryCompleted };
        var workspace = MakeWorkspace(JobStatus.DiscoveryCompleted);

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkspaceAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workspace));
        read.Setup(r => r.GetMaterialsAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));

        var vm = CreateVm(row, read.Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        vm.QuotationTab.HasQuotation.Should().BeFalse();
        vm.QuotationTab.GetAction(WorkOrderAction.EditQuotation).Should().BeNull("teklif yokken Teklifi Düzenle görünmez");

        // Ön koşullar sağlandığı için CreateQuotation etkin ve NextAction'ta.
        vm.DiscoveryTab.GetAction(WorkOrderAction.CreateQuotation)!.IsEnabled.Should().BeTrue();
        vm.NextAction!.Action.Should().Be(WorkOrderAction.CreateQuotation);
    }

    [Fact]
    public async Task InitializeAsync_SurfacesWarningsFromWorkspaceDto()
    {
        // Keşif ön koşulları eksik → uyarı listesi application katmanından gelir.
        var row = new ServiceJobRowDto { Id = 4, Status = JobStatus.DiscoveryCompleted };
        var workspace = MakeWorkspace(JobStatus.DiscoveryCompleted, discoveryNotes: null);

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkspaceAsync(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workspace));
        read.Setup(r => r.GetMaterialsAsync(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));

        var vm = CreateVm(row, read.Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        vm.Warnings.Should().NotBeEmpty();
        vm.Warnings.Should().Contain(w => w.Message.Contains("Teknik tespit"));
        vm.DiscoveryTab.GetAction(WorkOrderAction.CreateQuotation)!.IsEnabled.Should().BeFalse();
        vm.DiscoveryTab.GetAction(WorkOrderAction.CreateQuotation)!.DisabledReason.Should().Contain("Teknik tespit");
    }

    [Fact]
    public async Task InitializeAsync_DistributesActionsToRelevantTabsOnly()
    {
        // Kabul edilmiş teklif → PlanInstallation teklif sekmesinde, montaj sekmesinde değil.
        var row = new ServiceJobRowDto { Id = 5, Status = JobStatus.ConvertedToQuote };
        var workspace = MakeWorkspace(JobStatus.ConvertedToQuote, quote: MakeQuote(QuotationStatus.Accepted));

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkspaceAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workspace));
        read.Setup(r => r.GetMaterialsAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));

        var vm = CreateVm(row, read.Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        vm.QuotationTab.GetAction(WorkOrderAction.PlanInstallation)!.IsEnabled.Should().BeTrue();
        vm.QuotationTab.GetAction(WorkOrderAction.EditQuotation).Should().BeNull("kabul edilmiş teklif düzenlenemez");
        vm.InstallationTab.GetAction(WorkOrderAction.EditInstallation).Should().BeNull("montaj kaydı yokken düzenleme sunulmaz");
        vm.NextAction!.Action.Should().Be(WorkOrderAction.PlanInstallation);
    }

    [Fact]
    public async Task QuotationTab_ShowsSendActionForDraftQuote()
    {
        // Taslak teklif → "Teklifi Gönder" action'ı teklif sekmesinde görünür ve etkindir.
        var row = new ServiceJobRowDto { Id = 6, Status = JobStatus.ConvertedToQuote };
        var workspace = MakeWorkspace(JobStatus.ConvertedToQuote, quote: MakeQuote(QuotationStatus.Draft));

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkspaceAsync(6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workspace));
        read.Setup(r => r.GetMaterialsAsync(6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));

        var vm = CreateVm(row, read.Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        var send = vm.QuotationTab.GetAction(WorkOrderAction.SendQuotation);
        send.Should().NotBeNull("taslak teklifte Teklifi Gönder görünür");
        send!.IsEnabled.Should().BeTrue();
        vm.QuotationTab.Actions.Should().Contain(a => a.Action == WorkOrderAction.SendQuotation);
    }

    [Fact]
    public async Task QuotationTab_SendQuotation_InvokesServiceAndRefreshes()
    {
        // "Teklifi Gönder" → onay sonrası komut servisi çağrılır ve çalışma alanı tazelenir.
        var row = new ServiceJobRowDto { Id = 42, Status = JobStatus.ConvertedToQuote };
        var workspace = MakeWorkspace(JobStatus.ConvertedToQuote, quote: MakeQuote(QuotationStatus.Draft));

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkspaceAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workspace));
        read.Setup(r => r.GetMaterialsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));

        var command = new Mock<IServiceJobCommandService>();
        command.Setup(c => c.SendQuotationAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new WorkOrderQuotationResult(1, QuotationStatus.Sent, 0m)));

        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var vm = new WorkOrderWorkspaceViewModel(
            row,
            read.Object,
            command.Object,
            new PdfService(new Mock<IPersonalDataProtectionService>().Object, new Mock<IAuditTrailService>().Object),
            dialog.Object,
            new Mock<IToastService>().Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        await vm.QuotationTab.SendQuotationAsync();

        command.Verify(c => c.SendQuotationAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        read.Verify(r => r.GetWorkspaceAsync(42, It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task ExecuteAction_SendQuotation_RoutesToQuoteTabAndSends()
    {
        // Dispatcher SendQuotation case'i: önce Teklif sekmesine geçer, sonra gerçek komutu çalıştırır.
        var row = new ServiceJobRowDto { Id = 42, Status = JobStatus.ConvertedToQuote };
        var workspace = MakeWorkspace(JobStatus.ConvertedToQuote, quote: MakeQuote(QuotationStatus.Draft));

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkspaceAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workspace));
        read.Setup(r => r.GetMaterialsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));

        var command = new Mock<IServiceJobCommandService>();
        command.Setup(c => c.SendQuotationAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new WorkOrderQuotationResult(1, QuotationStatus.Sent, 0m)));

        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var vm = new WorkOrderWorkspaceViewModel(
            row,
            read.Object,
            command.Object,
            new PdfService(new Mock<IPersonalDataProtectionService>().Object, new Mock<IAuditTrailService>().Object),
            dialog.Object,
            new Mock<IToastService>().Object);

        (await vm.InitializeAsync()).Should().BeTrue();

        await vm.ExecuteActionCommand.ExecuteAsync(WorkOrderAction.SendQuotation);

        vm.ActiveTabIndex.Should().Be(2, "SendQuotation Teklif sekmesine yönlendirir");
        command.Verify(c => c.SendQuotationAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── FAZ 4: sekme yönlendirmesi ──

    [Theory]
    [InlineData(WorkOrderAction.EditGeneralInfo, 0)]
    [InlineData(WorkOrderAction.AssignResponsible, 0)]
    [InlineData(WorkOrderAction.CancelWorkOrder, 0)]
    [InlineData(WorkOrderAction.ScheduleDiscovery, 1)]
    [InlineData(WorkOrderAction.EditDiscovery, 1)]
    [InlineData(WorkOrderAction.CompleteDiscovery, 1)]
    [InlineData(WorkOrderAction.CreateQuotation, 1)]
    [InlineData(WorkOrderAction.EditQuotation, 2)]
    [InlineData(WorkOrderAction.SendQuotation, 2)]
    [InlineData(WorkOrderAction.AcceptQuotation, 2)]
    [InlineData(WorkOrderAction.RejectQuotation, 2)]
    [InlineData(WorkOrderAction.ReviseQuotation, 2)]
    [InlineData(WorkOrderAction.PlanInstallation, 2)]
    [InlineData(WorkOrderAction.EditInstallation, 3)]
    [InlineData(WorkOrderAction.CompleteInstallation, 3)]
    [InlineData(WorkOrderAction.CompleteDelivery, 4)]
    [InlineData(WorkOrderAction.GenerateInvoice, 5)]
    [InlineData(WorkOrderAction.GenerateServiceReport, 5)]
    public void TabIndexForAction_MapsEachActionToItsHomeTab(WorkOrderAction action, int expectedTab)
    {
        WorkOrderWorkspaceViewModel.TabIndexForAction(action).Should().Be(expectedTab);
    }

    [Fact]
    public void StageItems_NavigateToTheirTabs()
    {
        var vm = CreateVm(
            new ServiceJobRowDto { Id = 1, Status = JobStatus.Pending },
            new Mock<IServiceJobReadService>().Object);

        vm.Stages.Should().HaveCount(6);
        vm.Stages[1].NavigateCommand.Should().NotBeNull();
        vm.Stages[1].NavigateCommand!.Execute(null);
        vm.ActiveTabIndex.Should().Be(1, "Keşif adımı Keşif sekmesini açar");

        vm.Stages[4].NavigateCommand!.Execute(null);
        vm.ActiveTabIndex.Should().Be(4, "Teslim adımı Teslim sekmesini açar");

        vm.Stages[0].NavigateCommand!.Execute(null);
        vm.ActiveTabIndex.Should().Be(0, "Talep/Kapandı adımı Genel Bakış'a döner");
    }

    [Fact]
    public async Task ExecuteAction_NavigatesToHomeTabBeforeRunning()
    {
        var row = new ServiceJobRowDto { Id = 11, Status = JobStatus.Pending };
        var workspace = MakeWorkspace(JobStatus.Pending);

        var read = new Mock<IServiceJobReadService>();
        read.Setup(r => r.GetWorkspaceAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workspace));
        read.Setup(r => r.GetMaterialsAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ServiceJobMaterialDto>>(Array.Empty<ServiceJobMaterialDto>()));

        var vm = CreateVm(row, read.Object);
        (await vm.InitializeAsync()).Should().BeTrue();

        // Genel Bilgileri Düzenle → Genel Bakış (0); servis çağrısı yok (general editor null).
        await vm.ExecuteActionCommand.ExecuteAsync(WorkOrderAction.EditGeneralInfo);
        vm.ActiveTabIndex.Should().Be(0);

        // Servis Raporu → Belgeler (5); guard pasif işlemi çalıştırmadan önce yönlendirme yapılır.
        await vm.ExecuteActionCommand.ExecuteAsync(WorkOrderAction.GenerateServiceReport);
        vm.ActiveTabIndex.Should().Be(5);
    }

    // ── Yardımcılar ──

    private static WorkOrderWorkspaceViewModel CreateVm(ServiceJobRowDto row, IServiceJobReadService read) => new(
        row,
        read,
        new Mock<IServiceJobCommandService>().Object,
        new PdfService(new Mock<IPersonalDataProtectionService>().Object, new Mock<IAuditTrailService>().Object),
        new Mock<IDialogService>().Object,
        new Mock<IToastService>().Object);

    private static WorkOrderWorkspaceDto MakeWorkspace(
        JobStatus status,
        DiscoveryReportDto? discovery = null,
        WorkOrderQuotationDto? quote = null,
        InstallationOrderDto? installation = null,
        JobDeliveryDto? delivery = null,
        IReadOnlyList<DiscoveryVisitDto>? visits = null,
        bool assigned = true,
        int discoveryMaterialCount = 2,
        string? discoveryNotes = "Sahada 4 kat için güzergah tespit edildi.",
        string? discoverySolution = "Cat6 kablo ve IP kamera önerildi.",
        string? technician = "Teknisyen")
    {
        var input = new WorkOrderWorkspaceInput(
            JobId: 42,
            JobStatus: status,
            AssignedUserId: assigned ? 7 : null,
            AssignedTechnicianName: technician,
            SlaDeadline: DateTime.UtcNow.AddDays(3),
            HasDiscoveryReport: discovery is not null || discoveryNotes is not null || discoveryMaterialCount > 0,
            DiscoveryTechnicianName: technician,
            DiscoveryTechnicalNotes: discovery?.TechnicalNotes ?? discoveryNotes,
            DiscoveryRecommendedSolution: discovery?.RecommendedSolution ?? discoverySolution,
            DiscoveryMaterialCount: discovery?.Materials.Count ?? discoveryMaterialCount,
            DiscoveryVisitCount: visits?.Count ?? 0,
            DiscoveryAppointmentDate: visits is { Count: > 0 } ? visits[0].VisitDate : null,
            QuotationStatus: quote?.Status,
            QuotationRevisionNumber: quote?.RevisionNumber ?? 0,
            HasInstallation: installation is not null,
            InstallationDate: installation?.InstallationDate,
            InstallationCompleted: installation?.CompletedAt is not null,
            HasDelivery: delivery is not null,
            DeliveryDate: delivery?.DeliveryDate,
            installation?.Materials.Count ?? 0,
            installation?.LaborHours ?? 0m);

        var resolver = new WorkOrderNextActionResolver();
        return new WorkOrderWorkspaceDto(
            JobId: 42,
            WorkOrderNumber: "#000042",
            CustomerName: "Test Müşteri",
            CustomerPhone: "05550000000",
            CustomerAddress: "Test Mah. 1. Sokak No:1",
            JobTitle: "Kamera kurulumu",
            Description: "Kamera bakımı",
            WorkOrderType: WorkOrderType.Repair,
            Priority: JobPriority.Normal,
            AssignedUserId: input.AssignedUserId,
            AssignedUserName: null,
            AssignedTechnicianId: null,
            AssignedTechnicianName: technician,
            CurrentStage: resolver.ResolveStage(input),
            CurrentStageDisplay: WorkOrderStageLabels.Map(resolver.ResolveStage(input)),
            JobStatus: status,
            CreatedAt: DateTime.UtcNow.AddDays(-1),
            LastActivityAt: null,
            TargetDate: DateTime.UtcNow.AddDays(2),
            DiscoveryAppointmentDate: input.DiscoveryAppointmentDate,
            InstallationDate: installation?.InstallationDate,
            SlaDeadline: input.SlaDeadline,
            SlaStatus: "🟢 Normal",
            NextAction: resolver.ResolveNextAction(input),
            AllowedActions: resolver.ResolveAllowedActions(input),
            Warnings: resolver.ResolveWarnings(input),
            DiscoverySummary: discovery,
            QuotationSummary: quote,
            InstallationSummary: installation,
            DeliverySummary: delivery,
            RecentActivities: Array.Empty<ServiceJobHistoryDto>(),
            Visits: visits);
    }

    private static InstallationOrderDto MakeInstallation(decimal laborHours) => new(
        Id: 3,
        ServiceJobId: 7,
        QuotationId: 2,
        TechnicianId: null,
        TechnicianName: "Ali Usta",
        InstallationDate: new DateTime(2026, 8, 10),
        Notes: "Kablo güzergahı hazır",
        LaborHours: laborHours,
        CompletedAt: null,
        CompletionTechnician: null,
        DeliveryNote: null,
        CustomerSignature: null,
        Materials: new List<InstallationMaterialDto> { new(1, 1, "Kamera", 2m, 1000m, null) },
        Tasks: new List<InstallationTaskDto>());

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
