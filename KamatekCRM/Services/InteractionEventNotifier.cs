using System;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Events;

namespace KamatekCrm.Services
{
    public class InteractionEventNotifier : IInteractionEventNotifier
    {
        public void NotifyInteractionChanged(int interactionId, string interactionNumber, int? customerId, string changeType)
        {
            var type = changeType switch
            {
                "Created" => InteractionChangeType.Created,
                "Updated" => InteractionChangeType.Updated,
                "StatusChanged" => InteractionChangeType.StatusChanged,
                "Converted" => InteractionChangeType.Converted,
                _ => InteractionChangeType.Updated
            };

            EventAggregator.Instance.Publish(new CustomerInteractionChangedEvent(
                interactionId,
                interactionNumber,
                customerId,
                type));
        }
    }
}
