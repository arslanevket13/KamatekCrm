namespace KamatekCrm.ApplicationCore.Interfaces
{
    public interface IInteractionEventNotifier
    {
        void NotifyInteractionChanged(int interactionId, string interactionNumber, int? customerId, string changeType);
    }
}
