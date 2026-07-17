using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using KamatekCrm.Services;
using KamatekCrm.Settings;
using Serilog;

namespace KamatekCrm.Services
{
    /// <summary>
    /// PostgreSQL yedekleme ve geri yükleme servisi.
    /// pg_dump.exe ve pg_restore.exe araçlarını uygulama kök dizinindeki
    /// "PostgresTools" klasöründen çözümler. Sistem PATH bağımlılığı YOKTUR.
    /// </summary>
    public class BackupService : IBackupService
    {
        /// <summary>
        /// Uygulama kök dizinine göre PostgreSQL araçlarının bulunduğu alt klasör.
        /// Dağıtım sırasında bu klasör uygulama ile birlikte paketlenmelidir.
        /// </summary>
        private static readonly string PostgresToolsDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PostgresTools");

        private static readonly string PgDumpPath =
            Path.Combine(PostgresToolsDir, "pg_dump.exe");

        private static readonly string PgRestorePath =
            Path.Combine(PostgresToolsDir, "pg_restore.exe");

        private const string ToolsMissingMessage =
            "Yedekleme araçları bulunamadı. Lütfen uygulama klasöründe 'PostgresTools' dizininin " +
            "(pg_dump.exe ve pg_restore.exe ile birlikte) mevcut olduğundan emin olun.";

        #region Public API

        /// <summary>
        /// Veritabanının tam yedeğini alır ve .sql dosyasının yolunu döner.
        /// </summary>
        /// <returns>Oluşturulan yedek dosyasının tam yolu.</returns>
        public string BackupDatabase()
        {
            try
            {
                // ── 1. Araç varlık kontrolü (Fast-Fail) ──
                if (!File.Exists(PgDumpPath))
                {
                    Log.Error("Backup failed: pg_dump.exe not found at {PgDumpPath}.", PgDumpPath);
                    throw new FileNotFoundException(ToolsMissingMessage, PgDumpPath);
                }

                // ── 2. Bağlantı dizesini al ve parse et ──
                var connectionString = AppSettings.PostgreSqlConnectionString;
                if (string.IsNullOrEmpty(connectionString))
                {
                    Log.Error("Backup failed: Connection string is empty.");
                    throw new InvalidOperationException("PostgreSQL bağlantı dizesi bulunamadı.");
                }

                var dbInfo = ParseConnectionString(connectionString);

                // ── 3. Yedekleme klasörünü hazırla ──
                string backupFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "KamatekCRM", "Backups");

                Directory.CreateDirectory(backupFolder); // Zaten varsa sessizce geçer

                // ── 4. Dosya adını oluştur ──
                string fileName = $"KamatekBackup_{DateTime.Now:yyyyMMdd_HHmm}.sql";
                string backupPath = Path.Combine(backupFolder, fileName);

                Log.Information("Starting database backup to: {BackupPath} using tool: {PgDumpPath}",
                    backupPath, PgDumpPath);

                // ── 5. pg_dump işlemini başlat ──
                var psi = new ProcessStartInfo
                {
                    FileName = PgDumpPath,
                    Arguments = $"-h {dbInfo.Host} -p {dbInfo.Port} -U {dbInfo.User} " +
                                $"-d {dbInfo.Database} -F p -f \"{backupPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Şifreyi çevre değişkeni olarak ver (Güvenli yöntem — komut satırında görünmez)
                psi.EnvironmentVariables["PGPASSWORD"] = dbInfo.Password;

                using var process = Process.Start(psi);
                if (process == null)
                    throw new InvalidOperationException("pg_dump process başlatılamadı.");

                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Log.Error("pg_dump failed with exit code {ExitCode}. Error: {Error}",
                        process.ExitCode, error);
                    throw new InvalidOperationException(
                        $"Yedekleme işlemi başarısız oldu (Exit Code: {process.ExitCode}). Hata: {error}");
                }

                Log.Information("Database backup completed successfully: {BackupPath}", backupPath);
                return backupPath;
            }
            catch (Exception ex) when (ex is not FileNotFoundException and not InvalidOperationException)
            {
                Log.Error(ex, "Backup process encountered an unexpected error.");
                throw;
            }
        }

