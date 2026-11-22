using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KamatekCrm.Settings;
using Npgsql;
using Serilog;

namespace KamatekCrm.Services
{
    public sealed class ManagedPostgresService : IManagedPostgresService
    {
        private const string ManagedUsername = "kamatek_admin";
        private const string TargetDatabaseName = "kamatekcrm";
        private const int PortRangeStart = 5439;
        private const int PortRangeEnd = 5450;
        private static readonly char[] PasswordAlphabet =
            "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789".ToCharArray();

        public string ResolveDataDirectory(out bool usedFallback)
        {
            var programDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var primaryPath = Path.Combine(programDataRoot, "KamatekCRM", "pgdata");

            if (CanWriteToDirectory(primaryPath))
            {
                usedFallback = false;
                return primaryPath;
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var fallbackPath = Path.Combine(localAppData, "KamatekCRM", "pgdata");
            usedFallback = true;

            if (fallbackPath.Any(c => c > 127))
            {
                Log.Warning(
                    "ProgramData yazma izni olmadığı için yedek veri dizinine geçildi ancak yol ASCII dışı/Türkçe karakter içeriyor: {FallbackPath}",
                    fallbackPath);
            }
            else
            {
                Log.Information("ProgramData yazma izni olmadığı için yedek veri dizini kullanılıyor: {FallbackPath}", fallbackPath);
            }

            Directory.CreateDirectory(fallbackPath);
            return fallbackPath;
        }

        public string? ResolveBinDirectory()
        {
            var embeddedBin = Path.Combine(AppContext.BaseDirectory, "PostgresRuntime", "bin");
            if (HasRequiredBinaries(embeddedBin))
            {
                Log.Information("Yerel PostgreSQL runtime dizini seçildi (Gömülü): {BinDir}", embeddedBin);
                return embeddedBin;
            }

            var programFilesRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            foreach (var root in programFilesRoots.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                var pgBase = Path.Combine(root, "PostgreSQL");
                if (!Directory.Exists(pgBase)) continue;

                try
                {
                    var candidateBins = Directory.GetDirectories(pgBase)
                        .OrderByDescending(ParseMajorVersion)
                        .ThenByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                        .Select(d => Path.Combine(d, "bin"));

                    foreach (var bin in candidateBins)
                    {
                        if (HasRequiredBinaries(bin))
                        {
                            Log.Information("Yerel PostgreSQL runtime dizini seçildi (Sistem Fallback): {BinDir}", bin);
                            return bin;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Sistem PostgreSQL dizini taranırken hata oluştu: {Path}", pgBase);
                }
            }

            return null;
        }

        private static int ParseMajorVersion(string directoryPath)
        {
            var name = Path.GetFileName(directoryPath) ?? string.Empty;
            var digits = new string(name.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ver) ? ver : 0;
        }

        public bool IsRuntimeAvailable() => ResolveBinDirectory() is not null;

        public bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Yönetici yetki kontrolü sırasında hata oluştu.");
                return false;
            }
        }

        public async Task<ManagedPostgresResult> EnsureProvisionedAndRunningAsync(
            bool allowLanAccess,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            var dataDir = ResolveDataDirectory(out bool usedFallback);
            var binDir = ResolveBinDirectory();

            if (binDir is null)
            {
                var expectedInitDb = Path.Combine(AppContext.BaseDirectory, "PostgresRuntime", "bin", "initdb.exe");
                return new ManagedPostgresResult(
                    Success: false,
                    ConnectionString: string.Empty,
                    Port: AppSettings.ManagedPostgresPort,
                    DataDirectory: dataDir,
                    ErrorMessage: $"Yerel PostgreSQL çalışma zamanı bulunamadı. Beklenen dosya: '{expectedInitDb}' (veya C:\\Program Files\\PostgreSQL\\*\\bin).",
                    UsedFallbackDataDirectory: usedFallback);
            }

            var existingConnStr = AppSettings.PostgreSqlConnectionString;
            var configuredPort = AppSettings.ManagedPostgresPort is >= PortRangeStart and <= PortRangeEnd
                ? AppSettings.ManagedPostgresPort
                : PortRangeStart;
            bool allowOrphanReset = !AppSettings.IsFirstRunCompleted;

            if (AppSettings.IsManagedLocalPostgres &&
                !string.IsNullOrWhiteSpace(existingConnStr) &&
                await IsServerReadyAsync(binDir, configuredPort, ct).ConfigureAwait(false))
            {
                progress?.Report("Yerel veritabanı motoru zaten çalışıyor, bağlantı doğrulanıyor...");
                try
                {
                    await EnsureTargetDatabaseExistsAsync(existingConnStr, ct).ConfigureAwait(false);
                    Log.Information(
                        "Mevcut çalışan yerel PostgreSQL kullanılıyor. DataDir: {DataDir}, Port: {Port}, Mode: ExistingRunning",
                        dataDir,
                        configuredPort);
                    return new ManagedPostgresResult(
                        Success: true,
                        ConnectionString: existingConnStr,
                        Port: configuredPort,
                        DataDirectory: dataDir,
                        WasAlreadyRunning: true,
                        UsedFallbackDataDirectory: usedFallback);
                }
                catch (PostgresException pgEx) when (pgEx.SqlState == "28P01" && allowOrphanReset)
                {
                    Log.Warning(
                        pgEx,
                        "İlk kurulum tamamlanmadan kalan yerel PostgreSQL örneğinde parola uyuşmazlığı (28P01) tespit edildi; pgdata sıfırlanıyor. DataDir: {DataDir}",
                        dataDir);
                    await StopClusterAsync(binDir, dataDir, ct).ConfigureAwait(false);
                    CleanupPartialDataDirectory(dataDir);
                }
            }

            if (IsRunningAsAdministrator())
            {
                return new ManagedPostgresResult(
                    Success: false,
                    ConnectionString: string.Empty,
                    Port: configuredPort,
                    DataDirectory: dataDir,
                    ErrorMessage: "Visual Studio/uygulama Yönetici modunda çalışıyor; yerel PostgreSQL standart kullanıcı yetkisi gerektirir.",
                    UsedFallbackDataDirectory: usedFallback);
            }

            CleanupStalePidIfNeeded(dataDir);

            bool isInitialized = File.Exists(Path.Combine(dataDir, "PG_VERSION"));
            int port;
            string password;

            if (isInitialized)
            {
                port = configuredPort;
                password = ExtractPassword(existingConnStr);
                if (string.IsNullOrEmpty(password))
                {
                    if (allowOrphanReset)
                    {
                        Log.Warning(
                            "İlk kurulum tamamlanmamış ancak mevcut pgdata için kayıtlı parola yok; yetim pgdata temizlenip yeniden kuruluyor: {DataDir}",
                            dataDir);
                        await StopClusterAsync(binDir, dataDir, ct).ConfigureAwait(false);
                        CleanupPartialDataDirectory(dataDir);
                        isInitialized = false;
                    }
                    else
                    {
                        return new ManagedPostgresResult(
                            Success: false,
                            ConnectionString: string.Empty,
                            Port: port,
                            DataDirectory: dataDir,
                            ErrorMessage: "Mevcut yerel veri kümesi için kayıtlı erişim parolası bulunamadı. Gelişmiş bağlantı ayarlarını kullanın veya yeni kurulum yapın.",
                            UsedFallbackDataDirectory: usedFallback);
                    }
                }
            }

            bool retriedAfterAuthMismatch = false;
            while (true)
            {
                bool usingFreshDataDir = !isInitialized;
                if (usingFreshDataDir)
                {
                    CleanupPartialDataDirectory(dataDir);

                    progress?.Report("Uygun yerel veritabanı portu belirleniyor...");
                    port = FindAvailablePort(PortRangeStart, PortRangeEnd);
                    password = GenerateSecurePassword(24);

                    Log.Information(
                        "Yeni yerel PostgreSQL veri kümesi oluşturuluyor. DataDir: {DataDir}, Port: {Port}, Mode: FreshInit",
                        dataDir,
                        port);

                    progress?.Report("Yerel PostgreSQL veri kümesi oluşturuluyor...");
                    var initResult = await RunInitDbAsync(binDir, dataDir, password, ct).ConfigureAwait(false);
                    if (!initResult.Success)
                    {
                        var initError = initResult.ErrorOutput;
                        if (initError.Contains("administrator", StringComparison.OrdinalIgnoreCase) ||
                            initError.Contains("root", StringComparison.OrdinalIgnoreCase))
                        {
                            initError = "Visual Studio/uygulama Yönetici modunda çalışıyor; yerel PostgreSQL standart kullanıcı yetkisi gerektirir.";
                        }

                        return new ManagedPostgresResult(
                            Success: false,
                            ConnectionString: string.Empty,
                            Port: port,
                            DataDirectory: dataDir,
                            ErrorMessage: $"Veritabanı ilk kurulumu (initdb) başarısız oldu: {initError}",
                            UsedFallbackDataDirectory: usedFallback);
                    }
                }
                else
                {
                    port = configuredPort;
                    password = ExtractPassword(existingConnStr);
                    Log.Information(
                        "Mevcut yerel PostgreSQL veri kümesi başlatılıyor. DataDir: {DataDir}, Port: {Port}, Mode: ExistingDataDir",
                        dataDir,
                        port);
                }

                progress?.Report("PostgreSQL güvenlik ve ağ yapılandırması yazılıyor...");
                WritePostgresConfig(dataDir, port, allowLanAccess);
                WritePgHbaConfig(dataDir, allowLanAccess);

                progress?.Report("Yerel PostgreSQL motoru başlatılıyor...");
                var startResult = StartPgCtlDetached(binDir, dataDir, port);
                if (!startResult.Success)
                {
                    var logTail = ReadTailOfPostgresLog(dataDir, 15);
                    var detail = string.IsNullOrWhiteSpace(logTail) ? startResult.ErrorOutput : $"{startResult.ErrorOutput}\n{logTail}";
                    return new ManagedPostgresResult(
                        Success: false,
                        ConnectionString: string.Empty,
                        Port: port,
                        DataDirectory: dataDir,
                        ErrorMessage: $"Yerel PostgreSQL başlatılamadı: {detail}",
                        UsedFallbackDataDirectory: usedFallback);
                }

                bool ready = await WaitUntilReadyAsync(binDir, port, TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
                if (!ready)
                {
                    var logTail = ReadTailOfPostgresLog(dataDir, 15);
                    var detail = string.IsNullOrWhiteSpace(logTail)
                        ? "Yerel PostgreSQL başlatıldı ancak 15 saniye içinde bağlantıya hazır hale gelmedi."
                        : $"Yerel PostgreSQL 15 saniye içinde hazır hale gelmedi. Son log satırları:\n{logTail}";
                    return new ManagedPostgresResult(
                        Success: false,
                        ConnectionString: string.Empty,
                        Port: port,
                        DataDirectory: dataDir,
                        ErrorMessage: detail,
                        UsedFallbackDataDirectory: usedFallback);
                }

                var targetConnBuilder = new NpgsqlConnectionStringBuilder
                {
                    Host = "127.0.0.1",
                    Port = port,
                    Database = TargetDatabaseName,
                    Username = ManagedUsername,
                    Password = password
                };
                var finalConnectionString = targetConnBuilder.ConnectionString;

                progress?.Report("KamatekCRM veritabanı kontrol ediliyor...");
                try
                {
                    await EnsureTargetDatabaseExistsAsync(finalConnectionString, ct).ConfigureAwait(false);
                }
                catch (PostgresException pgEx) when (pgEx.SqlState == "28P01" && !usingFreshDataDir && allowOrphanReset && !retriedAfterAuthMismatch)
                {
                    retriedAfterAuthMismatch = true;
                    Log.Warning(
                        pgEx,
                        "Mevcut yetim pgdata ile parola uyuşmazlığı (28P01) alındı; pg_ctl stop + temizleme sonrası sıfırdan initdb çalıştırılıyor. DataDir: {DataDir}",
                        dataDir);
                    progress?.Report("Önceki yarım kurulum temizleniyor, veritabanı sıfırdan hazırlanıyor...");
                    await StopClusterAsync(binDir, dataDir, ct).ConfigureAwait(false);
                    CleanupPartialDataDirectory(dataDir);
                    isInitialized = false;
                    continue;
                }

                AppSettings.SaveManagedPostgresState(
                    plainConnectionString: finalConnectionString,
                    port: port,
                    allowLanAccess: allowLanAccess);

                Log.Information(
                    "Yönetilen yerel PostgreSQL hazır. Port: {Port}, LAN: {AllowLan}, DataDir: {DataDir}, Mode: {Mode}",
                    port,
                    allowLanAccess,
                    dataDir,
                    usingFreshDataDir ? "FreshInit" : "ExistingDataDir");

                return new ManagedPostgresResult(
                    Success: true,
                    ConnectionString: finalConnectionString,
                    Port: port,
                    DataDirectory: dataDir,
                    WasAlreadyRunning: false,
                    UsedFallbackDataDirectory: usedFallback);
            }
        }

        public async Task StopIfSinglePcModeAsync(CancellationToken ct = default)
        {
            if (!AppSettings.IsManagedLocalPostgres)
            {
                return;
            }

            if (AppSettings.AllowLanAccess)
            {
                Log.Information("LAN / Ana Sunucu modu aktif olduğu için yerel PostgreSQL arka planda açık bırakıldı.");
                return;
            }

            var binDir = ResolveBinDirectory();
            if (binDir is null) return;

            var dataDir = ResolveDataDirectory(out _);
            var pidFile = Path.Combine(dataDir, "postmaster.pid");
            if (!File.Exists(pidFile)) return;

            try
            {
                var pgCtl = Path.Combine(binDir, "pg_ctl.exe");
                var args = $"stop -D \"{dataDir}\" -m fast -w";
                var result = await RunProcessAsync(pgCtl, args, TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
                if (result.Success)
                {
                    Log.Information("Tek bilgisayar modu: yerel PostgreSQL güvenli şekilde durduruldu.");
                }
                else
                {
                    Log.Warning("Yerel PostgreSQL durdurulurken uyarı alındı: {Error}", result.ErrorOutput);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Yerel PostgreSQL kapatılırken hata oluştu.");
            }
        }

        private static bool CanWriteToDirectory(string directoryPath)
        {
            try
            {
                Directory.CreateDirectory(directoryPath);
                var probe = Path.Combine(directoryPath, $".write_test_{Guid.NewGuid():N}");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Dizin yazma izni testi başarısız: {Directory}", directoryPath);
                return false;
            }
        }

        private static bool HasRequiredBinaries(string binDir)
        {
            if (!Directory.Exists(binDir)) return false;
            return File.Exists(Path.Combine(binDir, "initdb.exe"))
                && File.Exists(Path.Combine(binDir, "pg_ctl.exe"))
                && File.Exists(Path.Combine(binDir, "postgres.exe"))
                && File.Exists(Path.Combine(binDir, "pg_isready.exe"));
        }

        private static void CleanupStalePidIfNeeded(string dataDir)
        {
            var pidFile = Path.Combine(dataDir, "postmaster.pid");
            if (!File.Exists(pidFile)) return;

            try
            {
                var firstLine = File.ReadLines(pidFile).FirstOrDefault()?.Trim();
                if (int.TryParse(firstLine, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pid))
                {
                    bool isAlivePostgres = false;
                    try
                    {
                        using var proc = Process.GetProcessById(pid);
                        isAlivePostgres = !proc.HasExited &&
                            proc.ProcessName.Contains("postgres", StringComparison.OrdinalIgnoreCase);
                    }
                    catch (ArgumentException)
                    {
                        isAlivePostgres = false;
                    }
                    catch (InvalidOperationException)
                    {
                        isAlivePostgres = false;
                    }

                    if (!isAlivePostgres)
                    {
                        Log.Warning("Ölü süreç PID={Pid} tespit edildi; stale postmaster.pid temizleniyor.", pid);
                        File.Delete(pidFile);
                    }
                }
                else
                {
                    Log.Warning("Bozuk postmaster.pid dosyası temizleniyor.");
                    File.Delete(pidFile);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "postmaster.pid kontrolü sırasında hata oluştu.");
            }
        }

        private static int FindAvailablePort(int startPort, int endPort)
        {
            for (int port = startPort; port <= endPort; port++)
            {
                try
                {
                    using var anyListener = new TcpListener(IPAddress.Any, port);
                    anyListener.Start();
                    anyListener.Stop();

                    using var loopbackListener = new TcpListener(IPAddress.Loopback, port);
                    loopbackListener.Start();
                    loopbackListener.Stop();

                    return port;
                }
                catch (SocketException sockEx)
                {
                    Log.Debug(sockEx, "Port {Port} kullanımda, sıradaki port deneniyor.", port);
                }
            }

            throw new InvalidOperationException($"{startPort}-{endPort} aralığında uygun TCP portu bulunamadı.");
        }

        private static string GenerateSecurePassword(int length)
        {
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = PasswordAlphabet[RandomNumberGenerator.GetInt32(PasswordAlphabet.Length)];
            }
            return new string(chars);
        }

        private static string ExtractPassword(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return string.Empty;
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);
                return builder.Password ?? string.Empty;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Bağlantı dizesinden parola çözümlenemedi.");
                return string.Empty;
            }
        }

        private static async Task<ProcessResult> RunInitDbAsync(string binDir, string dataDir, string password, CancellationToken ct)
        {
            var pwFile = Path.Combine(Path.GetTempPath(), $"kamatek_pg_{Guid.NewGuid():N}.pw");
            try
            {
                File.WriteAllText(pwFile, password, Encoding.ASCII);
                var initDbExe = Path.Combine(binDir, "initdb.exe");
                var args = $"-D \"{dataDir}\" -U {ManagedUsername} --encoding=UTF8 --no-locale --auth=scram-sha-256 --pwfile=\"{pwFile}\"";
                return await RunProcessAsync(initDbExe, args, TimeSpan.FromMinutes(2), ct).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    if (File.Exists(pwFile))
                    {
                        File.WriteAllText(pwFile, string.Empty);
                        File.Delete(pwFile);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Geçici initdb parola dosyası silinemedi: {Path}", pwFile);
                }
            }
        }

        private static async Task StopClusterAsync(string binDir, string dataDir, CancellationToken ct)
        {
            var pidFile = Path.Combine(dataDir, "postmaster.pid");
            if (!File.Exists(pidFile)) return;

            try
            {
                var pgCtl = Path.Combine(binDir, "pg_ctl.exe");
                var args = $"stop -D \"{dataDir}\" -m fast -w";
                var res = await RunProcessAsync(pgCtl, args, TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
                if (!res.Success)
                {
                    Log.Warning("pg_ctl stop uyarı döndürdü: {Error}", res.ErrorOutput);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Yerel PostgreSQL durdurulurken hata oluştu: {DataDir}", dataDir);
            }
        }

        private static void WritePostgresConfig(string dataDir, int port, bool allowLanAccess)
        {
            var confPath = Path.Combine(dataDir, "postgresql.conf");
            var listenAddresses = allowLanAccess ? "*" : "localhost";

            var managedBlock = new StringBuilder();
            managedBlock.AppendLine("# --- KamatekCRM Managed Settings Start ---");
            managedBlock.AppendLine($"listen_addresses = '{listenAddresses}'");
            managedBlock.AppendLine($"port = {port.ToString(CultureInfo.InvariantCulture)}");
            managedBlock.AppendLine("shared_buffers = 128MB");
            managedBlock.AppendLine("max_connections = 50");
            managedBlock.AppendLine("password_encryption = scram-sha-256");
            managedBlock.AppendLine("# --- KamatekCRM Managed Settings End ---");

            var existing = File.Exists(confPath) ? File.ReadAllText(confPath, Encoding.UTF8) : string.Empty;
            const string startMarker = "# --- KamatekCRM Managed Settings Start ---";
            const string endMarker = "# --- KamatekCRM Managed Settings End ---";

            var startIdx = existing.IndexOf(startMarker, StringComparison.Ordinal);
            var endIdx = existing.IndexOf(endMarker, StringComparison.Ordinal);

            string updated;
            if (startIdx >= 0 && endIdx > startIdx)
            {
                var afterEnd = endIdx + endMarker.Length;
                updated = existing[..startIdx].TrimEnd() + Environment.NewLine + Environment.NewLine + managedBlock + existing[afterEnd..];
            }
            else
            {
                updated = existing.TrimEnd() + Environment.NewLine + Environment.NewLine + managedBlock;
            }

            File.WriteAllText(confPath, updated, new UTF8Encoding(false));
        }

        private static void WritePgHbaConfig(string dataDir, bool allowLanAccess)
        {
            var hbaPath = Path.Combine(dataDir, "pg_hba.conf");
            var sb = new StringBuilder();
            sb.AppendLine("# KamatekCRM Managed pg_hba.conf");
            sb.AppendLine("host    all             all             127.0.0.1/32            scram-sha-256");
            sb.AppendLine("host    all             all             ::1/128                 scram-sha-256");
            if (allowLanAccess)
            {
                sb.AppendLine("host    all             all             10.0.0.0/8              scram-sha-256");
                sb.AppendLine("host    all             all             172.16.0.0/12           scram-sha-256");
                sb.AppendLine("host    all             all             192.168.0.0/16          scram-sha-256");
            }
            File.WriteAllText(hbaPath, sb.ToString(), new UTF8Encoding(false));
        }

        private static void CleanupPartialDataDirectory(string dataDir)
        {
            if (!Directory.Exists(dataDir)) return;
            try
            {
                if (!Directory.EnumerateFileSystemEntries(dataDir).Any()) return;

                Log.Warning("Yarım kalmış pgdata klasörü (PG_VERSION yok) tespit edildi; temizleniyor: {DataDir}", dataDir);
                foreach (var file in Directory.GetFiles(dataDir))
                {
                    File.Delete(file);
                }
                foreach (var subDir in Directory.GetDirectories(dataDir))
                {
                    Directory.Delete(subDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Yarım kalmış pgdata klasörü temizlenirken hata oluştu: {DataDir}", dataDir);
            }
        }

        private static ProcessResult StartPgCtlDetached(string binDir, string dataDir, int port)
        {
            var pgCtl = Path.Combine(binDir, "pg_ctl.exe");
            var logPath = Path.Combine(dataDir, "postgres.log");
            var args = $"start -D \"{dataDir}\" -l \"{logPath}\" -o \"-p {port.ToString(CultureInfo.InvariantCulture)}\"";

            try
            {
                // KRİTİK: RedirectStandardOutput/Error = false olmalı; aksi halde child postgres.exe
                // pipe handle'larını miras alır ve WaitForExit sonsuza kadar kilitlenir.
                var psi = new ProcessStartInfo
                {
                    FileName = pgCtl,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };

                using var process = Process.Start(psi);
                if (process is null)
                {
                    return new ProcessResult(false, -1, string.Empty, $"pg_ctl başlatılamadı: {pgCtl}");
                }

                return new ProcessResult(true, 0, string.Empty, string.Empty);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "pg_ctl.exe start çalıştırılamadı.");
                return new ProcessResult(false, -1, string.Empty, ex.Message);
            }
        }

        private static string ReadTailOfPostgresLog(string dataDir, int maxLines)
        {
            var logPath = Path.Combine(dataDir, "postgres.log");
            if (!File.Exists(logPath)) return string.Empty;

            try
            {
                using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs, Encoding.UTF8);
                var lines = new System.Collections.Generic.Queue<string>(maxLines + 1);
                while (reader.ReadLine() is { } line)
                {
                    if (lines.Count >= maxLines) lines.Dequeue();
                    lines.Enqueue(line);
                }
                return string.Join(Environment.NewLine, lines).Trim();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "postgres.log okunamadı: {Path}", logPath);
                return string.Empty;
            }
        }

        private static async Task<bool> IsServerReadyAsync(string binDir, int port, CancellationToken ct)
        {
            var pgIsReady = Path.Combine(binDir, "pg_isready.exe");
            var args = $"-h 127.0.0.1 -p {port.ToString(CultureInfo.InvariantCulture)}";
            var res = await RunProcessAsync(pgIsReady, args, TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            return res.Success;
        }

        private static async Task<bool> WaitUntilReadyAsync(string binDir, int port, TimeSpan timeout, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                ct.ThrowIfCancellationRequested();
                if (await IsServerReadyAsync(binDir, port, ct).ConfigureAwait(false))
                {
                    return true;
                }
                await Task.Delay(400, ct).ConfigureAwait(false);
            }
            return false;
        }

        private static async Task EnsureTargetDatabaseExistsAsync(string targetConnectionString, CancellationToken ct)
        {
            var adminBuilder = new NpgsqlConnectionStringBuilder(targetConnectionString)
            {
                Database = "postgres"
            };

            await using var conn = new NpgsqlConnection(adminBuilder.ConnectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            await using var checkCmd = new NpgsqlCommand(
                "SELECT 1 FROM pg_database WHERE datname = @db", conn);
            checkCmd.Parameters.AddWithValue("db", TargetDatabaseName);
            var exists = await checkCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);

            if (exists is null)
            {
                await using var createCmd = new NpgsqlCommand(
                    $"CREATE DATABASE {TargetDatabaseName} WITH ENCODING 'UTF8'", conn);
                await createCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                Log.Information("Yerel PostgreSQL içinde '{Database}' veritabanı oluşturuldu.", TargetDatabaseName);
            }
        }

        private static async Task<ProcessResult> RunProcessAsync(
            string fileName,
            string arguments,
            TimeSpan timeout,
            CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };
            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();

            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdOut.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stdErr.AppendLine(e.Data); };

            if (!process.Start())
            {
                return new ProcessResult(false, -1, string.Empty, $"Süreç başlatılamadı: {fileName}");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (Exception killEx) { Log.Debug(killEx, "Zaman aşımına uğrayan süreç sonlandırılamadı: {File}", fileName); }
                return new ProcessResult(false, -1, stdOut.ToString(), $"İşlem zaman aşımına uğradı ({timeout.TotalSeconds:0}s).");
            }

            var errText = stdErr.ToString().Trim();
            if (string.IsNullOrEmpty(errText) && process.ExitCode != 0)
            {
                errText = stdOut.ToString().Trim();
            }

            return new ProcessResult(process.ExitCode == 0, process.ExitCode, stdOut.ToString().Trim(), errText);
        }

        private sealed record ProcessResult(bool Success, int ExitCode, string StandardOutput, string ErrorOutput);
    }
}
