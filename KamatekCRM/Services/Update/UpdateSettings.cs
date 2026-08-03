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
    }
}
