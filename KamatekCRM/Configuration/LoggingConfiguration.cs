using System;
using System.IO;
using KamatekCrm.Diagnostics.Core;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace KamatekCrm.Configuration
{
    /// <summary>
    /// Uygulamanın tek log yapılandırma noktası.
    /// <para>
    /// Aşama 2 değişiklikleri:
    /// <list type="bullet">
    /// <item>Seviye artık <c>appsettings.json → Diagnostics:MinimumLevel</c> ile belirlenir
    /// ve çalışma anında <see cref="DiagnosticLogLevelSwitch"/> ile değiştirilebilir.</item>
    /// <item>Bellek içi sınırlı tampon (<see cref="InMemoryDiagnosticSink"/>) eklendi.</item>
    /// <item>Tüm olaylara <see cref="CorrelationIdEnricher"/> ile korelasyon kimliği eklenir.</item>
    /// <item>Yapılandırma hatası uygulamayı durdurmaz; varsayılanlarla devam edilir.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class LoggingConfiguration
    {
        private const string FileOutputTemplate =
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}";

        private const string ConsoleOutputTemplate =
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// Loglamayı yapılandırır ve kullanılan seçenekleri döndürür.
        /// </summary>
        /// <param name="options">
        /// Test veya ileride ayar ekranı için dışarıdan verilebilir; null ise appsettings.json okunur.
        /// </param>
        public static DiagnosticOptions ConfigureLogging(DiagnosticOptions? options = null)
        {
            var resolved = (options ?? LoadOptions()).Normalize();
            var logDirectory = DiagnosticPaths.EnsureDirectory(DiagnosticPaths.Logs);
            var levelSwitch = new DiagnosticLogLevelSwitch(resolved.MinimumLevel);

            // Tanılama kapalıysa null döner ve bellek tamponu hiç oluşturulmaz.
            var store = DiagnosticRuntime.Initialize(resolved, levelSwitch);

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch.SerilogSwitch)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithMachineName()
                .Enrich.With(new CorrelationIdEnricher())
                .WriteTo.Console(formatter: new RedactingTextFormatter(ConsoleOutputTemplate))
                .WriteTo.File(
                    formatter: new RedactingTextFormatter(FileOutputTemplate),
                    path: Path.Combine(logDirectory, "kamatek-.log"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: resolved.FileSizeLimitBytes,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: resolved.RetainedFileCountLimit);

            if (store != null)
            {
                loggerConfiguration = loggerConfiguration.WriteTo.Sink(new SerilogSinkAdapter(store));
            }

            Log.Logger = loggerConfiguration.CreateLogger();

            Log.Information(
                "Logging initialized (level: {Level}, buffer: {Capacity}, directory: {Directory})",
                levelSwitch.CurrentLevelName,
                store?.Capacity ?? 0,
                logDirectory);

            return resolved;
        }

        private static DiagnosticOptions LoadOptions()
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .Build();

                return DiagnosticOptions.FromConfiguration(configuration);
            }
            catch
            {
                // appsettings okunamazsa güvenli varsayılanlarla devam et.
                return new DiagnosticOptions().Normalize();
            }
        }
    }
}
