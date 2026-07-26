using System;

namespace KamatekCrm.Shared.Exceptions
{
    /// <summary>
    /// Tüm Domain ve İş Kuralı istisnalarının taban sınıfıdır.
    /// Global exception handler ve Serilog telemetry tarafından öncelikli yakalanır.
    /// </summary>
    public abstract class DomainException : Exception
    {
        protected DomainException()
        {
        }

        protected DomainException(string message) : base(message)
        {
        }

        protected DomainException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
