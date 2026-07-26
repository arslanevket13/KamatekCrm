using System;
using System.Collections.Generic;

namespace KamatekCrm.Shared.Exceptions
{
    /// <summary>
    /// Alan/Domain iş kurallarının detaylı doğrulama hatası fırlatması durumunda kullanılır.
    /// </summary>
    public class BusinessRuleValidationException : DomainException
    {
        public string RuleName { get; }
        public IReadOnlyList<string> BrokenRules { get; }

        public BusinessRuleValidationException(string ruleName, string message) : base(message)
        {
            RuleName = ruleName;
            BrokenRules = new List<string> { message };
        }

        public BusinessRuleValidationException(string ruleName, string message, IEnumerable<string> brokenRules) : base(message)
        {
            RuleName = ruleName;
            BrokenRules = new List<string>(brokenRules);
        }
    }
}
