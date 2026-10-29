using System;
using Serilog.Core;
using Serilog.Events;

namespace KamatekCrm.Diagnostics.Core
{
    /// <summary>
    /// Log seviyesini çalışma anında değiştirmeyi sağlar.
    /// Serilog'un <see cref="LoggingLevelSwitch"/> tipini sarmalar; ek paket gerektirmez.
    /// </summary>
    public sealed class DiagnosticLogLevelSwitch
    {
        private readonly LoggingLevelSwitch _levelSwitch;

        public DiagnosticLogLevelSwitch(string? levelName = null)
        {
            _levelSwitch = new LoggingLevelSwitch(Parse(levelName));
        }

        public LoggingLevelSwitch SerilogSwitch => _levelSwitch;

        public string CurrentLevelName => _levelSwitch.MinimumLevel.ToString();

        public DiagnosticLevel CurrentLevel => SerilogSinkAdapter.ToDiagnosticLevel(_levelSwitch.MinimumLevel);

        /// <summary>Seviyeyi değiştirir. Geçersiz ad verilirse Information'a döner.</summary>
        public void SetLevel(string? levelName) => _levelSwitch.MinimumLevel = Parse(levelName);

        /// <summary>
        /// Serilog seviye adlarını ve yaygın kısaltmaları kabul eder.
        /// Bilinmeyen değer için güvenli varsayılan: Information.
        /// </summary>
        public static LogEventLevel Parse(string? levelName)
        {
            if (string.IsNullOrWhiteSpace(levelName))
            {
                return LogEventLevel.Information;
            }

            var normalized = levelName.Trim();

            if (Enum.TryParse<LogEventLevel>(normalized, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            return normalized.ToLowerInvariant() switch
            {
                "info" => LogEventLevel.Information,
                "warn" => LogEventLevel.Warning,
                "err" => LogEventLevel.Error,
                "critical" or "crit" => LogEventLevel.Fatal,
                "trace" or "verbose" => LogEventLevel.Verbose,
                _ => LogEventLevel.Information
            };
        }
    }
}
