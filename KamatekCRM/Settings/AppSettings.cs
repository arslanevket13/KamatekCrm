using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Settings
{
    /// <summary>
    /// Uygulama ayarlarını yöneten singleton sınıf
    /// appsettings.json dosyasından yapılandırma okur
    /// </summary>
    public static class AppSettings
    {
        private static IConfiguration? _configuration;
        private static readonly object _lock = new();

        private static CompanyBrandProfile? _cachedBrandProfile;
        private static readonly object _brandLock = new();

        /// <summary>
        /// Yapılandırma nesnesi (lazy initialization)
        /// </summary>
        public static IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    lock (_lock)
                    {
                        if (_configuration == null)
                        {
                            var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

                            var builder = new ConfigurationBuilder()
                                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                                .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true);

                            _configuration = builder.Build();
                        }
                    }
                }
                return _configuration;
            }
        }

        /// <summary>
        /// Veritabanı türü: "SQLite" veya "SqlServer"
        /// </summary>
        public static string DatabaseType => 
            Configuration["DatabaseSettings:DatabaseType"] ?? "SQLite";

        /// <summary>
        /// SQLite bağlantı dizesi
        /// </summary>
        public static string SqliteConnectionString
        {
            get
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = Path.Combine(appData, "KamatekCRM");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                var dbPath = Path.Combine(folder, "KamatekCrm.db");
                
                // Config'den override edilebilir ama default olarak bu yolu kullanalım
                return Configuration["DatabaseSettings:ConnectionStrings:SQLite"] ?? $"Data Source={dbPath}";
            }
        }

        /// <summary>
        /// PostgreSQL bağlantı dizesi (DPAPI çözümlenmiş)
        /// </summary>
        public static string PostgreSqlConnectionString => 
            Security.DataProtectionHelper.UnprotectConnectionString(
                Configuration["DatabaseSettings:ConnectionStrings:PostgreSQL"] ?? "");

        /// <summary>
        /// Aktif bağlantı dizesi (PostgreSQL enforced)
        /// </summary>
        public static string ActiveConnectionString => PostgreSqlConnectionString;

        /// <summary>
        /// PostgreSQL kullanılıyor mu?
        /// </summary>
        public static bool UsePostgreSql => true;

        /// <summary>
        /// Şirket adı (Aktif marka profilinden, yoksa yapılandırmadan)
        /// </summary>
        public static string CompanyName => 
            GetBrandProfile().CompanyName ?? Configuration["AppSettings:CompanyName"] ?? "Kamatek";

        /// <summary>
        /// Uygulama sürümü
        /// </summary>
        public static string Version => 
            Configuration["AppSettings:Version"] ?? "1.0.0";

        /// <summary>
        /// Kurumsal marka kimliği ve PDF şablon profilini getirir.
        /// Yerel JSON dosyası varsa oradan okur; yoksa varsayılan Kamatek profilini döner.
        /// </summary>
        public static CompanyBrandProfile GetBrandProfile()
        {
            if (_cachedBrandProfile != null) return _cachedBrandProfile;

            lock (_brandLock)
            {
                if (_cachedBrandProfile != null) return _cachedBrandProfile;

                try
                {
                    string filePath = GetCompanySettingsFilePath();
                    if (File.Exists(filePath))
                    {
                        string json = File.ReadAllText(filePath);
                        var profile = System.Text.Json.JsonSerializer.Deserialize<CompanyBrandProfile>(json);
                        if (profile != null)
                        {
                            _cachedBrandProfile = profile;
                            return _cachedBrandProfile;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Brand profile load warning: {ex.Message}");
                }

                _cachedBrandProfile = CompanyBrandProfile.CreateDefaultKamatek();
                return _cachedBrandProfile;
            }
        }

        /// <summary>
        /// Kurumsal marka profilini kaydeder ve bellek içi önbelleği günceller.
        /// </summary>
        public static void SaveBrandProfile(CompanyBrandProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            lock (_brandLock)
            {
                try
                {
                    string filePath = GetCompanySettingsFilePath();
                    string json = System.Text.Json.JsonSerializer.Serialize(profile, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(filePath, json);
                    _cachedBrandProfile = profile;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Brand profile save error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Marka profili bellek önbelleğini temizler.
        /// </summary>
        public static void ClearBrandProfileCache()
        {
            lock (_brandLock)
            {
                _cachedBrandProfile = null;
            }
        }

        public static string GetCompanySettingsFilePath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KamatekCRM");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "company_settings.json");
        }

        /// <summary>
        /// Dinamik tema yönetimi için geçerli temanın kaydedilmesi (PremiumLight, MidnightDark, Glassmorphism)
        /// Crashleri önlemek adına AppData içine kaydedilir.
        /// </summary>
        public static string CurrentTheme
        {
            get
            {
                try
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var folder = Path.Combine(appData, "KamatekCRM");
                    var themeFile = Path.Combine(folder, "theme.txt");
                    if (File.Exists(themeFile))
                    {
                        var savedTheme = File.ReadAllText(themeFile).Trim();
                        if (!string.IsNullOrEmpty(savedTheme))
                            return savedTheme;
                    }
                }
                catch { }

                return Configuration["AppSettings:CurrentTheme"] ?? "PremiumLight";
            }
            set
            {
                try
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var folder = Path.Combine(appData, "KamatekCRM");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    var themeFile = Path.Combine(folder, "theme.txt");
                    File.WriteAllText(themeFile, value);
                }
                catch { }
            }
        }
    }
}
