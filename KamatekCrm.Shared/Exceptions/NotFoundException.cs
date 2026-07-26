namespace KamatekCrm.Shared.Exceptions
{
    public class NotFoundException : DomainException
    {
        public string EntityName { get; }
        public object EntityId { get; }

        public NotFoundException(string entityName, object entityId) 
            : base($"{entityName} with ID '{entityId}' was not found")
        {
            EntityName = entityName;
            EntityId = entityId;
        }
    }
}
