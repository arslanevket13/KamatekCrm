using System.Text.Json;
using FluentAssertions;
using KamatekCrm.ApplicationCore.DTOs.ProjectQuotes;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace KamatekCrm.Tests.Services;

public sealed class ProjectQuoteServiceTests
{
    [Fact]
    public void ScopeSerializer_RoundTripsItemsWithoutRuntimeCallbacks()
    {
        var scope = Scope(RequiredItem(100m, 30m, 5m, 2));
        scope[0].Items[0].OnItemChanged = () => { };

        var json = ProjectScopeService.Serialize(scope);
        var restored = ProjectScopeService.Deserialize(json);

        json.Should().NotContain("OnItemChanged");
        restored.Should().ContainSingle();
        restored[0].Items.Should().ContainSingle(item =>
            item.UnitPrice == 100m && item.Quantity == 2 && item.TotalItemCost == 70m);
    }

    [Fact]
    public void PricingPolicy_RecalculatesPrimitiveLines_AndIgnoresCachedAndOptionalTotals()
    {
        var scope = Scope(
            RequiredItem(unitPrice: 100.005m, unitCost: 40m, laborCost: 10m, quantity: 2),
            OptionalItem(unitPrice: 5_000m, quantity: 3));
        scope[0].RecursiveTotal = 9_999_999m;
        scope[0].RecursiveTotalCost = 8_888_888m;

        var result = ProjectQuotePricingPolicy.Calculate(scope, 10m, 20m);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().BeEquivalentTo(new ProjectQuotePricingResult(
            GrossRevenue: 200.01m,
            TotalCost: 100m,
            DiscountAmount: 20m,
            NetRevenue: 180.01m,
            VatAmount: 36m,
            GrandTotal: 216.01m,
            TotalProfit: 80.01m,
            MarginPercent: 44.45m,
            IncludedLineCount: 1,
            TotalQuantity: 2));
    }

