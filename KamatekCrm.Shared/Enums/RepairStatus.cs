namespace KamatekCrm.Shared.Enums
{
    public enum RepairStatus
    {
        Registered,
        Pending,
        Diagnosing,
        InProgress,
        InRepair, // Alias for InProgress? Added for compatibility.
        WaitingForParts,
        SentToFactory,
        ReturnedFromFactory,
        Testing,
        ReadyForPickup,
        Completed,
        Delivered,
        Unrepairable,
        Cancelled
    }
}
