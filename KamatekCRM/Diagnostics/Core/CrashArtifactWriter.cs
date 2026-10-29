using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace KamatekCrm.Diagnostics.Core
{
    /// <summary>
    /// Kritik (sonlandırıcı) hatalarda uygulamanın kapanmadan önce küçük bir JSON iz bırakmasını sağlar.
    /// <para>
    /// Dump dosyası üretmez; yalnızca redakte edilmiş istisna özeti, korelasyon kimliği ve
    /// süreç bilgisi yazar. Her hata durumunda dosya yazımı başarısız olsa bile çağıran akış
    /// etkilenmez (tümü try/catch içinde).
    /// </para>
    /// </summary>
    public static class CrashArtifactWriter
    {
        private const int MaxArtifacts = 50;
        private const int MaxStackTraceLength = 20_000;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>İz dosyasını yazar; başarılıysa tam yolu, değilse null döner.</summary>
        public static string? Write(
            Exception? exception,
            string source,
            bool isTerminating,
            IReadOnlyDictionary<string, string?>? context = null)
        {
            try
            {
                var directory = DiagnosticPaths.EnsureDirectory(DiagnosticPaths.Crashes);
                var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                var suffix = Guid.NewGuid().ToString("N")[..8];
                var path = Path.Combine(directory, $"crash-{stamp}-{suffix}.json");

                var payload = new
                {
                    timestampUtc = DateTimeOffset.UtcNow,
                    source = LogRedactor.RedactAndTruncate(source, 256),
                    isTerminating,
                    processId = Environment.ProcessId,
                    threadId = Environment.CurrentManagedThreadId,
                    machineName = Environment.MachineName,
                    correlationId = CorrelationContext.Current,
                    exceptionType = exception?.GetType().FullName,
                    message = LogRedactor.RedactAndTruncate(exception?.Message, 2_000),
                    stackTrace = LogRedactor.RedactAndTruncate(exception?.ToString(), MaxStackTraceLength),
                    context = LogRedactor.RedactProperties(context)
                };

                File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions));
                PruneOldArtifacts(directory);
                return path;
            }
            catch
            {
                return null;
            }
        }

        private static void PruneOldArtifacts(string directory)
        {
            try
            {
                var files = new DirectoryInfo(directory)
                    .GetFiles("crash-*.json")
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Skip(MaxArtifacts)
                    .ToList();

                foreach (var file in files)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                        // Tek dosya silinemezse diğerleri etkilenmesin.
                    }
                }
            }
            catch
            {
                // Temizlik başarısız olsa da iz dosyası yazılmış olarak kalır.
            }
        }
    }
}
