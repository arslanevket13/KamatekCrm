namespace KamatekCrm.Shared.Exceptions
{
    public class BusinessRuleException : DomainException
    {
        public string RuleName { get; }

        public BusinessRuleException(string ruleName, string message) : base(message)
        {
            RuleName = ruleName;
        }
    }
}
