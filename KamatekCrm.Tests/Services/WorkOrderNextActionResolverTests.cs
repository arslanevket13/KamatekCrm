using System;
using System.Linq;
using FluentAssertions;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Shared.Enums;
using Xunit;

namespace KamatekCrm.Tests.Services;

/// <summary>
/// Workspace iş akışı kuralları (PAKET: NextActionResolver). Kurallar saf
/// <see cref="WorkOrderWorkspaceInput"/> üzerinde çalışır; UI karar vermez, application çözer.
/// </summary>
public sealed class WorkOrderNextActionResolverTests
{
    private readonly WorkOrderNextActionResolver _resolver = new();

    // ── Yardımcılar ──

    private static WorkOrderWorkspaceInput Base(JobStatus status) => new(
        JobId: 42,
        JobStatus: status,
        AssignedUserId: 7,
        AssignedTechnicianName: "Teknisyen",
        SlaDeadline: DateTime.UtcNow.AddDays(3),
        HasDiscoveryReport: true,
        DiscoveryTechnicianName: "Teknisyen",
        DiscoveryTechnicalNotes: "Sahada 4 kat için güzergah tespit edildi.",
        DiscoveryRecommendedSolution: "Cat6 kablo ve IP kamera önerildi.",
        DiscoveryMaterialCount: 2,
        DiscoveryVisitCount: 1,
        DiscoveryAppointmentDate: DateTime.UtcNow.AddDays(-1),
        QuotationStatus: null,
        QuotationRevisionNumber: 0,
        HasInstallation: false,
        InstallationDate: null,
        InstallationCompleted: false,
        HasDelivery: false,
        DeliveryDate: null);

    // ── 1-4. Teklif oluşturma ön koşulları ──

    [Fact]
    public void CreateQuotation_DisabledWhenDiscoveryNotCompleted()
    {
        var input = Base(JobStatus.DiscoveryCompleted) with { DiscoveryTechnicalNotes = null };

        var actions = _resolver.ResolveAllowedActions(input);
        var create = actions.Single(a => a.Action == WorkOrderAction.CreateQuotation);

        create.IsEnabled.Should().BeFalse("keşif tamamlanmadan teklif oluşturulamaz");
        create.DisabledReason.Should().Contain("Teknik tespit");
    }

    [Fact]
    public void CreateQuotation_DisabledWhenTechnicalNotesMissing()
    {
        var input = Base(JobStatus.DiscoveryCompleted) with { DiscoveryTechnicalNotes = null };

        var warnings = _resolver.ResolveWarnings(input);
        warnings.Should().Contain(w => w.Message.Contains("Teknik tespit girilmedi"));
    }

    [Fact]
    public void CreateQuotation_DisabledWhenRecommendedSolutionMissing()
    {
        var input = Base(JobStatus.DiscoveryCompleted) with { DiscoveryRecommendedSolution = null };

        var actions = _resolver.ResolveAllowedActions(input);
        actions.Single(a => a.Action == WorkOrderAction.CreateQuotation).IsEnabled.Should().BeFalse();
        _resolver.ResolveWarnings(input)
            .Should().Contain(w => w.Message.Contains("Önerilen çözüm girilmedi"));
    }

    [Fact]
    public void CreateQuotation_DisabledWhenNoMaterials()
    {
        var input = Base(JobStatus.DiscoveryCompleted) with { DiscoveryMaterialCount = 0 };

        var actions = _resolver.ResolveAllowedActions(input);
        actions.Single(a => a.Action == WorkOrderAction.CreateQuotation).IsEnabled.Should().BeFalse();
        _resolver.ResolveWarnings(input)
            .Should().Contain(w => w.Message.Contains("malzeme/hizmet kalemi"));
    }

