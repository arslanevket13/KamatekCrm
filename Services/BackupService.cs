using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using KamatekCrm.Services;
using KamatekCrm.Settings;
using Serilog;

namespace KamatekCrm.Services
{
    public class BackupService : IBackupService
    {
        private const string PG_DUMP_EXE = "pg_dump.exe";

        public string BackupDatabase()
        {
            try
            {
                // 1. Bağlantı dizesini al ve parse et
                var connectionString = AppSettings.PostgreSqlConnectionString;
                if (string.IsNullOrEmpty(connectionString))
                {
                    Log.Error("Backup failed: Connection string is empty.");
                    throw new InvalidOperationException("PostgreSQL bağlantı dizesi bulunamadı.");
                }

                var dbInfo = ParseConnectionString(connectionString);

                // 2. pg_dump.exe'yi bul
                string pgDumpPath = FindPgDump();
                if (string.IsNullOrEmpty(pgDumpPath))
                {
                    Log.Error("Backup failed: pg_dump.exe not found.");
                    throw new FileNotFoundException("PostgreSQL yedekleme aracı (pg_dump.exe) bulunamadı. Lütfen PostgreSQL'in kurulu olduğundan ve PATH'e eklendiğinden emin olun.");
                }

                // 3. Yedekleme klasörünü hazırla
                string backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KamatekCRM", "Backups");
                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }

                // 4. Dosya adını oluştur
                string fileName = $"KamatekBackup_{DateTime.Now:yyyyMMdd_HHmm}.sql";
                string backupPath = Path.Combine(backupFolder, fileName);

                Log.Information("Starting database backup to: {BackupPath}", backupPath);

                // 5. pg_dump işlemini başlat
                var psi = new ProcessStartInfo
                {
                    FileName = pgDumpPath,
                    Arguments = $"-h {dbInfo.Host} -p {dbInfo.Port} -U {dbInfo.User} -d {dbInfo.Database} -F p -f \"{backupPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Şifreyi çevre değişkeni olarak ver (Güvenli yöntem)
                psi.EnvironmentVariables["PGPASSWORD"] = dbInfo.Password;

                using (var process = Process.Start(psi))
                {
                    if (process == null) throw new InvalidOperationException("pg_dump process failed to start.");

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        Log.Error("pg_dump failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
                        throw new Exception($"Yedekleme işlemi başarısız oldu (Exit Code: {process.ExitCode}). Hata: {error}");
                    }
                }

                Log.Information("Database backup completed successfully: {BackupPath}", backupPath);
                return backupPath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Backup process failed.");
                throw;
            }
        }

        public void RestoreDatabase(string backupPath)
        {
            throw new NotSupportedException("Geri yükleme işlemi için lütfen pgAdmin kullanınız.");
        }

        private (string Host, string Port, string Database, string User, string Password) ParseConnectionString(string connectionString)
        {
            // Basit Regex ile connection string parçalama
            // Host=localhost;Port=5432;Database=kamatekcrm;Username=postgres;Password=postgres
            var host = Regex.Match(connectionString, "Host=([^;]+)", RegexOptions.IgnoreCase).Groups[1].Value;
            var port = Regex.Match(connectionString, "Port=([^;]+)", RegexOptions.IgnoreCase).Groups[1].Value;
            var database = Regex.Match(connectionString, "Database=([^;]+)", RegexOptions.IgnoreCase).Groups[1].Value;
            var user = Regex.Match(connectionString, "User(name)?=([^;]+)", RegexOptions.IgnoreCase).Groups[2].Value;
            var password = Regex.Match(connectionString, "Password=([^;]+)", RegexOptions.IgnoreCase).Groups[1].Value;

            if (string.IsNullOrEmpty(host)) host = "localhost";
            if (string.IsNullOrEmpty(port)) port = "5432";

            return (host, port, database, user, password);
        }

        private string FindPgDump()
        {
            // 1. Önce sistem PATH'inde ara
            string? pgDump = FindInPath("pg_dump.exe");
            if (!string.IsNullOrEmpty(pgDump)) return pgDump;

            // 2. Yaygın kurulum yollarını kontrol et (C:\Program Files\PostgreSQL\...\bin)
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string[] versions = { "17", "16", "15", "14", "13" };

            foreach (var version in versions)
            {
                string path = Path.Combine(programFiles, "PostgreSQL", version, "bin", "pg_dump.exe");
                if (File.Exists(path)) return path;
            }

            return string.Empty;
        }

        private string? FindInPath(string fileName)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return null;

            var paths = pathEnv.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                try
                {
                    string fullPath = Path.Combine(path, fileName);
                    if (File.Exists(fullPath)) return fullPath;
                }
                catch { } // Geçersiz path hatalarını yut
            }

            return null;
        }
    }
}
