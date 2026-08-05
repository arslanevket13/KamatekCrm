#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace KamatekCrm.Diagnostics
{
    /// <summary>
    /// WPF bağlama hatalarını geliştirme sırasında görünür kılar.
    /// <list type="bullet">
    /// <item>Tüm bağlama hataları (Error/Warning) Debug çıktısına yazılır — Visual Studio
    /// "Output → Debug" penceresinde anında görünür.</item>
    /// <item>"TwoWay veya OneWayToSource" sınıfındaki hatalar (salt okunur özelliğe çift
    /// yönlü bağlama — uygulama çöktüren sınıf) ayrıca metin bazında tek seferlik
    /// MessageBox ile yüzeye çıkarılır.</item>
    /// </list>
    /// Yalnızca DEBUG derlemelerinde derlenir; üretim sürümünde hiçbir etkisi yoktur.
    /// </summary>
    public sealed class BindingErrorTraceListener : TraceListener
    {
        private const string TwoWayReadOnlyMarker = "TwoWay or OneWayToSource";

        /// <summary>Aynı hata metninin tekrar tekrar popup açmasını engeller.</summary>
        private readonly HashSet<string> _surfaced = new(StringComparer.Ordinal);

        public override void Write(string? message) => Forward(message);

        public override void WriteLine(string? message) => Forward(message);

        private void Forward(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            // Tüm bağlama hataları geliştirme çıktısına gider (VS Output → Debug).
            Debug.WriteLine($"[WPF Binding] {message}");

            // Çökme sınıfı kullanıcıya görünür olmalı.
            if (!message.Contains(TwoWayReadOnlyMarker, StringComparison.Ordinal))
            {
                return;
            }

            if (!_surfaced.Add(message))
            {
                return;
            }

            // Bağlama motoru aktivasyonun ortasındayken modal MessageBox açmak yeniden
            // giriş (reentrancy) sorunlarına yol açabilir; bu yüzden Dispatcher kuyruğuna
            // ertelenir.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
            {
                return;
            }

            dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    MessageBox.Show(
                        "WPF bağlama hatası (salt okunur özelliğe TwoWay/OneWayToSource bağlama):\n\n" +
                        message +
                        "\n\nÇözüm: Bağlamaya Mode=OneWay ekleyin.",
                        "Bağlama Hatası — Geliştirici Modu",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                catch
                {
                    // Mesaj kutusu gösterilemezse sessizce geç — Debug çıktısı zaten yazıldı.
                }
            }));
        }
    }
}
#endif
