using System;
using FluentAssertions;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Services
{
    public class SlaAndStatusTests
    {
        [Theory]
        [InlineData(JobStatus.WaitingForParts, true)]
        [InlineData(JobStatus.WaitingForApproval, true)]
        [InlineData(JobStatus.Pending, false)]
        [InlineData(JobStatus.InProgress, false)]
        [InlineData(JobStatus.Completed, false)]
        [InlineData(JobStatus.Cancelled, false)]
        public void SlaService_IsSlaPaused_ShouldReturnTrueOnlyForWaitingStatuses(JobStatus status, bool expectedPaused)
        {
            // Arrange
            var dbFactoryMock = new Mock<IDbContextFactory<AppDbContext>>();
            var slaService = new SlaService(dbFactoryMock.Object);

            // Act
            var isPaused = slaService.IsSlaPaused(status);

            // Assert
            isPaused.Should().Be(expectedPaused);
        }

        [Fact]
        public void SlaService_CalculateWorkingHoursDeadline_ShouldSkipSundaysAndNonWorkingHours()
        {
            // Arrange
            var dbFactoryMock = new Mock<IDbContextFactory<AppDbContext>>();
            var slaService = new SlaService(dbFactoryMock.Object);

            // Cumartesi 16:00 UTC başlangıç. Mesai 18:00'de bitiyor (kalan mesai 2 saat).
            // SLA süresi 4 saat (Cumartesi 2 saat kullanılır, Pazar atlanır, Pazartesi sabah 09:00'da kalan 2 saat başlar -> Pazartesi 11:00 biter).
            var saturdayAfternoon = new DateTime(2026, 10, 24, 16, 0, 0, DateTimeKind.Utc); // 24 Ekim 2026 Cumartesi
            var slaDuration = TimeSpan.FromHours(4);

            // Act
            var deadline = slaService.CalculateWorkingHoursDeadline(saturdayAfternoon, slaDuration);

            // Assert
            deadline.DayOfWeek.Should().Be(DayOfWeek.Monday);
            deadline.Hour.Should().Be(11);
            deadline.Minute.Should().Be(0);
        }

        [Theory]
        [InlineData(RepairStatus.Registered, RepairStatus.Diagnosing, true)]
        [InlineData(RepairStatus.Diagnosing, RepairStatus.InRepair, true)]
        [InlineData(RepairStatus.InRepair, RepairStatus.Testing, true)]
        [InlineData(RepairStatus.Testing, RepairStatus.ReadyForPickup, true)]
        [InlineData(RepairStatus.ReadyForPickup, RepairStatus.Delivered, true)]
        public void RepairStatusHelper_CanTransition_ShouldAllowValidTransitions(RepairStatus from, RepairStatus to, bool expectedCanTransition)
        {
            // Act
            var result = RepairStatusHelper.CanTransition(from, to, out var reason);

            // Assert
            result.Should().Be(expectedCanTransition);
            reason.Should().BeNull();
        }

        [Fact]
        public void RepairStatusHelper_CanTransition_ShouldBlockDirectDeliveryFromRegistered()
        {
            // Act: Kayıt aşamasındaki cihaz doğrudan teslim edilemez
            var result = RepairStatusHelper.CanTransition(RepairStatus.Registered, RepairStatus.Delivered, out var reason);

            // Assert
            result.Should().BeFalse();
            reason.Should().Contain("doğrudan teslim edilemez");
        }

        [Fact]
        public void RepairStatusHelper_CanTransition_ShouldBlockModification_WhenStatusIsDelivered()
        {
            // Act: Teslim edilmiş bir cihazın durumu geriye dönük değiştirilemez
            var result = RepairStatusHelper.CanTransition(RepairStatus.Delivered, RepairStatus.InRepair, out var reason);

            // Assert
            result.Should().BeFalse();
            reason.Should().Contain("Teslim edilmiş bir cihazın durumu");
        }

        [Fact]
        public void RepairStatusHelper_CanTransition_ShouldBlockModification_WhenStatusIsUnrepairable()
        {
            // Act: Tamir edilemez kapatılmış bir cihazın durumu değiştirilemez
            var result = RepairStatusHelper.CanTransition(RepairStatus.Unrepairable, RepairStatus.Diagnosing, out var reason);

            // Assert
            result.Should().BeFalse();
            reason.Should().Contain("Tamir edilemez olarak kapatılmış");
        }
    }
}
