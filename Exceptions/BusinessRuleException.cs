namespace KamatekCrm.Exceptions
{
    public class BusinessRuleException : Exception
    {
        public string RuleName { get; }

        public BusinessRuleException(string ruleName, string message) : base(message)
        {
            RuleName = ruleName;
        }
    }
}