    [Fact]
    public void CreateQuotation_DisabledWhenNoTechnician()
    {
        var input = Base(JobStatus.DiscoveryCompleted) with { DiscoveryTechnicianName = null };

        var actions = _resolver.ResolveAllowedActions(input);
        actions.Single(a => a.Action == WorkOrderAction.CreateQuotation).IsEnabled.Should().BeFalse();
        _resolver.ResolveWarnings(input)
            .Should().Contain(w => w.Message.Contains("teknisyen"));
    }

    [Fact]
    public void CreateQuotation_EnabledWhenDiscoveryComplete()
    {
        var input = Base(JobStatus.DiscoveryCompleted);

        var actions = _resolver.ResolveAllowedActions(input);
        actions.Single(a => a.Action == WorkOrderAction.CreateQuotation).IsEnabled.Should().BeTrue();
        _resolver.ResolveNextAction(input).Action.Should().Be(WorkOrderAction.CreateQuotation);
    }

    // ── 5-8. Teklif aşaması işlemleri ──

    [Fact]
    public void EditQuotation_NotOfferedWhenNoQuotation()
    {
        var input = Base(JobStatus.ConvertedToQuote) with { QuotationStatus = null };

        var actions = _resolver.ResolveAllowedActions(input);
        actions.Should().NotContain(a => a.Action == WorkOrderAction.EditQuotation,
            "teklif yokken EditQuotation action dönmez");
    }

    [Fact]
    public void EditQuotation_OfferedForDraftQuotation()
    {
        var input = Base(JobStatus.ConvertedToQuote) with { QuotationStatus = QuotationStatus.Draft };

        var actions = _resolver.ResolveAllowedActions(input);
        actions.Single(a => a.Action == WorkOrderAction.EditQuotation).IsEnabled.Should().BeTrue();
        _resolver.ResolveNextAction(input).Action.Should().Be(WorkOrderAction.EditQuotation);
    }

    [Fact]
    public void EditQuotation_NotOfferedForAcceptedQuotation()
    {
        var input = Base(JobStatus.ConvertedToQuote) with { QuotationStatus = QuotationStatus.Accepted };

        var actions = _resolver.ResolveAllowedActions(input);
        actions.Should().NotContain(a => a.Action == WorkOrderAction.EditQuotation,
            "kabul edilmiş teklif doğrudan düzenlenemez");
    }

    [Fact]
    public void PlanInstallation_OfferedForAcceptedQuotation()
    {
        var input = Base(JobStatus.ConvertedToQuote) with { QuotationStatus = QuotationStatus.Accepted };

        var actions = _resolver.ResolveAllowedActions(input);
        actions.Single(a => a.Action == WorkOrderAction.PlanInstallation).IsEnabled.Should().BeTrue();
        _resolver.ResolveNextAction(input).Action.Should().Be(WorkOrderAction.PlanInstallation);
    }

    // ── 9. Kabul edilmemiş tekliften montaj planlanamaz ──

    [Fact]
    public void PlanInstallation_NotOfferedForDraftQuotation()
    {
        var input = Base(JobStatus.ConvertedToQuote) with { QuotationStatus = QuotationStatus.Draft };

        var actions = _resolver.ResolveAllowedActions(input);
        actions.Should().NotContain(a => a.Action == WorkOrderAction.PlanInstallation,
            "kabul edilmemiş tekliften montaj planlanamaz");
    }

    [Fact]
    public void PlanInstallation_NotOfferedForRejectedQuotation()
    {
        var input = Base(JobStatus.ConvertedToQuote) with { QuotationStatus = QuotationStatus.Rejected };

        var actions = _resolver.ResolveAllowedActions(input);
        actions.Should().NotContain(a => a.Action == WorkOrderAction.PlanInstallation);
        _resolver.ResolveNextAction(input).Action.Should().Be(WorkOrderAction.ReviseQuotation);
    }

    // ── 10. Montaj tamamlanmadan teslim tamamlanamaz ──

