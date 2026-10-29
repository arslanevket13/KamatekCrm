using System;

namespace KamatekCrm.Events
{
    public enum InteractionChangeType
    {
        Created,
        Updated,
        StatusChanged,
        Converted
    }

    /// <summary>
    /// Müşteri iletişim/talep kaydı oluşturulduğunda, güncellendiğinde veya dönüştürüldüğünde yayınlanan event.
    /// EventAggregator üzerinden açık olan ViewModel'lerin canlı senkronizasyonunu sağlar.
    /// </summary>
    public class CustomerInteractionChangedEvent
    {
        public int InteractionId { get; }
        public string InteractionNumber { get; }
        public int? CustomerId { get; }
        public InteractionChangeType ChangeType { get; }
        public DateTime Timestamp { get; }

        public CustomerInteractionChangedEvent(
            int interactionId, 
            string interactionNumber, 
            int? customerId, 
            InteractionChangeType changeType)
        {
            InteractionId = interactionId;
            InteractionNumber = interactionNumber ?? string.Empty;
            CustomerId = customerId;
            ChangeType = changeType;
            Timestamp = DateTime.UtcNow;
        }
    }
}

