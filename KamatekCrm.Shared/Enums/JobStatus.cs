namespace KamatekCrm.Shared.Enums
{
    public enum JobStatus
    {
        Pending,
        InProgress,
        WaitingForParts,
        WaitingForApproval,
        Completed,
        Cancelled,
        Rejected // Müşterinin keşif sonrası teklifi reddetmesi
    }
}