    [Fact]
    public async Task SaveAsync_CreatesAtomicallyWithServerPricingAuditAndIdempotency()
    {
        await using var fixture = await ProjectQuoteFixture.CreateAsync();
        var operationId = Guid.NewGuid();
        var command = fixture.NewCommand(operationId, Scope(RequiredItem(125m, 50m, 5m, 2)), 10m, 20m);

        var first = await fixture.Command.SaveAsync(command);
        var second = await fixture.Command.SaveAsync(command);

        first.IsSuccess.Should().BeTrue(first.Error);
        second.IsSuccess.Should().BeTrue(second.Error);
        first.Value!.Pricing.NetRevenue.Should().Be(225m);
        first.Value.Pricing.GrandTotal.Should().Be(270m);
        second.Value!.WasAlreadyApplied.Should().BeTrue();
        second.Value.ProjectId.Should().Be(first.Value.ProjectId);

        await using var verify = fixture.CreateContext();
        var project = await verify.ServiceProjects.SingleAsync();
        project.TotalBudget.Should().Be(225m);
        project.TotalCost.Should().Be(110m);
        project.TotalProfit.Should().Be(115m);
        project.RevisionNumber.Should().Be(1);
        (await verify.ActivityLogs.CountAsync(log => log.ReferenceId == $"QUOTE-SAVE:{operationId:N}"))
            .Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_UpdateArchivesPreviousRevisionAndDoesNotArchiveUiOnlyChanges()
    {
        await using var fixture = await ProjectQuoteFixture.CreateAsync();
        var originalScope = Scope(RequiredItem(100m, 30m, 0, 1));
        var created = await fixture.Command.SaveAsync(
            fixture.NewCommand(Guid.NewGuid(), originalScope, 0, 20));
        var projectId = created.Value!.ProjectId;

        originalScope[0].IsExpanded = !originalScope[0].IsExpanded;
        originalScope[0].IsSelected = true;
        var noOp = await fixture.Command.SaveAsync(fixture.UpdateCommand(
            Guid.NewGuid(), projectId, 1, originalScope, 0, 20));

        var revisedScope = Scope(RequiredItem(150m, 30m, 0, 1));
        var revised = await fixture.Command.SaveAsync(fixture.UpdateCommand(
            Guid.NewGuid(), projectId, 1, revisedScope, 0, 20));

        noOp.IsSuccess.Should().BeTrue(noOp.Error);
        noOp.Value!.WasNoOp.Should().BeTrue();
        noOp.Value.RevisionNumber.Should().Be(1);
        revised.IsSuccess.Should().BeTrue(revised.Error);
        revised.Value!.RevisionNumber.Should().Be(2);

        await using var verify = fixture.CreateContext();
        var project = await verify.ServiceProjects.SingleAsync();
        project.TotalBudget.Should().Be(150m);
        var history = JsonSerializer.Deserialize<List<QuoteRevision>>(project.RevisionsJson!);
        history.Should().ContainSingle();
        history![0].RevisionNumber.Should().Be(1);
        history[0].TotalBudget.Should().Be(100m);
        ProjectQuotePricingPolicy.Calculate(history[0].ScopeSnapshotJson, 0, 20)
            .Value!.GrossRevenue.Should().Be(100m);
    }

    [Fact]
    public async Task SaveAsync_WithStaleRevisionRejectsWithoutPartialWrite()
    {
        await using var fixture = await ProjectQuoteFixture.CreateAsync();
        var created = await fixture.Command.SaveAsync(fixture.NewCommand(
            Guid.NewGuid(), Scope(RequiredItem(100m, 40m, 0, 1)), 0, 20));
        var id = created.Value!.ProjectId;
        var firstUpdate = await fixture.Command.SaveAsync(fixture.UpdateCommand(
            Guid.NewGuid(), id, 1, Scope(RequiredItem(120m, 40m, 0, 1)), 0, 20));

        var staleOperation = Guid.NewGuid();
        var stale = await fixture.Command.SaveAsync(fixture.UpdateCommand(
            staleOperation, id, 1, Scope(RequiredItem(999m, 1m, 0, 1)), 0, 20));

        firstUpdate.IsSuccess.Should().BeTrue(firstUpdate.Error);
        stale.IsFailure.Should().BeTrue();
        stale.Error.Should().Contain("başka bir kullanıcı");
        await using var verify = fixture.CreateContext();
        var project = await verify.ServiceProjects.SingleAsync();
        project.TotalBudget.Should().Be(120m);
        project.RevisionNumber.Should().Be(2);
        (await verify.ActivityLogs.AnyAsync(log => log.ReferenceId == $"QUOTE-SAVE:{staleOperation:N}"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_WhenRevisionHistoryIsCorrupt_RollsBackAllChanges()
    {
        await using var fixture = await ProjectQuoteFixture.CreateAsync();
        var created = await fixture.Command.SaveAsync(fixture.NewCommand(
            Guid.NewGuid(), Scope(RequiredItem(100m, 40m, 0, 1)), 0, 20));
        await using (var corrupt = fixture.CreateContext())
        {
            var project = await corrupt.ServiceProjects.SingleAsync();
            project.RevisionsJson = "not-json";
            await corrupt.SaveChangesAsync();
        }

        var operation = Guid.NewGuid();
        var result = await fixture.Command.SaveAsync(fixture.UpdateCommand(
            operation, created.Value!.ProjectId, 1, Scope(RequiredItem(200m, 40m, 0, 1)), 0, 20));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("revizyon geçmişi");
        await using var verify = fixture.CreateContext();
        var unchanged = await verify.ServiceProjects.SingleAsync();
        unchanged.TotalBudget.Should().Be(100m);
        unchanged.RevisionNumber.Should().Be(1);
        unchanged.RevisionsJson.Should().Be("not-json");
        (await verify.ActivityLogs.AnyAsync(log => log.ReferenceId == $"QUOTE-SAVE:{operation:N}"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Services_WhenUnauthorized_DoNotExposeOrWriteQuoteData()
    {
        await using var fixture = await ProjectQuoteFixture.CreateAsync(isAuthorized: false);

        var workspace = await fixture.Read.GetWorkspaceAsync();
        var save = await fixture.Command.SaveAsync(fixture.NewCommand(
            Guid.NewGuid(), Scope(RequiredItem(100m, 30m, 0, 1)), 0, 20));

        workspace.IsFailure.Should().BeTrue();
        save.IsFailure.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        (await verify.ServiceProjects.CountAsync()).Should().Be(0);
        (await verify.ActivityLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void LifecyclePolicy_EnforcesOrderedTransitionsAndRequiredRejectionReason()
    {
        var now = DateTime.UtcNow;

        ProjectQuoteLifecyclePolicy.ValidateTransition(
            QuoteStatus.Draft, QuoteStatus.Approved, null, now).IsFailure.Should().BeTrue();
        ProjectQuoteLifecyclePolicy.ValidateTransition(
            QuoteStatus.Draft, QuoteStatus.Sent, null, now).IsSuccess.Should().BeTrue();
        ProjectQuoteLifecyclePolicy.ValidateTransition(
            QuoteStatus.Sent, QuoteStatus.Rejected, now.AddDays(1), now).IsFailure.Should().BeTrue();
        ProjectQuoteLifecyclePolicy.ValidateTransition(
            QuoteStatus.Sent, QuoteStatus.Rejected, now.AddDays(1), now, "Bütçe")
            .IsSuccess.Should().BeTrue();
        ProjectQuoteLifecyclePolicy.ValidateTransition(
            QuoteStatus.Sent, QuoteStatus.Approved, now.AddMinutes(-1), now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeStatusAsync_RejectsDirectApprovalWithoutWriting()
    {
        await using var fixture = await ProjectQuoteFixture.CreateAsync();
        var created = await fixture.Command.SaveAsync(fixture.NewCommand(
            Guid.NewGuid(), Scope(RequiredItem(100m, 30m, 0, 1)), 0, 20));
        var operation = Guid.NewGuid();

        var result = await fixture.Command.ChangeStatusAsync(new ChangeProjectQuoteStatusCommand(
            operation, created.Value!.ProjectId, 1, QuoteStatus.Draft, QuoteStatus.Approved));

        result.IsFailure.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        (await verify.ServiceProjects.SingleAsync()).QuoteStatus.Should().Be(QuoteStatus.Draft);
        (await verify.ActivityLogs.AnyAsync(log => log.ReferenceId == $"QUOTE-STATUS:{operation:N}"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task ChangeStatusAsync_SendsAndApprovesIdempotentlyWithPipelineState()
    {
        await using var fixture = await ProjectQuoteFixture.CreateAsync();
        var created = await fixture.Command.SaveAsync(fixture.NewCommand(
            Guid.NewGuid(), Scope(RequiredItem(100m, 30m, 0, 1)), 0, 20));
        var projectId = created.Value!.ProjectId;
        var sent = await fixture.Command.ChangeStatusAsync(new ChangeProjectQuoteStatusCommand(
            Guid.NewGuid(), projectId, 1, QuoteStatus.Draft, QuoteStatus.Sent, ValidityDays: 45));
        var approvalKey = Guid.NewGuid();
        var approved = await fixture.Command.ChangeStatusAsync(new ChangeProjectQuoteStatusCommand(
            approvalKey, projectId, 1, QuoteStatus.Sent, QuoteStatus.Approved));
        var replay = await fixture.Command.ChangeStatusAsync(new ChangeProjectQuoteStatusCommand(
            approvalKey, projectId, 1, QuoteStatus.Sent, QuoteStatus.Approved));

        sent.IsSuccess.Should().BeTrue(sent.Error);
        approved.IsSuccess.Should().BeTrue(approved.Error);
        replay.IsSuccess.Should().BeTrue(replay.Error);
        replay.Value!.WasAlreadyApplied.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        var project = await verify.ServiceProjects.SingleAsync();
        project.QuoteStatus.Should().Be(QuoteStatus.Approved);
        project.Status.Should().Be(ProjectStatus.Active);
        project.PipelineStage.Should().Be(PipelineStage.Won);
        project.SentDate.Should().NotBeNull();
        project.ValidUntil.Should().BeAfter(project.SentDate!.Value.AddDays(44));
        project.ApprovedDate.Should().NotBeNull();
        (await verify.ActivityLogs.CountAsync(log => log.ReferenceId == $"QUOTE-STATUS:{approvalKey:N}"))
            .Should().Be(1);
    }

    [Fact]
    public async Task ExpireOverdueAsync_ExpiresOnlySentQuotesPastValidity()
    {
        await using var fixture = await ProjectQuoteFixture.CreateAsync();
        var overdue = await fixture.Command.SaveAsync(fixture.NewCommand(
            Guid.NewGuid(), Scope(RequiredItem(100m, 30m, 0, 1)), 0, 20));
        var future = await fixture.Command.SaveAsync(fixture.NewCommand(
            Guid.NewGuid(), Scope(RequiredItem(200m, 50m, 0, 1)), 0, 20) with { Title = "Gelecek Teklif" });
        await using (var arrange = fixture.CreateContext())
        {
            var first = await arrange.ServiceProjects.FindAsync(overdue.Value!.ProjectId);
            var second = await arrange.ServiceProjects.FindAsync(future.Value!.ProjectId);
            first!.QuoteStatus = QuoteStatus.Sent;
            first.ValidUntil = DateTime.UtcNow.AddDays(-1);
            second!.QuoteStatus = QuoteStatus.Sent;
            second.ValidUntil = DateTime.UtcNow.AddDays(1);
            await arrange.SaveChangesAsync();
        }

        var result = await fixture.Command.ExpireOverdueAsync();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.ExpiredCount.Should().Be(1);
        await using var verify = fixture.CreateContext();
        (await verify.ServiceProjects.FindAsync(overdue.Value.ProjectId))!.QuoteStatus
            .Should().Be(QuoteStatus.Expired);
        (await verify.ServiceProjects.FindAsync(future.Value.ProjectId))!.QuoteStatus
            .Should().Be(QuoteStatus.Sent);
    }

    [Fact]
    public async Task DuplicateAndDeleteDraft_AreIdempotentAndPreserveSentHistory()
    {
        await using var fixture = await ProjectQuoteFixture.CreateAsync();
        var source = await fixture.Command.SaveAsync(fixture.NewCommand(
            Guid.NewGuid(), Scope(RequiredItem(125m, 40m, 5m, 2)), 10, 20));
        var duplicateKey = Guid.NewGuid();
        var duplicate = await fixture.Command.DuplicateAsync(new DuplicateProjectQuoteCommand(
            duplicateKey, source.Value!.ProjectId, 1));
        var duplicateReplay = await fixture.Command.DuplicateAsync(new DuplicateProjectQuoteCommand(
            duplicateKey, source.Value.ProjectId, 1));
        var deleteKey = Guid.NewGuid();
        var deleted = await fixture.Command.DeleteDraftAsync(new DeleteProjectQuoteCommand(
            deleteKey, duplicate.Value!.ProjectId, 1, QuoteStatus.Draft));
        var deleteReplay = await fixture.Command.DeleteDraftAsync(new DeleteProjectQuoteCommand(
            deleteKey, duplicate.Value.ProjectId, 1, QuoteStatus.Draft));

        duplicate.IsSuccess.Should().BeTrue(duplicate.Error);
        duplicateReplay.Value!.WasAlreadyApplied.Should().BeTrue();
        deleted.IsSuccess.Should().BeTrue(deleted.Error);
        deleteReplay.Value!.WasAlreadyApplied.Should().BeTrue();
        await using var verify = fixture.CreateContext();
        (await verify.ServiceProjects.CountAsync()).Should().Be(1);
        (await verify.ServiceProjects.SingleAsync()).Id.Should().Be(source.Value.ProjectId);
    }

    [Fact]
    public async Task DeleteDraftAsync_RefusesSentQuoteAndKeepsAuditHistoryClean()
    {
        await using var fixture = await ProjectQuoteFixture.CreateAsync();
        var created = await fixture.Command.SaveAsync(fixture.NewCommand(
            Guid.NewGuid(), Scope(RequiredItem(100m, 30m, 0, 1)), 0, 20));
        await fixture.Command.ChangeStatusAsync(new ChangeProjectQuoteStatusCommand(
            Guid.NewGuid(), created.Value!.ProjectId, 1, QuoteStatus.Draft, QuoteStatus.Sent));
        var deleteOperation = Guid.NewGuid();

        var result = await fixture.Command.DeleteDraftAsync(new DeleteProjectQuoteCommand(
            deleteOperation, created.Value.ProjectId, 1, QuoteStatus.Sent));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("taslak");
        await using var verify = fixture.CreateContext();
        (await verify.ServiceProjects.CountAsync()).Should().Be(1);
        (await verify.ActivityLogs.AnyAsync(log => log.ReferenceId == $"QUOTE-DELETE:{deleteOperation:N}"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task ConvertApprovedToWorkOrderAsync_CreatesSingleLinkedInstallationAtomically()
    {
        await using var fixture = await ProjectQuoteFixture.CreateAsync();
        var created = await fixture.Command.SaveAsync(fixture.NewCommand(
            Guid.NewGuid(), Scope(RequiredItem(100m, 30m, 5m, 2)), 10, 20));
        var projectId = created.Value!.ProjectId;
        await fixture.Command.ChangeStatusAsync(new ChangeProjectQuoteStatusCommand(
            Guid.NewGuid(), projectId, 1, QuoteStatus.Draft, QuoteStatus.Sent));
        await fixture.Command.ChangeStatusAsync(new ChangeProjectQuoteStatusCommand(
            Guid.NewGuid(), projectId, 1, QuoteStatus.Sent, QuoteStatus.Approved));

        var first = await fixture.Command.ConvertApprovedToWorkOrderAsync(
            new ConvertApprovedQuoteToWorkOrderCommand(
                Guid.NewGuid(), projectId, 1, QuoteStatus.Approved));
        var second = await fixture.Command.ConvertApprovedToWorkOrderAsync(
            new ConvertApprovedQuoteToWorkOrderCommand(
                Guid.NewGuid(), projectId, 1, QuoteStatus.Approved));

        first.IsSuccess.Should().BeTrue(first.Error);
        second.IsSuccess.Should().BeTrue(second.Error);
        second.Value!.WasAlreadyApplied.Should().BeTrue();
        second.Value.WorkOrderId.Should().Be(first.Value!.WorkOrderId);
        await using var verify = fixture.CreateContext();
        var job = await verify.ServiceJobs.SingleAsync();
        job.ServiceProjectId.Should().Be(projectId);
        job.WorkOrderType.Should().Be(WorkOrderType.Installation);
        job.ServiceJobType.Should().Be(ServiceJobType.Project);
        job.Price.Should().Be(180m);
        job.TaxAmount.Should().Be(36m);
        job.TotalAmount.Should().Be(216m);
        (await verify.ServiceJobHistories.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_EditingApprovedQuoteCreatesUnapprovedRevision()
    {
        await using var fixture = await ProjectQuoteFixture.CreateAsync();
        var created = await fixture.Command.SaveAsync(fixture.NewCommand(
            Guid.NewGuid(), Scope(RequiredItem(100m, 30m, 0, 1)), 0, 20));
        var projectId = created.Value!.ProjectId;
        await fixture.Command.ChangeStatusAsync(new ChangeProjectQuoteStatusCommand(
            Guid.NewGuid(), projectId, 1, QuoteStatus.Draft, QuoteStatus.Sent));
        await fixture.Command.ChangeStatusAsync(new ChangeProjectQuoteStatusCommand(
            Guid.NewGuid(), projectId, 1, QuoteStatus.Sent, QuoteStatus.Approved));

        var revised = await fixture.Command.SaveAsync(fixture.UpdateCommand(
            Guid.NewGuid(), projectId, 1, Scope(RequiredItem(120m, 30m, 0, 1)), 0, 20));

        revised.IsSuccess.Should().BeTrue(revised.Error);
        revised.Value!.RevisionNumber.Should().Be(2);
        await using var verify = fixture.CreateContext();
        var project = await verify.ServiceProjects.SingleAsync();
        project.QuoteStatus.Should().Be(QuoteStatus.Revised);
        project.Status.Should().Be(ProjectStatus.Draft);
        project.PipelineStage.Should().Be(PipelineStage.Negotiation);
        project.SentDate.Should().BeNull();
        project.ValidUntil.Should().BeNull();
        project.ApprovedDate.Should().BeNull();
    }

    [Fact]
    public async Task ReadService_ProjectsLifecycleListAndPdfExportData()
    {
        await using var fixture = await ProjectQuoteFixture.CreateAsync();
        var created = await fixture.Command.SaveAsync(fixture.NewCommand(
            Guid.NewGuid(), Scope(RequiredItem(100m, 30m, 0, 1)), 5, 20));

        var list = await fixture.Read.GetListAsync();
        var export = await fixture.Read.GetExportAsync(created.Value!.ProjectId);

        list.IsSuccess.Should().BeTrue(list.Error);
        list.Value.Should().ContainSingle(item =>
            item.Id == created.Value.ProjectId &&
            item.CustomerName == "Teklif Müşterisi" &&
            item.TotalBudget == 95m &&
            item.QuoteStatus == QuoteStatus.Draft);
        export.IsSuccess.Should().BeTrue(export.Error);
        export.Value!.Quote.ProjectScopeJson.Should().NotBeNullOrWhiteSpace();
        export.Value.Customer!.Email.Should().Be("customer@example.com");
        export.Value.Customer.City.Should().Be("Ankara");
    }

    private static List<ScopeNode> Scope(params ScopeNodeItem[] items)
    {
        var root = new ScopeNode { Name = "Proje", Type = NodeType.Project };
        foreach (var item in items) root.Items.Add(item);
        root.NotifyTotalsChanged();
        return [root];
    }

    private static ScopeNodeItem RequiredItem(
        decimal unitPrice,
        decimal unitCost,
        decimal laborCost,
        int quantity) => new()
        {
            Name = "Kamera",
            ProductName = "Kamera",
            ProductId = 1,
            UnitPrice = unitPrice,
            UnitCost = unitCost,
            LaborCost = laborCost,
            Quantity = quantity
        };

    private static ScopeNodeItem OptionalItem(decimal unitPrice, int quantity) => new()
    {
        Name = "Opsiyon",
        ProductName = "Opsiyon",
        ProductId = 2,
        UnitPrice = unitPrice,
        Quantity = quantity,
        IsOptional = true
    };

    private sealed class ProjectQuoteFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        private ProjectQuoteFixture(
            SqliteConnection connection,
            DbContextOptions<AppDbContext> options,
            ProjectQuoteCommandService command,
            ProjectQuoteReadService read,
            int customerId)
        {
            _connection = connection;
            _options = options;
            Command = command;
            Read = read;
            CustomerId = customerId;
        }

        public ProjectQuoteCommandService Command { get; }
        public ProjectQuoteReadService Read { get; }
        public int CustomerId { get; }

        public static async Task<ProjectQuoteFixture> CreateAsync(bool isAuthorized = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            int customerId;
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                var customer = new Customer
                {
                    CustomerCode = "M-001",
                    FullName = "Teklif Müşterisi",
                    PhoneNumber = "5550000000",
                    Email = "customer@example.com",
                    City = "Ankara"
                };
                seed.Customers.Add(customer);
                seed.Products.Add(new Product
                {
                    ProductName = "Kamera",
                    SKU = "CAM-1",
                    Barcode = "869000000099",
                    PurchasePrice = 30,
                    SalePrice = 100,
                    Unit = "Adet"
                });
                await seed.SaveChangesAsync();
                customerId = customer.Id;
            }

            var factory = new Mock<IDbContextFactory<AppDbContext>>();
            factory.Setup(item => item.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new AppDbContext(options));
            var authorization = new TestAuthorizationService(isAuthorized);
            var currentUser = new TestCurrentUserContext();
            return new ProjectQuoteFixture(
                connection,
                options,
                new ProjectQuoteCommandService(factory.Object, authorization, currentUser),
                new ProjectQuoteReadService(factory.Object, authorization),
                customerId);
        }

        public SaveProjectQuoteCommand NewCommand(
            Guid operationId,
            IReadOnlyCollection<ScopeNode> scope,
            decimal discount,
            decimal vat) => new(
                operationId, null, 1, CustomerId, "Kamera Projesi",
                JsonSerializer.Serialize(scope), discount, vat);

        public SaveProjectQuoteCommand UpdateCommand(
            Guid operationId,
            int projectId,
            int expectedRevision,
            IReadOnlyCollection<ScopeNode> scope,
            decimal discount,
            decimal vat) => new(
                operationId, projectId, expectedRevision, CustomerId, "Kamera Projesi",
                JsonSerializer.Serialize(scope), discount, vat);

        public AppDbContext CreateContext() => new(_options);

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }

    private sealed class TestAuthorizationService(bool isAuthorized) : IApplicationAuthorizationService
    {
        public bool IsAuthorized(ApplicationPermission permission) => isAuthorized;
        public KamatekCrm.ApplicationCore.Common.Result Authorize(ApplicationPermission permission) =>
            isAuthorized
                ? KamatekCrm.ApplicationCore.Common.Result.Success()
                : KamatekCrm.ApplicationCore.Common.Result.Failure("Yetkisiz işlem.");
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public bool IsAuthenticated => true;
        public int? UserId => 7;
        public string Username => "quote-user";
        public string Role => "Technician";
        public bool HasPermission(ApplicationPermission permission) => true;
    }
}
