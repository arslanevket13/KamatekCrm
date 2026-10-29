using System;
using System.Collections.Generic;
using System.Text;

namespace KamatekCrm.Diagnostics.Core
{
    /// <summary>
    /// Geliştirici Merkezi'nin standart olay modeli.
    /// Log dosyası, bellek içi tampon ve ileride dışa aktarılacak tanılama paketi
    /// aynı modeli kullanır; böylece filtreleme tek yerde yapılır.
    /// </summary>
    public sealed record DiagnosticEvent
    {
        /// <summary>Artan sıra numarası (tampon içinde sıralama için).</summary>
        public long Sequence { get; init; }

        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

        public DiagnosticLevel Level { get; init; } = DiagnosticLevel.Information;

        /// <summary>Olayın üretildiği modül (genellikle sınıf adı).</summary>
        public string Module { get; init; } = "App";

        /// <summary>Kullanıcıya/log'a yazılan mesaj (redakte edilmiş).</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Hata kodu veya olay kimliği (örn. VAL_001, AUTH_FAILED).</summary>
        public string? EventCode { get; init; }

        /// <summary>İşlem/istek boyunca taşınan korelasyon kimliği.</summary>
        public string? CorrelationId { get; init; }

        public int ThreadId { get; init; }

        public int ProcessId { get; init; }

        /// <summary>Exception'ın tam tip adı; exception yoksa null.</summary>
        public string? ExceptionType { get; init; }

        public string? ExceptionMessage { get; init; }

        /// <summary>Redakte edilmiş tam stack trace.</summary>
        public string? StackTrace { get; init; }

        /// <summary>Ek yapısal alanlar (redakte edilmiş, sınırlı sayıda).</summary>
        public IReadOnlyDictionary<string, string?>? Properties { get; init; }

        public bool IsError => Level >= DiagnosticLevel.Error;

        /// <summary>Liste bağlamaları için tek satırlık gösterim.</summary>
        public string DisplayLine => ToDisplayLine();

        public string LevelText => Level switch
        {
            DiagnosticLevel.Verbose => "VRB",
            DiagnosticLevel.Debug => "DBG",
            DiagnosticLevel.Information => "INF",
            DiagnosticLevel.Warning => "WRN",
            DiagnosticLevel.Error => "ERR",
            DiagnosticLevel.Fatal => "FTL",
            _ => "???"
        };

        /// <summary>Canlı log listesinde tek satırda gösterilecek özet.</summary>
        public string ToDisplayLine()
        {
            var builder = new StringBuilder(160);
            builder.Append(Timestamp.ToString("HH:mm:ss.fff")).Append(" [").Append(LevelText).Append("] ");
            if (!string.IsNullOrWhiteSpace(EventCode))
            {
                builder.Append('(').Append(EventCode).Append(") ");
            }

            builder.Append(Module).Append(": ").Append(Message);
            return builder.ToString();
        }

        public override string ToString() => ToDisplayLine();
    }
}
