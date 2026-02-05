using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KamatekCrm.Helpers
{
    /// <summary>
    /// WPF uygulaması tarafından API ve Web projelerini gizli olarak başlatır/durdurur.
    /// Zombie process temizliği ve otomatik tarayıcı açma özelliği içerir.
    /// </summary>
    public static class ProcessManager
    {
        private static Process? _apiProcess;
        private static Process? _webProcess;

        private const string API_PROCESS_NAME = "KamatekCrm.API";
        private const string WEB_PROCESS_NAME = "KamatekCrm.Web";
        private const string WEB_URL = "http://localhost:5200";
        private const int BROWSER_DELAY_MS = 3000;

        /// <summary>
        /// Önce zombie process'leri öldürür, sonra API ve Web projelerini başlatır.
        /// </summary>
        public static void StartProcesses()
        {
            try
            {
                // 1. KILL ZOMBIES: Mevcut eski process'leri öldür
                KillZombieProcesses();

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string apiPath = GetApiPath(baseDir);
                string webPath = GetWebPath(baseDir);

                // 2. START API (Port 5050)
                if (!string.IsNullOrEmpty(apiPath) && File.Exists(apiPath))
                {
                    _apiProcess = StartHiddenProcess(apiPath);
                    Debug.WriteLine($"API Started: {apiPath}");
                }
                else
                {
                    Debug.WriteLine($"API not found at: {apiPath}");
                }

                // 3. START WEB (Port 5200)
                if (!string.IsNullOrEmpty(webPath) && File.Exists(webPath))
                {
                    _webProcess = StartHiddenProcess(webPath);
                    Debug.WriteLine($"Web App Started: {webPath}");
                }
                else
                {
                    Debug.WriteLine($"Web not found at: {webPath}");
                }

                // 4. AUTO-BROWSER: 3 saniye bekle, sonra tarayıcıyı aç
                if (_webProcess != null)
                {
                    Task.Run(async () =>
                    {
                        await Task.Delay(BROWSER_DELAY_MS);
                        OpenDefaultBrowser(WEB_URL);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProcessManager Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Çalışan API ve Web process'lerini durdurur.
        /// </summary>
        public static void StopProcesses()
        {
            KillProcess(_apiProcess);
            KillProcess(_webProcess);
            
            // Ekstra güvenlik: isimle de öldür
            KillZombieProcesses();
        }

        /// <summary>
        /// Mevcut KamatekCrm.API ve KamatekCrm.Web zombie process'lerini öldürür.
        /// Bu, önceki çökme veya düzgün kapatılmamış oturumlardan kalan process'leri temizler.
        /// </summary>
        private static void KillZombieProcesses()
        {
            try
            {
                // API zombies
                var apiProcesses = Process.GetProcessesByName(API_PROCESS_NAME);
                foreach (var proc in apiProcesses)
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(2000);
                        Debug.WriteLine($"Killed zombie API process (PID: {proc.Id})");
                    }
                    catch { /* Ignore if already dead */ }
                    finally { proc.Dispose(); }
                }

                // Web zombies
                var webProcesses = Process.GetProcessesByName(WEB_PROCESS_NAME);
                foreach (var proc in webProcesses)
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(2000);
                        Debug.WriteLine($"Killed zombie Web process (PID: {proc.Id})");
                    }
                    catch { /* Ignore if already dead */ }
                    finally { proc.Dispose(); }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"KillZombieProcesses Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Varsayılan tarayıcıda belirtilen URL'yi açar.
        /// </summary>
        private static void OpenDefaultBrowser(string url)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
                Debug.WriteLine($"Browser opened: {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenDefaultBrowser Error: {ex.Message}");
            }
        }

        private static string? GetApiPath(string baseDir)
        {
            // 1. Release Mode (Sibling Folder)
            string releasePath = Path.Combine(baseDir, "Api", "KamatekCrm.API.exe");
            if (File.Exists(releasePath)) return releasePath;

            // 2. Debug Mode (Solution Structure) - .NET 9.0
            string? solutionRoot = FindSolutionRoot(baseDir);
            if (!string.IsNullOrEmpty(solutionRoot))
            {
                // Try Debug first
                string debugPath = Path.Combine(solutionRoot, "KamatekCrm.API", "bin", "Debug", "net9.0", "KamatekCrm.API.exe");
                if (File.Exists(debugPath)) return debugPath;

                // Try Release
                string releaseModePath = Path.Combine(solutionRoot, "KamatekCrm.API", "bin", "Release", "net9.0", "KamatekCrm.API.exe");
                if (File.Exists(releaseModePath)) return releaseModePath;
            }

            return null;
        }

        private static string? GetWebPath(string baseDir)
        {
            // 1. Release Mode (Sibling Folder)
            string releasePath = Path.Combine(baseDir, "Web", "KamatekCrm.Web.exe");
            if (File.Exists(releasePath)) return releasePath;

            // 2. Debug Mode (Solution Structure) - .NET 9.0
            string? solutionRoot = FindSolutionRoot(baseDir);
            if (!string.IsNullOrEmpty(solutionRoot))
            {
                // Try Debug first
                string debugPath = Path.Combine(solutionRoot, "KamatekCrm.Web", "bin", "Debug", "net9.0", "KamatekCrm.Web.exe");
                if (File.Exists(debugPath)) return debugPath;

                // Try Release
                string releaseModePath = Path.Combine(solutionRoot, "KamatekCrm.Web", "bin", "Release", "net9.0", "KamatekCrm.Web.exe");
                if (File.Exists(releaseModePath)) return releaseModePath;
            }

            return null;
        }

        private static string? FindSolutionRoot(string startPath)
        {
            DirectoryInfo? dir = new DirectoryInfo(startPath);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "KamatekCrm.sln")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        private static Process StartHiddenProcess(string filePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(filePath) // Important for appsettings.json
            };

            return Process.Start(startInfo)!;
        }

        private static void KillProcess(Process? process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(2000);
                    process.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to kill process: {ex.Message}");
            }
        }
    }
}
