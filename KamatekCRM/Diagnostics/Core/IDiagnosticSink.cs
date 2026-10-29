using System;
using System.Collections.Generic;

namespace KamatekCrm.Diagnostics.Core
{
    /// <summary>Tanılama olaylarını tüketen hedef (bellek içi tampon, dosya, dışa aktarma...).</summary>
    public interface IDiagnosticSink
    {
        void Publish(DiagnosticEvent diagnosticEvent);
    }

    /// <summary>Tanılama olaylarını sorgulanabilir biçimde tutan depolar.</summary>
    public interface IDiagnosticStore
    {
        int Capacity { get; }

        int Count { get; }

        /// <summary>Tampon dolduğu için düşürülen olay sayısı.</summary>
        long DroppedCount { get; }

        IReadOnlyList<DiagnosticEvent> GetSnapshot(int limit = 500);

        IReadOnlyList<DiagnosticEvent> Query(DiagnosticQuery query);

        void Clear();
    }

    /// <summary>Canlı log ekranının filtre/sayfalama isteği.</summary>
    public sealed record DiagnosticQuery
    {
        public DiagnosticLevel MinimumLevel { get; init; } = DiagnosticLevel.Verbose;

        /// <summary>Tam eşleşme (büyük/küçük harf duyarsız). Null ise tüm modüller.</summary>
        public string? Module { get; init; }

        /// <summary>Mesaj/modül/kod içinde arama.</summary>
        public string? SearchText { get; init; }

        public DateTimeOffset? From { get; init; }

        public DateTimeOffset? To { get; init; }

        public string? CorrelationId { get; init; }

        public int Skip { get; init; }

        public int Take { get; init; } = 200;
    }
}