    [Fact]
    public void CompleteDelivery_NotOfferedBeforeInstallationCompleted()
    {
        var input = Base(JobStatus.InstallationPlanned) with { HasInstallation = true };

        var actions = _resolver.ResolveAllowedActions(input);
        actions.Should().NotContain(a => a.Action == WorkOrderAction.CompleteDelivery);
        _resolver.ResolveNextAction(input).Action.Should().Be(WorkOrderAction.EditInstallation);
    }

    // ── 11. Teslim aşaması: teslim kaydı olmayan işte yalnızca Teslim Et sunulur ──

    [Fact]
    public void CompleteDelivery_OfferedInDeliveryStage()
    {
        var input = Base(JobStatus.InstallationCompleted) with { HasInstallation = true };

        var actions = _resolver.ResolveAllowedActions(input);
        actions.Should().NotContain(a => a.Action == WorkOrderAction.CloseWorkOrder,
            "politika Delivered'dan çıkışa izin vermez; kapatma ayrı bir adım değildir");
        actions.Single(a => a.Action == WorkOrderAction.CompleteDelivery).IsEnabled.Should().BeTrue();
        _resolver.ResolveNextAction(input).Action.Should().Be(WorkOrderAction.CompleteDelivery);
    }

    [Fact]
    public void GenerateServiceReport_EnabledForDeliveredJobs()
    {
        // Teslim edilmiş (kapalı) işte servis raporu üretilebilir; yalnızca iptalde kapanır.
        var delivered = Base(JobStatus.Delivered) with { HasDelivery = true };
        _resolver.ResolveAllowedActions(delivered)
            .Single(a => a.Action == WorkOrderAction.GenerateServiceReport).IsEnabled.Should().BeTrue();

        var cancelled = Base(JobStatus.Cancelled);
        _resolver.ResolveAllowedActions(cancelled)
            .Single(a => a.Action == WorkOrderAction.GenerateServiceReport).IsEnabled.Should().BeFalse();
        _resolver.ResolveAllowedActions(cancelled)
            .Single(a => a.Action == WorkOrderAction.GenerateServiceReport)
            .DisabledReason.Should().Contain("İptal");
    }

    [Fact]
    public void CompleteDelivery_StillEditableWhenDeliveryExists()
    {
        var input = Base(JobStatus.InstallationCompleted) with
        {
            HasInstallation = true,
            HasDelivery = true,
            DeliveryDate = DateTime.UtcNow
        };

        var actions = _resolver.ResolveAllowedActions(input);
        actions.Single(a => a.Action == WorkOrderAction.CompleteDelivery).IsEnabled.Should().BeTrue("teslim kaydı düzenlenebilir");
        _resolver.ResolveNextAction(input).Action.Should().BeNull("teslim kaydı mevcutsa iş terminaldir");
    }

    // ── 11b. Montaj tamamlama kapısı (malzeme; işçilik saati tamamlama formunda girilir) ──

    [Fact]
    public void CompleteInstallation_DisabledWithoutMaterials()
    {
        var input = Base(JobStatus.InstallationPlanned) with { HasInstallation = true };

        var actions = _resolver.ResolveAllowedActions(input);
        var complete = actions.Single(a => a.Action == WorkOrderAction.CompleteInstallation);
        complete.IsEnabled.Should().BeFalse("malzeme olmadan montaj tamamlanamaz");
        complete.DisabledReason.Should().Contain("malzeme");
    }

    [Fact]
    public void CompleteInstallation_EnabledWithMaterials_EvenWhenPlannedHoursZero()
    {
        // Fiili işçilik saati tamamlama formunda girilir; planlanan saat 0 olsa bile
        // "Montajı Tamamla" formun kendisine erişilebilir kalır (servis saat > 0 doğrular).
        var input = Base(JobStatus.InstallationPlanned) with
        {
            HasInstallation = true,
            InstallationMaterialCount = 2,
            InstallationLaborHours = 0m
        };

        var actions = _resolver.ResolveAllowedActions(input);
        var complete = actions.Single(a => a.Action == WorkOrderAction.CompleteInstallation);
        complete.IsEnabled.Should().BeTrue();
        complete.DisabledReason.Should().BeEmpty();
    }

