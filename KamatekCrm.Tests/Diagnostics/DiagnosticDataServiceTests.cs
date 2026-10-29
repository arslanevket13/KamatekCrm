using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Configuration;
using KamatekCrm.Diagnostics.Authorization;
using KamatekCrm.Diagnostics.Core;
using Xunit;

namespace KamatekCrm.Tests.Diagnostics;

/// <summary>
/// Dev Center veri kapısının yetki denetimini her çağrıda uyguladığını doğrular.
/// </summary>
public sealed class DiagnosticDataServiceTests
{
    private static DiagnosticEvent CreateEvent(string message, DiagnosticLevel level = DiagnosticLevel.Information, string module = "Test")
        => new()
        {
            Timestamp = DateTimeOffset.Now,
            Level = level,
            Module = module,
            Message = message
        };

    private static DiagnosticAccessGuard CreateGuard(bool authorized)
    {
        var options = new DiagnosticOptions
        {
            Enabled = authorized,
            DeveloperAccounts = new List<string> { "dev" }
        };

        return new DiagnosticAccessGuard(new FakeCurrentUserContext(authorized, authorized, "dev"), options);
    }

    private static InMemoryDiagnosticSink CreateStore()
    {
        var store = new InMemoryDiagnosticSink(100);
        store.Publish(CreateEvent("birinci"));
        store.Publish(CreateEvent("ikinci", DiagnosticLevel.Error));
        return store;
    }

    [Fact]
    public void Snapshot_WhenUnauthorized_Throws()
    {
        var service = new DiagnosticDataService(CreateStore(), CreateGuard(authorized: false));

        var act = () => service.Snapshot();

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Query_WhenUnauthorized_Throws()
    {
        var service = new DiagnosticDataService(CreateStore(), CreateGuard(authorized: false));

        var act = () => service.Query(new DiagnosticQuery());

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void GetStatistics_WhenUnauthorized_Throws()
    {
        var service = new DiagnosticDataService(CreateStore(), CreateGuard(authorized: false));

        var act = () => service.GetStatistics();

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void GetEnvironmentSnapshot_WhenUnauthorized_Throws()
    {
        var service = new DiagnosticDataService(CreateStore(), CreateGuard(authorized: false));

        var act = () => service.GetEnvironmentSnapshot();

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void GetCrashArtifacts_WhenUnauthorized_Throws()
    {
        var service = new DiagnosticDataService(CreateStore(), CreateGuard(authorized: false));

        var act = () => service.GetCrashArtifacts();

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void GetStatistics_WhenAuthorized_ReportsBufferState()
    {
        var store = CreateStore();
        var service = new DiagnosticDataService(store, CreateGuard(authorized: true));

        var stats = service.GetStatistics();

        stats.IsEnabled.Should().BeTrue();
        stats.Count.Should().Be(2);
        stats.Capacity.Should().Be(store.Capacity);
        stats.DroppedCount.Should().Be(0);
    }

    [Fact]
    public void Query_WhenAuthorized_FiltersByLevel()
    {
        var service = new DiagnosticDataService(CreateStore(), CreateGuard(authorized: true));

        var events = service.Query(new DiagnosticQuery { MinimumLevel = DiagnosticLevel.Error });

        events.Should().HaveCount(1);
        events[0].Message.Should().Be("ikinci");
    }

    [Fact]
    public void GetEnvironmentSnapshot_WhenAuthorized_ReturnsProcessInformation()
    {
        var service = new DiagnosticDataService(CreateStore(), CreateGuard(authorized: true));

        var snapshot = service.GetEnvironmentSnapshot();

        snapshot.ProcessId.Should().Be(Environment.ProcessId);
        snapshot.MachineName.Should().NotBeNullOrWhiteSpace();
        snapshot.OperatingSystem.Should().NotBeNullOrWhiteSpace();
        snapshot.RuntimeVersion.Should().NotBeNullOrWhiteSpace();
        snapshot.LogDirectory.Should().NotBeNullOrWhiteSpace();
        snapshot.DiagnosticsDirectory.Should().NotBeNullOrWhiteSpace();
        snapshot.WorkingSetBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetCrashArtifacts_WhenAuthorized_ReturnsListWithoutThrowing()
    {
        var service = new DiagnosticDataService(CreateStore(), CreateGuard(authorized: true));

        var artifacts = service.GetCrashArtifacts();

        artifacts.Should().NotBeNull();
        artifacts.Should().HaveCountLessThanOrEqualTo(50);
        // Boş liste geçerli bir sonuçtur (çökme izi olmayabilir); varsa hepsi JSON olmalı.
        artifacts
            .Where(item => !item.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Should().BeEmpty();
    }

    [Fact]
    public void CreatePackage_WhenUnauthorized_Throws()
    {
        var service = new DiagnosticDataService(CreateStore(), CreateGuard(authorized: false));

        var act = () => service.CreatePackage();

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void GetPackages_WhenUnauthorized_Throws()
    {
        var service = new DiagnosticDataService(CreateStore(), CreateGuard(authorized: false));

        var act = () => service.GetPackages();

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void DeletePackage_WhenUnauthorized_Throws()
    {
        var service = new DiagnosticDataService(CreateStore(), CreateGuard(authorized: false));

        var act = () => service.DeletePackage("tanilama-x.zip");

        act.Should().Throw<UnauthorizedAccessException>();
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
