using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Configuration;
using KamatekCrm.Diagnostics.Authorization;
using KamatekCrm.Diagnostics.Core;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.ViewModels.DevCenter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Diagnostics;

/// <summary>
/// Dev Center kabuğunun sekme listesini ve yetkisiz açılışta veri okumadığını doğrular.
/// Pencere (XAML) yüklemesi bu testlerin kapsamı dışındadır.
/// </summary>
public sealed class DevCenterViewModelTests
{
    private static DevCenterViewModel CreateViewModel(bool authorized)
    {
        var options = new DiagnosticOptions
        {
            // Tanılama açık; erişimi kapatan şey boş geliştirici listesi (fail-closed).
            Enabled = true,
            DeveloperAccounts = authorized
                ? new List<string> { "dev" }
                : new List<string>()
        };

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.Username).Returns("dev");
        currentUser.Setup(item => item.HasPermission(ApplicationPermission.AccessDiagnostics)).Returns(authorized);

        var guard = new DiagnosticAccessGuard(currentUser.Object, options);
        var store = new InMemoryDiagnosticSink(100);
        var data = new DiagnosticDataService(store, guard);

        var configuration = new ConfigurationBuilder().Build();

        return new DevCenterViewModel(
            new OverviewTabViewModel(data),
            new LiveLogsTabViewModel(data),
            new ErrorCenterTabViewModel(data),
            new CrashReportsTabViewModel(data),
            new PerformanceTabViewModel(),
            new BindingIssuesTabViewModel(data),
            new BuildAnalysisTabViewModel(),
            new AutomatedTestsTabViewModel(),
            new HealthTabViewModel(data, new Mock<IDbContextFactory<AppDbContext>>().Object, new Mock<IHttpClientFactory>().Object, configuration),
            new DiagnosticPackageTabViewModel(data),
            guard);
    }

    [Fact]
    public void Constructor_ExposesTenTabsInExpectedOrder()
    {
        using var viewModel = CreateViewModel(authorized: true);

        viewModel.Tabs.Should().HaveCount(10);
        viewModel.Tabs.Select(tab => tab.Title).Should().ContainInOrder(
            "Genel Bakış",
            "Canlı Loglar",
            "Hata Merkezi",
            "Çökme ve Donma",
            "Performans",
            "XAML / Binding",
            "Derleme & Kod Analizi",
            "Otomatik Testler",
            "API / DB Sağlığı",
            "Tanılama Paketi");
        viewModel.SelectedTab.Should().BeSameAs(viewModel.Tabs[0]);
    }

    [Fact]
    public void Tabs_StillUnavailable_AreMarkedUnavailable()
    {
        using var viewModel = CreateViewModel(authorized: true);

        var unavailable = viewModel.Tabs.Where(tab => !tab.IsAvailable).Select(tab => tab.Title).ToList();

        unavailable.Should().BeEquivalentTo(
            "Derleme & Kod Analizi",
            "Otomatik Testler");
        viewModel.Tabs.Where(tab => !tab.IsAvailable)
            .Should().OnlyContain(tab => !string.IsNullOrWhiteSpace(tab.AvailabilityNote));
        viewModel.Tabs.Single(tab => tab.Title == "Tanılama Paketi").IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenUnauthorized_ReportsDenialWithoutThrowing()
    {
        using var viewModel = CreateViewModel(authorized: false);

        await viewModel.InitializeAsync();

        viewModel.IsAuthorized.Should().BeFalse();
        viewModel.StatusText.Should().Contain("DeveloperAccounts");
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var viewModel = CreateViewModel(authorized: true);

        viewModel.Invoking(item => item.Dispose()).Should().NotThrow();
        viewModel.Invoking(item => item.Dispose()).Should().NotThrow();
    }
}
