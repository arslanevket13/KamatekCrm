using System;
using System.IO;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace KamatekCrm.Diagnostics.Core
{
    /// <summary>
    /// Dosya ve konsol çıktısını yazmadan önce <see cref="LogRedactor"/> ile temizleyen biçimlendirici.
    /// <para>
    /// Neden gerekli: redaksiyon yalnızca bellek içi tamponda uygulanırsa, diske yazılan
    /// <c>kamatek-*.log</c> dosyaları parola/token içermeye devam eder. Bu sınıf, mevcut
    /// çıktı şablonunu koruyarak metni tek noktada maskeler.
    /// </para>
    /// </summary>
    public sealed class RedactingTextFormatter : ITextFormatter
    {
        private readonly MessageTemplateTextFormatter _inner;

        public RedactingTextFormatter(string outputTemplate)
        {
            if (string.IsNullOrWhiteSpace(outputTemplate))
            {
                throw new ArgumentException("Çıktı şablonu boş olamaz.", nameof(outputTemplate));
            }

            _inner = new MessageTemplateTextFormatter(outputTemplate);
        }

        public void Format(LogEvent logEvent, TextWriter output)
        {
            ArgumentNullException.ThrowIfNull(logEvent);
            ArgumentNullException.ThrowIfNull(output);

            try
            {
                using var buffer = new StringWriter();
                _inner.Format(logEvent, buffer);
                output.Write(LogRedactor.Redact(buffer.ToString()));
            }
            catch
            {
                // Biçimlendirme hatası log yazımını durdurmamalı; en azından olay kaybolmasın.
                try
                {
                    output.Write("[log format hatasi]");
                }
                catch
                {
                    // Çıktı hedefi de kullanılamıyorsa yapacak bir şey yok.
                }
            }
        }
    }
}