    // ── 12. İptal edilmiş iş ilerletilemez ──

    [Fact]
    public void CancelledJob_HasNoEnabledActions()
    {
        var input = Base(JobStatus.Cancelled);

        var actions = _resolver.ResolveAllowedActions(input);
        actions.Where(a => a.IsEnabled).Should().BeEmpty("iptal edilmiş işte ilerletilebilecek işlem yok");
        actions.Should().NotContain(a => a.Action == WorkOrderAction.CreateQuotation || a.Action == WorkOrderAction.CompleteDelivery);
        _resolver.ResolveNextAction(input).IsEnabled.Should().BeFalse();
        _resolver.ResolveStage(input).Should().Be(WorkOrderStage.Cancelled);
    }

    // ── 13. NextAction matrisi ──

    [Fact]
    public void NextAction_Matrix_GuidesEachStage()
    {
        // Talep: sorumlu yok → Sorumlu Ata
        var pendingNoOwner = Base(JobStatus.Pending) with { AssignedUserId = null };
        _resolver.ResolveNextAction(pendingNoOwner).Action.Should().Be(WorkOrderAction.AssignResponsible);

        // Talep: sorumlu var → Keşif Planla
        _resolver.ResolveNextAction(Base(JobStatus.Pending)).Action.Should().Be(WorkOrderAction.ScheduleDiscovery);

        // Keşif: rapor yok → Keşif Planla
        var discoveryNoReport = Base(JobStatus.DiscoveryRequest) with { HasDiscoveryReport = false, DiscoveryMaterialCount = 0 };
        _resolver.ResolveNextAction(discoveryNoReport).Action.Should().Be(WorkOrderAction.ScheduleDiscovery);

        // Keşif: rapor tamamlanmadı → Keşfi Tamamla (pasif + gerekçe)
        var discoveryIncomplete = Base(JobStatus.DiscoveryCompleted) with { DiscoveryMaterialCount = 0 };
        var incompleteNext = _resolver.ResolveNextAction(discoveryIncomplete);
        incompleteNext.Action.Should().Be(WorkOrderAction.CompleteDiscovery);
        incompleteNext.IsEnabled.Should().BeFalse();

        // Keşif tamamlandı → Teklif Oluştur
        _resolver.ResolveNextAction(Base(JobStatus.DiscoveryCompleted)).Action.Should().Be(WorkOrderAction.CreateQuotation);

        // Teklif: Taslak → Düzenle
        _resolver.ResolveNextAction(Base(JobStatus.ConvertedToQuote) with { QuotationStatus = QuotationStatus.Draft })
            .Action.Should().Be(WorkOrderAction.EditQuotation);

        // Teklif: Gönderildi → Müşteri cevabını kaydet
        _resolver.ResolveNextAction(Base(JobStatus.ConvertedToQuote) with { QuotationStatus = QuotationStatus.Sent })
            .Action.Should().Be(WorkOrderAction.AcceptQuotation);

        // Teklif: Süresi doldu → Revizyon
        _resolver.ResolveNextAction(Base(JobStatus.ConvertedToQuote) with { QuotationStatus = QuotationStatus.Expired })
            .Action.Should().Be(WorkOrderAction.ReviseQuotation);

        // Montaj planlandı → Montajı Başlat (EditInstallation)
        _resolver.ResolveNextAction(Base(JobStatus.InstallationPlanned) with { HasInstallation = true })
            .Action.Should().Be(WorkOrderAction.EditInstallation);

        // Montaj tamamlandı, teslim yok → Teslim Et
        _resolver.ResolveNextAction(Base(JobStatus.InstallationCompleted) with { HasInstallation = true })
            .Action.Should().Be(WorkOrderAction.CompleteDelivery);

        // Teslim tamamlandı → terminal (kapatılacak ayrı adım yok — politika Delivered'da durur)
        _resolver.ResolveNextAction(Base(JobStatus.InstallationCompleted) with
        {
            HasInstallation = true,
            HasDelivery = true,
            DeliveryDate = DateTime.UtcNow
        }).Action.Should().BeNull();

        // Kapandı → ilerletilecek işlem yok
        _resolver.ResolveNextAction(Base(JobStatus.Delivered)).Action.Should().BeNull();
    }