        /// <summary>
        /// Belirtilen .sql yedek dosyasından veritabanını geri yükler.
        /// </summary>
        /// <param name="backupPath">Geri yüklenecek .sql dosyasının tam yolu.</param>
        public void RestoreDatabase(string backupPath)
        {
            try
            {
                // ── 1. Araç varlık kontrolü (Fast-Fail) ──
                if (!File.Exists(PgRestorePath))
                {
                    Log.Error("Restore failed: pg_restore.exe not found at {PgRestorePath}.", PgRestorePath);
                    throw new FileNotFoundException(ToolsMissingMessage, PgRestorePath);
                }

                // ── 2. Yedek dosya varlık kontrolü ──
                if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
                {
                    Log.Error("Restore failed: Backup file not found at {BackupPath}.", backupPath);
                    throw new FileNotFoundException(
                        $"Yedek dosyası bulunamadı: {backupPath}", backupPath);
                }

                // ── 3. Bağlantı dizesini al ve parse et ──
                var connectionString = AppSettings.PostgreSqlConnectionString;
                if (string.IsNullOrEmpty(connectionString))
                {
                    Log.Error("Restore failed: Connection string is empty.");
                    throw new InvalidOperationException("PostgreSQL bağlantı dizesi bulunamadı.");
                }

                var dbInfo = ParseConnectionString(connectionString);

                Log.Information("Starting database restore from: {BackupPath} using tool: {PgRestorePath}",
                    backupPath, PgRestorePath);

                // ── 4. pg_restore işlemini başlat ──
                var psi = new ProcessStartInfo
                {
                    FileName = PgRestorePath,
                    Arguments = $"-h {dbInfo.Host} -p {dbInfo.Port} -U {dbInfo.User} " +
                                $"-d {dbInfo.Database} --clean --if-exists \"{backupPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                psi.EnvironmentVariables["PGPASSWORD"] = dbInfo.Password;

                using var process = Process.Start(psi);
                if (process == null)
                    throw new InvalidOperationException("pg_restore process başlatılamadı.");

                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Log.Error("pg_restore failed with exit code {ExitCode}. Error: {Error}",
                        process.ExitCode, error);
                    throw new InvalidOperationException(
                        $"Geri yükleme işlemi başarısız oldu (Exit Code: {process.ExitCode}). Hata: {error}");
                }

                Log.Information("Database restore completed successfully from: {BackupPath}", backupPath);
            }
            catch (Exception ex) when (ex is not FileNotFoundException and not InvalidOperationException)
            {
                Log.Error(ex, "Restore process encountered an unexpected error.");
                throw;
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Npgsql connection string'ini bileşenlerine ayırır.
        /// </summary>
        private static (string Host, string Port, string Database, string User, string Password)
            ParseConnectionString(string connectionString)
        {
            var host = Regex.Match(connectionString, @"Host=([^;]+)", RegexOptions.IgnoreCase).Groups[1].Value;
            var port = Regex.Match(connectionString, @"Port=([^;]+)", RegexOptions.IgnoreCase).Groups[1].Value;
            var database = Regex.Match(connectionString, @"Database=([^;]+)", RegexOptions.IgnoreCase).Groups[1].Value;
            var user = Regex.Match(connectionString, @"User(?:name)?=([^;]+)", RegexOptions.IgnoreCase).Groups[1].Value;
            var password = Regex.Match(connectionString, @"Password=([^;]+)", RegexOptions.IgnoreCase).Groups[1].Value;

            if (string.IsNullOrEmpty(host)) host = "localhost";
            if (string.IsNullOrEmpty(port)) port = "5432";

            return (host, port, database, user, password);
        }

        #endregion
    }
}

