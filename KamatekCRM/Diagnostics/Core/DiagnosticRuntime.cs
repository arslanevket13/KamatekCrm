using System;
using KamatekCrm.Configuration;

namespace KamatekCrm.Diagnostics.Core
{
    /// <summary>
    /// Loglama yapılandırıldığında oluşturulan tanılama bileşenlerinin süreç genelindeki
    /// erişim noktası.
    /// <para>
    /// Neden statik: <c>Log.Logger</c> DI kurulmadan ÖNCE (App.OnStartup başında) kuruluyor;
    /// aynı tamponun hem Serilog sink'i hem de DI üzerinden ViewModel'ler tarafından
    /// kullanılması gerekiyor. Bu sınıf yalnızca referans taşır, iş mantığı içermez.
    /// </para>
    /// </summary>
    public static class DiagnosticRuntime
    {
        private static readonly object Gate = new();

        private static DiagnosticOptions _options = new DiagnosticOptions();
        private static InMemoryDiagnosticSink? _store;
        private static DiagnosticLogLevelSwitch? _levelSwitch;

        public static DiagnosticOptions Options
        {
            get
            {
                lock (Gate)
                {
                    return _options;
                }
            }
        }

        public static InMemoryDiagnosticSink? Store => _store;

        public static DiagnosticLogLevelSwitch? LevelSwitch => _levelSwitch;

        public static bool IsInitialized => _store != null;

        /// <summary>
        /// Tamponu ve seviye anahtarını oluşturur. Birden fazla çağrıda aynı örneği korur,
        /// böylece loglama iki kez yapılandırılsa bile tampon sıfırlanmaz.
        /// Tanılama kapalıysa tampon oluşturulmaz ve null döner.
        /// </summary>
        public static InMemoryDiagnosticSink? Initialize(DiagnosticOptions options, DiagnosticLogLevelSwitch levelSwitch)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(levelSwitch);

            lock (Gate)
            {
                _options = options.Normalize();
                _levelSwitch = levelSwitch;

                if (!_options.Enabled)
                {
                    _store = null;
                    return null;
                }

                _store ??= new InMemoryDiagnosticSink(_options.BufferCapacity);
                return _store;
            }
        }

        /// <summary>Testlerde süreç geneli durumu temizlemek için.</summary>
        public static void Reset()
        {
            lock (Gate)
            {
                _store = null;
                _levelSwitch = null;
                _options = new DiagnosticOptions();
            }
        }
    }
}
