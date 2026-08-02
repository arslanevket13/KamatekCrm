using FluentAssertions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Services;
using KamatekCrm.Shared.Models;
using Moq;

namespace KamatekCrm.Tests.Services;

public class ApplicationAuthorizationServiceTests
{
    [Fact]
    public void Authorize_WhenSessionIsMissing_ReturnsFailure()
    {
        var service = new ApplicationAuthorizationService(new FakeCurrentUserContext(false, false));

        var result = service.Authorize(ApplicationPermission.AccessSettings);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("oturum");
    }

    [Fact]
    public void Authorize_WhenPermissionIsMissing_ReturnsNamedFailure()
    {
        var service = new ApplicationAuthorizationService(new FakeCurrentUserContext(true, false));

        var result = service.Authorize(ApplicationPermission.ApprovePurchase);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Satın alma onayı");
    }

    [Fact]
    public void Authorize_WhenPermissionExists_Succeeds()
    {
        var service = new ApplicationAuthorizationService(new FakeCurrentUserContext(true, true));

        service.Authorize(ApplicationPermission.ManageServiceJobs).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void DesktopContext_TechnicianCanManageJobsButCannotAdjustInventoryByDefault()
    {
        var auth = new Mock<IAuthService>();
        auth.SetupGet(item => item.IsLoggedIn).Returns(true);
        auth.SetupGet(item => item.CurrentUser).Returns(new User { Id = 7, Username = "tech", Role = "Technician" });
        var context = new DesktopCurrentUserContext(auth.Object);

        context.HasPermission(ApplicationPermission.ManageServiceJobs).Should().BeTrue();
        context.HasPermission(ApplicationPermission.ManageQuotes).Should().BeTrue();
        context.HasPermission(ApplicationPermission.ExecuteSales).Should().BeTrue();
        context.HasPermission(ApplicationPermission.AdjustInventory).Should().BeFalse();
        context.HasPermission(ApplicationPermission.ManageUsers).Should().BeFalse();
    }

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        private readonly bool _allowed;

        public FakeCurrentUserContext(bool isAuthenticated, bool allowed)
        {
            IsAuthenticated = isAuthenticated;
            _allowed = allowed;
        }

        public bool IsAuthenticated { get; }
        public int? UserId => 1;
        public string Username => "test";
        public string Role => "Technician";
        public bool HasPermission(ApplicationPermission permission) => _allowed;
    }
}
