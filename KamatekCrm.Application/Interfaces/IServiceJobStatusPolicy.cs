using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.Interfaces;

public interface IServiceJobStatusPolicy
{
    Result ValidateTransition(JobStatus currentStatus, JobStatus requestedStatus);
    IReadOnlyCollection<JobStatus> GetAllowedTransitions(JobStatus currentStatus);
}

