using System;

namespace KamatekCrm.Exceptions
{
    /// <summary>
    /// Bağlı kayıt varken silme denendiğinde fırlatılan özel exception
    /// </summary>
    public class ReferentialIntegrityException : Exception
    {
        public string EntityType { get; }
        public int EntityId { get; }
        public string DependentEntityType { get; }
        public int DependentCount { get; }

        public ReferentialIntegrityException(string entityType, int entityId, string dependentEntityType, int dependentCount)
            : base($"'{entityType}' (ID: {entityId}) silinemez. {dependentCount} adet bağlı '{dependentEntityType}' kaydı mevcut.")
        {
            EntityType = entityType;
            EntityId = entityId;
            DependentEntityType = dependentEntityType;
            DependentCount = dependentCount;
        }

        public ReferentialIntegrityException(string message) : base(message)
        {
            EntityType = string.Empty;
            DependentEntityType = string.Empty;
        }

        public ReferentialIntegrityException(string message, Exception innerException) : base(message, innerException)
        {
            EntityType = string.Empty;
            DependentEntityType = string.Empty;
        }
    }
}
