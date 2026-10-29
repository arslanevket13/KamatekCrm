using System;
using System.Threading;

namespace KamatekCrm.Diagnostics.Core
{
    /// <summary>
    /// Bir işlem (kullanıcı eylemi, arka plan işi, API çağrısı) boyunca taşınan
    /// korelasyon kimliğini yönetir.
    /// <para>
    /// <see cref="AsyncLocal{T}"/> kullanıldığı için kimlik, <c>await</c> sonrasında da
    /// aynı mantıksal akışta korunur; farklı akışlara sızmaz.
    /// </para>
    /// </summary>
    public static class CorrelationContext
    {
        private static readonly AsyncLocal<Holder?> CurrentHolder = new();

        /// <summary>Aktif korelasyon kimliği; yoksa null.</summary>
        public static string? Current => CurrentHolder.Value?.Value;

        /// <summary>Yeni bir korelasyon kimliği üretir (örn. CRL-20261029-1a2b3c4d).</summary>
        public static string NewId(string prefix = "CRL")
        {
            var safePrefix = string.IsNullOrWhiteSpace(prefix) ? "CRL" : prefix.Trim();
            return $"{safePrefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..(safePrefix.Length + 14)];
        }

        /// <summary>
        /// Verilen kimliği (veya yenisini) aktif hale getirir ve kapsam bitince
        /// önceki değeri geri yükler.
        /// </summary>
        public static IDisposable BeginScope(string? correlationId = null)
        {
            var previous = CurrentHolder.Value;
            CurrentHolder.Value = new Holder(string.IsNullOrWhiteSpace(correlationId) ? NewId() : correlationId!);
            return new Scope(previous);
        }

        /// <summary>Kimliği kapsam olmadan ayarlar (uygulama oturumu gibi uzun ömürlü kullanım).</summary>
        public static void Set(string? correlationId)
        {
            CurrentHolder.Value = string.IsNullOrWhiteSpace(correlationId) ? null : new Holder(correlationId!);
        }

        public static void Clear() => CurrentHolder.Value = null;

        private sealed class Holder
        {
            public Holder(string value) => Value = value;

            public string Value { get; }
        }

        private sealed class Scope : IDisposable
        {
            private readonly Holder? _previous;
            private bool _disposed;

            public Scope(Holder? previous) => _previous = previous;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                CurrentHolder.Value = _previous;
            }
        }
    }
}
