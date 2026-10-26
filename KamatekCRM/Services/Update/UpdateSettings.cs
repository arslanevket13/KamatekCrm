using System;

namespace KamatekCrm.Services.Update
{
    /// <summary>
    /// Kullanıcı makinesinde %LOCALAPPDATA%\KamatekCRM\update_settings.json konumunda
    /// saklanan güncelleme yapılandırması. Uygulama yükseltmelerinde asla silinmez.
    /// </summary>
    public class UpdateSettings
    {
        /// <summary>
        /// Uygulama açılışında otomatik güncelleme kontrolü yapılsın mı?
        /// </summary>
        public bool CheckForUpdatesOnStartup { get; set; } = true;

        /// <summary>
        /// Yeni sürüm bulunduğunda kullanıcıya sormadan arka planda indirilsin mi?
        /// </summary>
        public bool AutoDownloadUpdates { get; set; } = false;

        /// <summary>
        /// Güncelleme kanalı: "Stable" (Kararlı) veya "Beta" (Ön izleme)
        /// </summary>
        public string UpdateChannel { get; set; } = "Stable";

        /// <summary>
        /// İndirilen güncelleme program kapatılırken otomatik yüklensin mi?
        /// </summary>
        public bool InstallOnClose { get; set; } = false;

        /// <summary>
        /// Son güncelleme kontrol zamanı
        /// </summary>
        public DateTime? LastCheckTime { get; set; }

        /// <summary>
        /// Son indirilen ve yüklenmeye hazır sürüm (örn: "v2.1.0")
        /// </summary>
        public string? LastDownloadedVersion { get; set; }

        /// <summary>
        /// Özel güncelleme kaynağı URL'si veya güvenli ağ yolu.
        /// Yalnızca https:// veya güvenli yerel/UNC dosya yolu kabul edilir. http:// kesinlikle reddedilir.
        /// </summary>
        public string? CustomUpdateUrl { get; set; }

        /// <summary>
        /// Güncelleme kaynağının güvenliğini doğrular.
        /// </summary>
        public static bool IsValidUpdateSource(string? source)
        {
            if (string.IsNullOrWhiteSpace(source)) return false;

            source = source.Trim();
            if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return false; // Güvensiz HTTP protokolü engellendi
            }

            if (source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
            }

            // Yerel veya güvenli UNC ağ paylaşım yolu
            return System.IO.Path.IsPathRooted(source) || source.StartsWith(@"\\", StringComparison.Ordinal);
        }
    }
}
