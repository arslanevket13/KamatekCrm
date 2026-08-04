using FluentAssertions;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Tests.Services;

public class ServiceJobStatusPolicyTests
{
    private readonly ServiceJobStatusPolicy _policy = new();

    [Theory]
    [InlineData(JobStatus.Pending, JobStatus.InProgress)]
    [InlineData(JobStatus.InProgress, JobStatus.WaitingForParts)]
    [InlineData(JobStatus.InProgress, JobStatus.Completed)]
    [InlineData(JobStatus.DiscoveryCompleted, JobStatus.Quoting)]
    [InlineData(JobStatus.Rejected, JobStatus.Quoting)]
    [InlineData(JobStatus.DiscoveryRequest, JobStatus.ConvertedToQuote)]
    [InlineData(JobStatus.ConvertedToQuote, JobStatus.InstallationPlanned)]
    [InlineData(JobStatus.InstallationPlanned, JobStatus.InstallationCompleted)]
    [InlineData(JobStatus.DiscoveryRequest, JobStatus.Cancelled)]
    [InlineData(JobStatus.ConvertedToQuote, JobStatus.Cancelled)]
    [InlineData(JobStatus.InstallationPlanned, JobStatus.Cancelled)]
    public void ValidateTransition_AllowsDefinedWorkflowSteps(JobStatus current, JobStatus requested)
    {
        _policy.ValidateTransition(current, requested).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(JobStatus.Pending, JobStatus.Completed)]
    [InlineData(JobStatus.Completed, JobStatus.InProgress)]
    [InlineData(JobStatus.Cancelled, JobStatus.Pending)]
    [InlineData(JobStatus.WaitingForParts, JobStatus.Completed)]
    [InlineData(JobStatus.DiscoveryRequest, JobStatus.InstallationCompleted)]
    [InlineData(JobStatus.InstallationCompleted, JobStatus.InstallationPlanned)]
    public void ValidateTransition_RejectsWorkflowShortcuts(JobStatus current, JobStatus requested)
    {
        var result = _policy.ValidateTransition(current, requested);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("geçilemez");
    }

    [Fact]
    public void ValidateTransition_IsIdempotentForSameStatus()
    {
        _policy.ValidateTransition(JobStatus.Completed, JobStatus.Completed).IsSuccess.Should().BeTrue();
    }
}
