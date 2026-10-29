using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace KamatekCrm.Configuration
{
    /// <summary>
    /// Tanılama altyapısının yapılandırması.
    /// <para>
    /// Değerler <c>appsettings.json</c> içindeki <c>Diagnostics</c> bölümünden okunur.
    /// Bölüm yoksa güvenli varsayılanlar kullanılır; uygulama bu yüzden başlamamazlık etmez.
    /// </para>
    /// </summary>
    public sealed class DiagnosticOptions
    {
        public const string SectionName = "Diagnostics";

        private const long OneMegabyte = 1024L * 1024L;

        /// <summary>Tanılama toplayıcıları etkin mi? Kapalıyken bellek tamponu oluşturulmaz.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Başlangıç log seviyesi (Verbose/Debug/Information/Warning/Error/Fatal).</summary>
        public string MinimumLevel { get; set; } = "Debug";

        /// <summary>Bellek içi halka tampon kapasitesi (olay sayısı).</summary>
        public int BufferCapacity { get; set; } = 5_000;

        /// <summary>Tek log dosyası üst sınırı.</summary>
        public long FileSizeLimitBytes { get; set; } = 10 * OneMegabyte;

        /// <summary>Saklanacak dosya sayısı.</summary>
        public int RetainedFileCountLimit { get; set; } = 30;

        /// <summary>WPF binding hataları tanılama tamponuna yazılsın mı?</summary>
        public bool CaptureBindingErrors { get; set; } = true;

        /// <summary>UI thread donma eşiği (ms). Aşama 5'te kullanılacak.</summary>
        public int UiFreezeThresholdMs { get; set; } = 500;

        /// <summary>
        /// Geliştirici Merkezi'ne erişebilecek hesaplar (kullanıcı adı).
        /// Liste boşsa erişim <b>kapalıdır</b> (fail-closed): Admin rolü tek başına yetki vermez.
        /// Karşılaştırma büyük/küçük harf duyarsızdır.
        /// </summary>
        public List<string> DeveloperAccounts { get; set; } = new();

        /// <summary>Değerleri güvenli aralıklara çeker ve yeni bir örnek döndürür.</summary>
        public DiagnosticOptions Normalize()
        {
            return new DiagnosticOptions
            {
                Enabled = Enabled,
                MinimumLevel = NormalizeLevel(MinimumLevel),
                BufferCapacity = Math.Clamp(BufferCapacity, 100, 100_000),
                FileSizeLimitBytes = Math.Clamp(FileSizeLimitBytes, OneMegabyte, 100 * OneMegabyte),
                RetainedFileCountLimit = Math.Clamp(RetainedFileCountLimit, 1, 365),
                CaptureBindingErrors = CaptureBindingErrors,
                UiFreezeThresholdMs = Math.Clamp(UiFreezeThresholdMs, 100, 10_000),
                DeveloperAccounts = (DeveloperAccounts ?? new List<string>())
                    .Where(account => !string.IsNullOrWhiteSpace(account))
                    .Select(account => account.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        /// <summary>
        /// <c>IConfiguration</c> üzerinden okur. Binder paketine bağımlı olmamak için
        /// alanlar tek tek okunur.
        /// </summary>
        public static DiagnosticOptions FromConfiguration(IConfiguration? configuration)
        {
            if (configuration == null)
            {
                return new DiagnosticOptions();
            }

            var section = configuration.GetSection(SectionName);
            if (!section.Exists())
            {
                return new DiagnosticOptions();
            }

            return new DiagnosticOptions
            {
                Enabled = ParseBool(section["Enabled"], true),
                MinimumLevel = string.IsNullOrWhiteSpace(section["MinimumLevel"])
                    ? "Debug"
                    : section["MinimumLevel"]!,
                BufferCapacity = ParseInt(section["BufferCapacity"], 5_000),
                FileSizeLimitBytes = ParseLong(section["FileSizeLimitBytes"], 10 * OneMegabyte),
                RetainedFileCountLimit = ParseInt(section["RetainedFileCountLimit"], 30),
                CaptureBindingErrors = ParseBool(section["CaptureBindingErrors"], true),
                UiFreezeThresholdMs = ParseInt(section["UiFreezeThresholdMs"], 500),
                DeveloperAccounts = section.GetSection("DeveloperAccounts")
                    .GetChildren()
                    .Select(child => child.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .ToList()
            }.Normalize();
        }

        private static bool ParseBool(string? raw, bool fallback) =>
            bool.TryParse(raw, out var value) ? value : fallback;

        private static int ParseInt(string? raw, int fallback) =>
            int.TryParse(raw, out var value) ? value : fallback;

        private static long ParseLong(string? raw, long fallback) =>
            long.TryParse(raw, out var value) ? value : fallback;

        private static string NormalizeLevel(string? level)
        {
            var parsed = Diagnostics.Core.DiagnosticLogLevelSwitch.Parse(level);
            return parsed.ToString();
        }
    }
}
