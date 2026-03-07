using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Static helper for XAML CommandParameter bindings to RepairStatus enum values
    /// </summary>
    public static class RepairStatusHelper
    {
        public static RepairStatus Registered => RepairStatus.Registered;
        public static RepairStatus Diagnosing => RepairStatus.Diagnosing;
        public static RepairStatus WaitingForParts => RepairStatus.WaitingForParts;
        public static RepairStatus SentToFactory => RepairStatus.SentToFactory;
        public static RepairStatus ReturnedFromFactory => RepairStatus.ReturnedFromFactory;
        public static RepairStatus InRepair => RepairStatus.InRepair;
        public static RepairStatus Testing => RepairStatus.Testing;
        public static RepairStatus ReadyForPickup => RepairStatus.ReadyForPickup;
        public static RepairStatus Delivered => RepairStatus.Delivered;
        public static RepairStatus Unrepairable => RepairStatus.Unrepairable;
    }
}
