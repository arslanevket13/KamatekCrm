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
        public static string PostgreSqlConnectionString
        {
            get
            {
                var localConn = GetLocalRuntimeSettings().EncryptedConnectionString;
                if (!string.IsNullOrWhiteSpace(localConn))
                {
                    return Security.DataProtectionHelper.UnprotectConnectionString(localConn);
                }

                return Security.DataProtectionHelper.UnprotectConnectionString(
                    Configuration["DatabaseSettings:ConnectionStrings:PostgreSQL"] ?? "");
            }
        }

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
                catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "AppSettings.cs"); }

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
                catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "AppSettings.cs"); }
            }
        }

        #region Managed Local Postgres, First-Run, Auto-Login & Recovery Key

        private static LocalRuntimeSettingsModel? _cachedLocalRuntime;
        private static readonly object _localRuntimeLock = new();

        public static bool IsManagedLocalPostgres
        {
            get => GetLocalRuntimeSettings().IsManagedLocalPostgres;
            set => UpdateLocalRuntimeSettings(s => s.IsManagedLocalPostgres = value);
        }

        public static int ManagedPostgresPort
        {
            get => GetLocalRuntimeSettings().ManagedPostgresPort;
            set => UpdateLocalRuntimeSettings(s => s.ManagedPostgresPort = value);
        }

        public static bool AllowLanAccess
        {
            get => GetLocalRuntimeSettings().AllowLanAccess;
            set => UpdateLocalRuntimeSettings(s => s.AllowLanAccess = value);
        }

        public static bool IsFirstRunCompleted
        {
            get => GetLocalRuntimeSettings().IsFirstRunCompleted;
            set => UpdateLocalRuntimeSettings(s => s.IsFirstRunCompleted = value);
        }

        public static bool AutoLoginEnabled
        {
            get => GetLocalRuntimeSettings().AutoLoginEnabled;
            set => UpdateLocalRuntimeSettings(s => s.AutoLoginEnabled = value);
        }

        public static string? AutoLoginEncryptedToken
        {
            get => GetLocalRuntimeSettings().AutoLoginEncryptedToken;
            set => UpdateLocalRuntimeSettings(s => s.AutoLoginEncryptedToken = value);
        }

        public static string? RecoveryKeyHash
        {
            get => GetLocalRuntimeSettings().RecoveryKeyHash;
            set => UpdateLocalRuntimeSettings(s => s.RecoveryKeyHash = value);
        }

        public static void SaveManagedPostgresState(string plainConnectionString, int port, bool allowLanAccess)
        {
            var protectedConn = Security.DataProtectionHelper.ProtectConnectionStringPassword(plainConnectionString);
            UpdateLocalRuntimeSettings(s =>
            {
                s.IsManagedLocalPostgres = true;
                s.ManagedPostgresPort = port;
                s.AllowLanAccess = allowLanAccess;
                s.EncryptedConnectionString = protectedConn;
            });
            TrySyncAppSettingsConnectionString(protectedConn);
        }

        public static void SaveAutoLoginSession(int userId, string username)
        {
            var payload = $"{userId}|{username}";
            var encrypted = Security.DataProtectionHelper.Protect(payload);
            UpdateLocalRuntimeSettings(s =>
            {
                s.AutoLoginEnabled = true;
                s.AutoLoginEncryptedToken = encrypted;
            });
        }

        public static bool TryReadAutoLoginSession(out int userId, out string username)
        {
            userId = 0;
            username = string.Empty;

            var state = GetLocalRuntimeSettings();
            if (!state.AutoLoginEnabled || string.IsNullOrWhiteSpace(state.AutoLoginEncryptedToken))
                return false;

            var plain = Security.DataProtectionHelper.Unprotect(state.AutoLoginEncryptedToken);
            var sep = plain.IndexOf('|');
            if (sep <= 0 || sep >= plain.Length - 1)
                return false;

            if (!int.TryParse(plain[..sep], out userId))
                return false;

            username = plain[(sep + 1)..];
            return !string.IsNullOrWhiteSpace(username);
        }

        public static void ClearAutoLoginSession()
        {
            UpdateLocalRuntimeSettings(s =>
            {
                s.AutoLoginEnabled = false;
                s.AutoLoginEncryptedToken = null;
            });
        }

        public static string LocalRuntimeSettingsFilePath => GetLocalRuntimeSettingsPath();

        private static string GetLocalRuntimeSettingsPath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KamatekCRM");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "local_runtime_settings.json");
        }

        private static LocalRuntimeSettingsModel GetLocalRuntimeSettings()
        {
            if (_cachedLocalRuntime is not null) return _cachedLocalRuntime;

            lock (_localRuntimeLock)
            {
                if (_cachedLocalRuntime is not null) return _cachedLocalRuntime;

                try
                {
                    var path = GetLocalRuntimeSettingsPath();
                    if (File.Exists(path))
                    {
                        var json = File.ReadAllText(path);
                        var loaded = System.Text.Json.JsonSerializer.Deserialize<LocalRuntimeSettingsModel>(json);
                        if (loaded is not null)
                        {
                            _cachedLocalRuntime = loaded;
                            return _cachedLocalRuntime;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "local_runtime_settings.json okunamadı; varsayılan değerler kullanılıyor.");
                }

                _cachedLocalRuntime = new LocalRuntimeSettingsModel();
                return _cachedLocalRuntime;
            }
        }

        private static void UpdateLocalRuntimeSettings(Action<LocalRuntimeSettingsModel> mutator)
        {
            lock (_localRuntimeLock)
            {
                var current = GetLocalRuntimeSettings();
                mutator(current);
                try
                {
                    var path = GetLocalRuntimeSettingsPath();
                    var json = System.Text.Json.JsonSerializer.Serialize(
                        current,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(path, json);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "local_runtime_settings.json kaydedilemedi.");
                }
            }
        }

        private static void TrySyncAppSettingsConnectionString(string protectedConnectionString)
        {
            try
            {
                var appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!File.Exists(appSettingsPath)) return;

                var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(appSettingsPath))?.AsObject();
                if (root is null) return;

                var dbSettings = root["DatabaseSettings"]?.AsObject();
                if (dbSettings is null)
                {
                    dbSettings = new System.Text.Json.Nodes.JsonObject();
                    root["DatabaseSettings"] = dbSettings;
                }

                var connStrings = dbSettings["ConnectionStrings"]?.AsObject();
                if (connStrings is null)
                {
                    connStrings = new System.Text.Json.Nodes.JsonObject();
                    dbSettings["ConnectionStrings"] = connStrings;
                }

                connStrings["PostgreSQL"] = protectedConnectionString;
                File.WriteAllText(
                    appSettingsPath,
                    root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "appsettings.json bağlantı dizesi senkronizasyonu atlandı.");
            }
        }

        private sealed class LocalRuntimeSettingsModel
        {
            public bool IsManagedLocalPostgres { get; set; }
            public int ManagedPostgresPort { get; set; } = 5439;
            public bool AllowLanAccess { get; set; }
            public bool IsFirstRunCompleted { get; set; }
            public bool AutoLoginEnabled { get; set; }
            public string? AutoLoginEncryptedToken { get; set; }
            public string? RecoveryKeyHash { get; set; }
            public string? EncryptedConnectionString { get; set; }
        }

        #endregion
    }
}
