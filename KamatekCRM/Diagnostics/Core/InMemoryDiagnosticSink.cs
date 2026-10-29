using System;
using System.Collections.Generic;
using System.Threading;

namespace KamatekCrm.Diagnostics.Core
{
    /// <summary>
    /// Sabit kapasiteli halka tampon (ring buffer).
    /// <para>
    /// Yazma O(1)'dir ve UI thread'ini bloklamaz; kapasite dolduğunda en eski kayıt
    /// düşürülür ve <see cref="DroppedCount"/> artırılır. Böylece uzun süre açık kalan
    /// bir uygulamada bellek sınırsız büyümez.
    /// </para>
    /// </summary>
    public sealed class InMemoryDiagnosticSink : IDiagnosticSink, IDiagnosticStore
    {
        public const int MinimumCapacity = 100;
        public const int MaximumCapacity = 100_000;

        private readonly DiagnosticEvent[] _buffer;
        private readonly object _gate = new();

        private int _start;
        private int _count;
        private long _droppedCount;
        private long _sequence;

        public InMemoryDiagnosticSink(int capacity = 5_000)
        {
            var safeCapacity = Math.Clamp(capacity, MinimumCapacity, MaximumCapacity);
            _buffer = new DiagnosticEvent[safeCapacity];
        }

        public int Capacity => _buffer.Length;

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _count;
                }
            }
        }

        public long DroppedCount => Interlocked.Read(ref _droppedCount);

        public void Publish(DiagnosticEvent diagnosticEvent)
        {
            ArgumentNullException.ThrowIfNull(diagnosticEvent);

            var sequenced = diagnosticEvent with { Sequence = Interlocked.Increment(ref _sequence) };

            lock (_gate)
            {
                if (_count == _buffer.Length)
                {
                    _buffer[_start] = sequenced;
                    _start = (_start + 1) % _buffer.Length;
                    Interlocked.Increment(ref _droppedCount);
                    return;
                }

                var index = (_start + _count) % _buffer.Length;
                _buffer[index] = sequenced;
                _count++;
            }
        }

        /// <summary>En yeniden en eskiye doğru anlık görüntü.</summary>
        public IReadOnlyList<DiagnosticEvent> GetSnapshot(int limit = 500)
        {
            if (limit <= 0)
            {
                return Array.Empty<DiagnosticEvent>();
            }

            lock (_gate)
            {
                var take = Math.Min(limit, _count);
                var result = new DiagnosticEvent[take];
                for (var i = 0; i < take; i++)
                {
                    var index = (_start + _count - 1 - i) % _buffer.Length;
                    result[i] = _buffer[index];
                }

                return result;
            }
        }

        public IReadOnlyList<DiagnosticEvent> Query(DiagnosticQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var take = query.Take <= 0 ? 200 : Math.Min(query.Take, MaximumCapacity);
            var skip = Math.Max(0, query.Skip);
            var search = string.IsNullOrWhiteSpace(query.SearchText) ? null : query.SearchText.Trim();
            var module = string.IsNullOrWhiteSpace(query.Module) ? null : query.Module.Trim();
            var correlationId = string.IsNullOrWhiteSpace(query.CorrelationId) ? null : query.CorrelationId.Trim();

            lock (_gate)
            {
                var result = new List<DiagnosticEvent>(Math.Min(take, _count));
                var matched = 0;

                for (var i = 0; i < _count; i++)
                {
                    var index = (_start + _count - 1 - i) % _buffer.Length;
                    var candidate = _buffer[index];

                    if (candidate.Level < query.MinimumLevel) continue;
                    if (query.From.HasValue && candidate.Timestamp < query.From.Value) continue;
                    if (query.To.HasValue && candidate.Timestamp > query.To.Value) continue;

                    if (module != null &&
                        !string.Equals(candidate.Module, module, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (correlationId != null &&
                        !string.Equals(candidate.CorrelationId, correlationId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (search != null && !MatchesSearch(candidate, search))
                    {
                        continue;
                    }

                    if (matched++ < skip)
                    {
                        continue;
                    }

                    result.Add(candidate);
                    if (result.Count >= take)
                    {
                        break;
                    }
                }

                return result;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _start = 0;
                _count = 0;
            }
        }

        private static bool MatchesSearch(DiagnosticEvent candidate, string search)
        {
            return candidate.Message.Contains(search, StringComparison.OrdinalIgnoreCase)
                || candidate.Module.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (candidate.EventCode?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (candidate.ExceptionType?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (candidate.CorrelationId?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
        }
    }

    /// <summary>Tanılama kapalıyken kullanılan, hiçbir şey tutmayan güvenli yedek.</summary>
    public sealed class NullDiagnosticSink : IDiagnosticSink, IDiagnosticStore
    {
        public static readonly NullDiagnosticSink Instance = new();

        private NullDiagnosticSink()
        {
        }

        public int Capacity => 0;

        public int Count => 0;

        public long DroppedCount => 0;

        public void Publish(DiagnosticEvent diagnosticEvent)
        {
            // Bilinçli olarak boş: tanılama devre dışı.
        }

        public IReadOnlyList<DiagnosticEvent> GetSnapshot(int limit = 500) => Array.Empty<DiagnosticEvent>();

        public IReadOnlyList<DiagnosticEvent> Query(DiagnosticQuery query) => Array.Empty<DiagnosticEvent>();

        public void Clear()
        {
        }
    }
}
