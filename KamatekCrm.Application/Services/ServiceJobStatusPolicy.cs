using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.Services;

/// <summary>
/// İş emrinin yaşam döngüsünü tek yerde tanımlar. UI katmanlarının doğrudan ve
/// birbiriyle çelişen durum atamaları yapmasını engeller.
/// </summary>
public sealed class ServiceJobStatusPolicy : IServiceJobStatusPolicy
{
    private static readonly IReadOnlyDictionary<JobStatus, JobStatus[]> AllowedTransitions =
        new Dictionary<JobStatus, JobStatus[]>
        {
            [JobStatus.Pending] = [JobStatus.InProgress, JobStatus.PendingDiscovery, JobStatus.Cancelled],
            [JobStatus.PendingDiscovery] = [JobStatus.DiscoveryCompleted, JobStatus.Cancelled],
            [JobStatus.DiscoveryCompleted] = [JobStatus.Quoting, JobStatus.InProgress, JobStatus.Cancelled],
            [JobStatus.Quoting] = [JobStatus.WaitingForApproval, JobStatus.InProgress, JobStatus.Rejected, JobStatus.Cancelled],
            [JobStatus.WaitingForApproval] = [JobStatus.InProgress, JobStatus.Rejected, JobStatus.Cancelled],
            [JobStatus.InProgress] = [JobStatus.WaitingForParts, JobStatus.WaitingForApproval, JobStatus.Completed, JobStatus.Cancelled],
            [JobStatus.WaitingForParts] = [JobStatus.InProgress, JobStatus.Cancelled],
            [JobStatus.Rejected] = [JobStatus.Quoting, JobStatus.Cancelled],
            [JobStatus.Completed] = [],
            [JobStatus.Cancelled] = []
        };

    public Result ValidateTransition(JobStatus currentStatus, JobStatus requestedStatus)
    {
        if (currentStatus == requestedStatus)
        {
            return Result.Success();
        }

        return GetAllowedTransitions(currentStatus).Contains(requestedStatus)
            ? Result.Success()
            : Result.Failure($"{GetDisplayName(currentStatus)} durumundan {GetDisplayName(requestedStatus)} durumuna geçilemez.");
    }

    public IReadOnlyCollection<JobStatus> GetAllowedTransitions(JobStatus currentStatus) =>
        AllowedTransitions.TryGetValue(currentStatus, out var transitions)
            ? transitions
            : Array.Empty<JobStatus>();

    private static string GetDisplayName(JobStatus status) => status switch
    {
        JobStatus.Pending => "Bekliyor",
        JobStatus.InProgress => "Devam Ediyor",
        JobStatus.WaitingForParts => "Parça Bekleniyor",
        JobStatus.WaitingForApproval => "Onay Bekleniyor",
        JobStatus.Completed => "Tamamlandı",
        JobStatus.Cancelled => "İptal",
        JobStatus.Rejected => "Reddedildi",
        JobStatus.PendingDiscovery => "Keşif Bekliyor",
        JobStatus.DiscoveryCompleted => "Keşif Tamamlandı",
        JobStatus.Quoting => "Teklif Aşaması",
        _ => status.ToString()
    };
}
