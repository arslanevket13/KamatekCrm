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

        /// <summary>
        /// Tamir durumu geçişini doğrular. Terminal durumlardan geri dönüşü ve geçersiz atlamaları engeller.
        /// </summary>
        public static bool CanTransition(RepairStatus from, RepairStatus to, out string? reason)
        {
            reason = null;
            if (from == to) return true;

            if (from == RepairStatus.Delivered)
            {
                reason = "Teslim edilmiş bir cihazın durumu geriye dönük olarak değiştirilemez.";
                return false;
            }
            if (from == RepairStatus.Unrepairable)
            {
                reason = "Tamir edilemez olarak kapatılmış bir cihazın durumu değiştirilemez.";
                return false;
            }

            if (to == RepairStatus.Delivered && from is RepairStatus.Registered)
            {
                reason = "Kayıt aşamasındaki cihaz doğrudan teslim edilemez; önce teşhis ve onarım tamamlanmalıdır.";
                return false;
            }

            return true;
        }

        public static bool CanTransition(RepairStatus from, RepairStatus to) =>
            CanTransition(from, to, out _);
    }
}
