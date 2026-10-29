using Serilog.Core;
using Serilog.Events;

namespace KamatekCrm.Diagnostics.Core
{
    /// <summary>
    /// Aktif korelasyon kimliğini her Serilog olayına ekler.
    /// Böylece dosya logları, bellek içi tampon ve ileride API/DB çağrıları
    /// aynı kimlik üzerinden eşleştirilebilir.
    /// </summary>
    public sealed class CorrelationIdEnricher : ILogEventEnricher
    {
        public const string PropertyName = "CorrelationId";

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent == null || propertyFactory == null)
            {
                return;
            }

            if (logEvent.Properties.ContainsKey(PropertyName))
            {
                return;
            }

            var correlationId = CorrelationContext.Current;
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                return;
            }

            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PropertyName, correlationId));
        }
    }
}