    // ── 14. AllowedActions matrisi ──

    [Fact]
    public void AllowedActions_Matrix_MatchesQuotationStatus()
    {
        var draft = Base(JobStatus.ConvertedToQuote) with { QuotationStatus = QuotationStatus.Draft };
        var draftActions = _resolver.ResolveAllowedActions(draft).Select(a => a.Action).ToArray();
        draftActions.Should().Contain(WorkOrderAction.EditQuotation);
        draftActions.Should().Contain(WorkOrderAction.SendQuotation);
        draftActions.Should().NotContain(WorkOrderAction.AcceptQuotation);

        var sent = draft with { QuotationStatus = QuotationStatus.Sent };
        var sentActions = _resolver.ResolveAllowedActions(sent).Select(a => a.Action).ToArray();
        sentActions.Should().Contain(WorkOrderAction.AcceptQuotation);
        sentActions.Should().Contain(WorkOrderAction.RejectQuotation);
        sentActions.Should().NotContain(WorkOrderAction.EditQuotation);

        var accepted = draft with { QuotationStatus = QuotationStatus.Accepted };
        var acceptedActions = _resolver.ResolveAllowedActions(accepted).Select(a => a.Action).ToArray();
        acceptedActions.Should().Contain(WorkOrderAction.PlanInstallation);
        acceptedActions.Should().NotContain(WorkOrderAction.EditQuotation);
    }

    [Fact]
    public void AllowedActions_InvoiceOnlyWhenAcceptedOrDelivered()
    {
        var draft = Base(JobStatus.ConvertedToQuote) with { QuotationStatus = QuotationStatus.Draft };
        _resolver.ResolveAllowedActions(draft)
            .Single(a => a.Action == WorkOrderAction.GenerateInvoice).IsEnabled.Should().BeFalse();

        var accepted = draft with { QuotationStatus = QuotationStatus.Accepted };
        _resolver.ResolveAllowedActions(accepted)
            .Single(a => a.Action == WorkOrderAction.GenerateInvoice).IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Stage_ResolvesFromJobStatus()
    {
        _resolver.ResolveStage(Base(JobStatus.Pending)).Should().Be(WorkOrderStage.Pending);
        _resolver.ResolveStage(Base(JobStatus.DiscoveryRequest)).Should().Be(WorkOrderStage.Discovery);
        _resolver.ResolveStage(Base(JobStatus.DiscoveryCompleted)).Should().Be(WorkOrderStage.Discovery);
        _resolver.ResolveStage(Base(JobStatus.ConvertedToQuote)).Should().Be(WorkOrderStage.Quotation);
        _resolver.ResolveStage(Base(JobStatus.InstallationPlanned)).Should().Be(WorkOrderStage.Installation);
        _resolver.ResolveStage(Base(JobStatus.InstallationCompleted)).Should().Be(WorkOrderStage.Delivery);
        _resolver.ResolveStage(Base(JobStatus.Delivered)).Should().Be(WorkOrderStage.Closed);
        _resolver.ResolveStage(Base(JobStatus.Completed)).Should().Be(WorkOrderStage.Closed);
        _resolver.ResolveStage(Base(JobStatus.Cancelled)).Should().Be(WorkOrderStage.Cancelled);
    }
}
