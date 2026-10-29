using System;
using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;

namespace KamatekCrm.Diagnostics.Core
{
    /// <summary>
    /// Serilog olaylarını <see cref="DiagnosticEvent"/> modeline çevirip belleğe yazan köprü.
    /// <para>
    /// Bu sınıf mevcut Serilog hattına eklenir; uygulama kodunda <c>Log.Information(...)</c>
    /// kullanımı değişmez. Sink içinde oluşan her hata yutulur; tanılama altyapısı
    /// ana uygulamayı asla bozmaz.
    /// </para>
    /// </summary>
    public sealed class SerilogSinkAdapter : ILogEventSink
    {
        private const int MaxProperties = 12;

        private static readonly HashSet<string> ReservedProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            "SourceContext",
            "CorrelationId",
            "EventCode",
            "ErrorCode",
            "ThreadId",
            "MachineName",
            "ProcessId",
            "RequestId",
            "RequestPath",
            "SpanId",
            "TraceId"
        };

        private readonly IDiagnosticSink _sink;

        public SerilogSinkAdapter(IDiagnosticSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null)
            {
                return;
            }

            try
            {
                _sink.Publish(Map(logEvent));
            }
            catch
            {
                // Tanılama hattındaki hata loglamayı veya uygulamayı durdurmamalı.
            }
        }

        /// <summary>
        /// Serilog olayını tanılama modeline çevirir. Saf fonksiyondur; test ve dışa aktarma
        /// araçları doğrudan kullanabilir.
        /// </summary>
        public static DiagnosticEvent Map(LogEvent logEvent)
        {
            var exception = logEvent.Exception;

            return new DiagnosticEvent
            {
                Timestamp = logEvent.Timestamp,
                Level = ToDiagnosticLevel(logEvent.Level),
                Module = ResolveModule(logEvent),
                Message = LogRedactor.RedactAndTruncate(RenderMessage(logEvent), 4000),
                EventCode = GetScalarString(logEvent, "EventCode") ?? GetScalarString(logEvent, "ErrorCode"),
                CorrelationId = GetScalarString(logEvent, "CorrelationId") ?? CorrelationContext.Current,
                ThreadId = GetScalarInt(logEvent, "ThreadId") ?? Environment.CurrentManagedThreadId,
                ProcessId = GetScalarInt(logEvent, "ProcessId") ?? Environment.ProcessId,
                ExceptionType = exception?.GetType().FullName,
                ExceptionMessage = exception == null
                    ? null
                    : LogRedactor.RedactAndTruncate(exception.Message, 2000),
                StackTrace = exception == null
                    ? null
                    : LogRedactor.RedactAndTruncate(exception.ToString(), 20000),
                Properties = CollectProperties(logEvent)
            };
        }

        private static string RenderMessage(LogEvent logEvent)
        {
            try
            {
                return logEvent.RenderMessage();
            }
            catch
            {
                return logEvent.MessageTemplate.Text;
            }
        }

        private static string ResolveModule(LogEvent logEvent)
        {
            var sourceContext = GetScalarString(logEvent, "SourceContext");
            if (string.IsNullOrWhiteSpace(sourceContext))
            {
                return "App";
            }

            var lastDot = sourceContext!.LastIndexOf('.');
            return lastDot >= 0 && lastDot < sourceContext.Length - 1
                ? sourceContext[(lastDot + 1)..]
                : sourceContext;
        }

        private static IReadOnlyDictionary<string, string?>? CollectProperties(LogEvent logEvent)
        {
            if (logEvent.Properties.Count == 0)
            {
                return null;
            }

            Dictionary<string, string?>? collected = null;
            var count = 0;

            foreach (var pair in logEvent.Properties)
            {
                if (count >= MaxProperties)
                {
                    break;
                }

                if (ReservedProperties.Contains(pair.Key))
                {
                    continue;
                }

                if (pair.Value is not ScalarValue scalar || scalar.Value == null)
                {
                    continue;
                }

                collected ??= new Dictionary<string, string?>(StringComparer.Ordinal);
                collected[pair.Key] = scalar.Value.ToString();
                count++;
            }

            return LogRedactor.RedactProperties(collected);
        }

        private static string? GetScalarString(LogEvent logEvent, string propertyName)
        {
            if (!logEvent.Properties.TryGetValue(propertyName, out var value) || value is not ScalarValue scalar)
            {
                return null;
            }

            return scalar.Value as string ?? scalar.Value?.ToString();
        }

        private static int? GetScalarInt(LogEvent logEvent, string propertyName)
        {
            if (!logEvent.Properties.TryGetValue(propertyName, out var value) || value is not ScalarValue scalar)
            {
                return null;
            }

            return scalar.Value switch
            {
                int intValue => intValue,
                long longValue when longValue is >= int.MinValue and <= int.MaxValue => (int)longValue,
                _ => null
            };
        }

        public static DiagnosticLevel ToDiagnosticLevel(LogEventLevel level) => level switch
        {
            LogEventLevel.Verbose => DiagnosticLevel.Verbose,
            LogEventLevel.Debug => DiagnosticLevel.Debug,
            LogEventLevel.Information => DiagnosticLevel.Information,
            LogEventLevel.Warning => DiagnosticLevel.Warning,
            LogEventLevel.Error => DiagnosticLevel.Error,
            LogEventLevel.Fatal => DiagnosticLevel.Fatal,
            _ => DiagnosticLevel.Information
        };
    }
}
