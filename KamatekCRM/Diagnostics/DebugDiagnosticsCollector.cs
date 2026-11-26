#if DEBUG
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using Serilog.Core;
using Serilog.Events;

namespace KamatekCrm.Diagnostics
{
    /// <summary>
    /// Yalnızca DEBUG derlemelerinde aktif olan canlı teşhis toplayıcısı.
    /// WPF DataBinding/Resource hatalarını ve Serilog Warning/Error/Fatal kayıtlarını
    /// sabit boyutlu (son 100 kayıt) bellek içi kuyruklarda tutar.
    /// </summary>
    public sealed class DebugDiagnosticsCollector
    {
        private const int MaxEntries = 100;

        public static DebugDiagnosticsCollector Instance { get; } = new();

        private readonly ConcurrentQueue<BindingDiagnosticEntry> _bindingErrors = new();
        private readonly ConcurrentQueue<SerilogDiagnosticEntry> _serilogEvents = new();
        private readonly object _bindingTrimLock = new();
        private readonly object _serilogTrimLock = new();
        private readonly CollectorBindingTraceListener _traceListener;

        public ILogEventSink SerilogSink { get; }

        private DebugDiagnosticsCollector()
        {
            _traceListener = new CollectorBindingTraceListener(this);
            SerilogSink = new CollectorSerilogSink(this);
        }

        /// <summary>
        /// WPF PresentationTraceSources (DataBinding ve ResourceDictionary) kaynaklarına
        /// bellek içi dinleyiciyi güvenli ve tekil biçimde bağlar.
        /// </summary>
        public void AttachWpfTraceListeners()
        {
            try
            {
                PresentationTraceSources.Refresh();

                AttachToSource(PresentationTraceSources.DataBindingSource);
                AttachToSource(PresentationTraceSources.ResourceDictionarySource);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "DebugDiagnosticsCollector WPF TraceListener bağlantısı sırasında hata oluştu.");
            }
        }

        private void AttachToSource(TraceSource? source)
        {
            if (source == null)
            {
                return;
            }

            if (source.Switch.Level == SourceLevels.Off || source.Switch.Level > SourceLevels.Warning)
            {
                source.Switch.Level = SourceLevels.Warning;
            }

            if (!source.Listeners.OfType<CollectorBindingTraceListener>().Any())
            {
                source.Listeners.Add(_traceListener);
            }
        }

        internal void RecordBindingError(string? rawMessage)
        {
            if (string.IsNullOrWhiteSpace(rawMessage))
            {
                return;
            }

            string normalized = Truncate(rawMessage.Trim(), 1200);
            _bindingErrors.Enqueue(new BindingDiagnosticEntry(
                DateTimeOffset.UtcNow.ToString("O"),
                normalized));

            lock (_bindingTrimLock)
            {
                while (_bindingErrors.Count > MaxEntries && _bindingErrors.TryDequeue(out _))
                {
                }
            }
        }

        internal void RecordSerilogEvent(LogEvent logEvent)
        {
            if (logEvent.Level < LogEventLevel.Warning)
            {
                return;
            }

            string renderedMessage;
            try
            {
                renderedMessage = Truncate(logEvent.RenderMessage(), 1000);
            }
            catch (Exception ex)
            {
                renderedMessage = $"[LogRenderError: {ex.Message}]";
            }

            string? exceptionSummary = null;
            if (logEvent.Exception != null)
            {
                string baseEx = $"{logEvent.Exception.GetType().Name}: {logEvent.Exception.Message}";
                if (logEvent.Exception.InnerException != null)
                {
                    baseEx += $" -> {logEvent.Exception.InnerException.GetType().Name}: {logEvent.Exception.InnerException.Message}";
                }

                exceptionSummary = Truncate(baseEx, 600);
            }

            _serilogEvents.Enqueue(new SerilogDiagnosticEntry(
                logEvent.Timestamp.ToUniversalTime().ToString("O"),
                logEvent.Level.ToString(),
                renderedMessage,
                exceptionSummary));

            lock (_serilogTrimLock)
            {
                while (_serilogEvents.Count > MaxEntries && _serilogEvents.TryDequeue(out _))
                {
                }
            }
        }

        /// <summary>
        /// Mevcut WPF binding ve Serilog hata özetini döndürür; isteğe bağlı olarak tamponları temizler.
        /// </summary>
        public DebugDiagnosticsSnapshot GetSnapshotAndOptionallyClear(bool clear)
        {
            BindingDiagnosticEntry[] bindingSnapshot = _bindingErrors.ToArray();
            SerilogDiagnosticEntry[] serilogSnapshot = _serilogEvents.ToArray();

            if (clear)
            {
                lock (_bindingTrimLock)
                {
                    while (_bindingErrors.TryDequeue(out _))
                    {
                    }
                }

                lock (_serilogTrimLock)
                {
                    while (_serilogEvents.TryDequeue(out _))
                    {
                    }
                }
            }

            return new DebugDiagnosticsSnapshot(
                DateTimeOffset.UtcNow.ToString("O"),
                clear,
                bindingSnapshot.Length,
                bindingSnapshot,
                serilogSnapshot.Length,
                serilogSnapshot);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (value.Length <= maxLength)
            {
                return value;
            }

            return string.Concat(value.AsSpan(0, maxLength), "...");
        }

        private sealed class CollectorBindingTraceListener : TraceListener
        {
            [ThreadStatic]
            private static StringBuilder? _pendingBuffer;

            private readonly DebugDiagnosticsCollector _owner;

            public CollectorBindingTraceListener(DebugDiagnosticsCollector owner)
            {
                _owner = owner;
            }

            public override void Write(string? message)
            {
                if (string.IsNullOrEmpty(message))
                {
                    return;
                }

                _pendingBuffer ??= new StringBuilder(256);
                _pendingBuffer.Append(message);
            }

            public override void WriteLine(string? message)
            {
                if (_pendingBuffer != null && _pendingBuffer.Length > 0)
                {
                    if (!string.IsNullOrEmpty(message))
                    {
                        _pendingBuffer.Append(message);
                    }

                    string combined = _pendingBuffer.ToString();
                    _pendingBuffer.Clear();
                    _owner.RecordBindingError(combined);
                    return;
                }

                _owner.RecordBindingError(message);
            }
        }

        private sealed class CollectorSerilogSink : ILogEventSink
        {
            private readonly DebugDiagnosticsCollector _owner;

            public CollectorSerilogSink(DebugDiagnosticsCollector owner)
            {
                _owner = owner;
            }

            public void Emit(LogEvent logEvent)
            {
                if (logEvent != null)
                {
                    _owner.RecordSerilogEvent(logEvent);
                }
            }
        }
    }

    public sealed record BindingDiagnosticEntry(
        [property: JsonPropertyName("timestamp")] string Timestamp,
        [property: JsonPropertyName("message")] string Message);

    public sealed record SerilogDiagnosticEntry(
        [property: JsonPropertyName("timestamp")] string Timestamp,
        [property: JsonPropertyName("level")] string Level,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("exception")] string? Exception);

    public sealed record DebugDiagnosticsSnapshot(
        [property: JsonPropertyName("capturedAtUtc")] string CapturedAtUtc,
        [property: JsonPropertyName("cleared")] bool Cleared,
        [property: JsonPropertyName("bindingErrorCount")] int BindingErrorCount,
        [property: JsonPropertyName("bindingErrors")] BindingDiagnosticEntry[] BindingErrors,
        [property: JsonPropertyName("serilogEventCount")] int SerilogEventCount,
        [property: JsonPropertyName("serilogEvents")] SerilogDiagnosticEntry[] SerilogEvents);
}
#endif
