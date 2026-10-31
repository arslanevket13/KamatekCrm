using System;
using System.Collections.Generic;
using FluentAssertions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Configuration;
using KamatekCrm.Diagnostics.Authorization;
using KamatekCrm.Services;
using KamatekCrm.Shared.Models;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Diagnostics;

/// <summary>
/// Yetki kapısının fail-closed davranışını doğrular: eksik/boş yapılandırma erişim AÇMAZ.
/// </summary>
public sealed class DiagnosticAccessGuardTests
{
    private static DiagnosticOptions EnabledOptions(params string[] accounts) => new()
    {
        Enabled = true,
        DeveloperAccounts = new List<string>(accounts)
    };

    private static DiagnosticAccessGuard CreateGuard(
        DiagnosticOptions options,
        string username = "dev",
        bool isAuthenticated = true,
        bool hasPermission = true)
        => new(new FakeCurrentUserContext(isAuthenticated, hasPermission, username), options);

    [Fact]
    public void IsAuthorized_WhenDiagnosticsDisabled_ReturnsFalse()
    {
        var options = EnabledOptions("dev");
        options.Enabled = false;

        var guard = CreateGuard(options);

        guard.IsAuthorized.Should().BeFalse();
        guard.DenialReason.Should().Contain("kapalı");
    }

    [Fact]
    public void IsAuthorized_WhenSessionMissing_ReturnsFalse()
    {
        var guard = CreateGuard(EnabledOptions("dev"), isAuthenticated: false);

        guard.IsAuthorized.Should().BeFalse();
        guard.DenialReason.Should().Contain("oturum");
    }

    [Fact]
    public void IsAuthorized_WhenDeveloperAccountListIsEmpty_ReturnsFalse()
    {
        var guard = CreateGuard(EnabledOptions());

        guard.IsAuthorized.Should().BeFalse();
        guard.DenialReason.Should().Contain("DeveloperAccounts");
    }

    [Fact]
    public void IsAuthorized_WhenUserIsNotListed_ReturnsFalseEvenForAdmin()
    {
        var guard = CreateGuard(EnabledOptions("baska-gelistirici"), username: "admin");

        guard.IsAuthorized.Should().BeFalse();
        guard.DenialReason.Should().Contain("yetkisi yok");
    }

    [Fact]
    public void IsAuthorized_WhenAccountIsListedIgnoringCase_ReturnsTrue()
    {
        var guard = CreateGuard(EnabledOptions("DevUser"), username: "devuser");

        guard.IsListedDeveloper.Should().BeTrue();
        guard.IsAuthorized.Should().BeTrue();
        guard.DenialReason.Should().BeEmpty();
        guard.Invoking(item => item.EnsureAuthorized()).Should().NotThrow();
    }

    [Fact]
    public void IsAuthorized_WhenPermissionDenied_ReturnsFalse()
    {
        var guard = CreateGuard(EnabledOptions("dev"), hasPermission: false);

        guard.IsAuthorized.Should().BeFalse();
        guard.DenialReason.Should().Contain("yetkisi yok");
    }

    [Fact]
    public void EnsureAuthorized_WhenUnauthorized_ThrowsWithDenialReason()
    {
        var guard = CreateGuard(EnabledOptions());

        var act = () => guard.EnsureAuthorized();

        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*DeveloperAccounts*");
    }

    [Fact]
    public void DesktopContext_AccessDiagnosticsRequiresAllowlistEntry()
    {
        var options = EnabledOptions("devuser");
        var context = CreateDesktopContext("devuser", "Admin", options);

        context.HasPermission(ApplicationPermission.AccessDiagnostics).Should().BeTrue();
    }

    [Fact]
    public void DesktopContext_AdminWithoutAllowlistEntryHasNoDiagnosticsPermission()
    {
        var context = CreateDesktopContext("admin", "Admin", EnabledOptions("devuser"));

        context.HasPermission(ApplicationPermission.AccessDiagnostics).Should().BeFalse();
    }

    [Fact]
    public void DesktopContext_EmptyAllowlistClosesDiagnosticsEvenForAdmin()
    {
        var context = CreateDesktopContext("admin", "Admin", EnabledOptions());

        context.HasPermission(ApplicationPermission.AccessDiagnostics).Should().BeFalse();
    }

    [Fact]
    public void DesktopContext_WithoutOptionsClosesDiagnostics()
    {
        var context = CreateDesktopContext("devuser", "Admin", options: null);

        context.HasPermission(ApplicationPermission.AccessDiagnostics).Should().BeFalse();
    }

    [Fact]
    public void DesktopContext_AdminWithIsAdminTrueAndAllowlistEntry_HasDiagnosticsPermission()
    {
        var auth = new Mock<IAuthService>();
        auth.SetupGet(item => item.IsLoggedIn).Returns(true);
        auth.SetupGet(item => item.IsAdmin).Returns(true);
        auth.SetupGet(item => item.CurrentUser).Returns(new User { Id = 1, Username = "admin", Role = "Admin" });

        var context = new DesktopCurrentUserContext(auth.Object, EnabledOptions("admin"));

        context.HasPermission(ApplicationPermission.AccessDiagnostics).Should().BeTrue();
    }

    [Fact]
    public void DesktopContext_AdminWithIsAdminTrueWithoutAllowlistEntry_HasNoDiagnosticsPermission()
    {
        var auth = new Mock<IAuthService>();
        auth.SetupGet(item => item.IsLoggedIn).Returns(true);
        auth.SetupGet(item => item.IsAdmin).Returns(true);
        auth.SetupGet(item => item.CurrentUser).Returns(new User { Id = 1, Username = "admin", Role = "Admin" });

        var context = new DesktopCurrentUserContext(auth.Object, EnabledOptions("otherdev"));

        context.HasPermission(ApplicationPermission.AccessDiagnostics).Should().BeFalse();
    }

    private static DesktopCurrentUserContext CreateDesktopContext(string username, string role, DiagnosticOptions? options)
    {
        var auth = new Mock<IAuthService>();
        auth.SetupGet(item => item.IsLoggedIn).Returns(true);
        auth.SetupGet(item => item.CurrentUser).Returns(new User { Id = 1, Username = username, Role = role });

        return new DesktopCurrentUserContext(auth.Object, options);
    }

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        private readonly bool _allowed;

        public FakeCurrentUserContext(bool isAuthenticated, bool allowed, string username)
        {
            IsAuthenticated = isAuthenticated;
            _allowed = allowed;
            Username = username;
        }

        public bool IsAuthenticated { get; }
        public int? UserId => 1;
        public string Username { get; }
        public string Role => "Admin";
        public bool HasPermission(ApplicationPermission permission) => _allowed;
    }
}
