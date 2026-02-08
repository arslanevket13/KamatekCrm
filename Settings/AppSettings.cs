using System;
using System.IO;
using Microsoft.Extensions.Configuration;

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
                            var builder = new ConfigurationBuilder()
                                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

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
        /// SQL Server bağlantı dizesi
        /// </summary>
        public static string SqlServerConnectionString => 
            Configuration["DatabaseSettings:ConnectionStrings:SqlServer"] ?? "";

        /// <summary>
        /// Aktif bağlantı dizesi (DatabaseType'a göre)
        /// </summary>
        public static string ActiveConnectionString => 
            DatabaseType.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) 
                ? SqlServerConnectionString 
                : SqliteConnectionString;

        /// <summary>
        /// SQL Server kullanılıyor mu?
        /// </summary>
        public static bool UseSqlServer => 
            DatabaseType.Equals("SqlServer", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Şirket adı
        /// </summary>
        public static string CompanyName => 
            Configuration["AppSettings:CompanyName"] ?? "Kamatek";

        /// <summary>
        /// Uygulama sürümü
        /// </summary>
        public static string Version => 
            Configuration["AppSettings:Version"] ?? "1.0.0";
    }
}
