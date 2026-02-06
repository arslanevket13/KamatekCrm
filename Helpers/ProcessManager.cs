using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KamatekCrm.Helpers
{
    public static class ProcessManager
    {
        private static Process? _apiProcess;
        private static Process? _webProcess;

        private const string API_PROCESS_NAME = "KamatekCrm.API";
        private const string WEB_PROCESS_NAME = "KamatekCrm.Web";
        // DÜZELTME 1: Port 7001 (Web projesiyle eslesmeli)
        private const string WEB_URL = "http://localhost:7001";
        private const int BROWSER_DELAY_MS = 3000;

        public static void StartProcesses()
        {
            try
            {
                KillZombieProcesses();

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string apiPath = GetApiPath(baseDir);
                string webPath = GetWebPath(baseDir);

                // API BASLAT (Gorunur Mod - Hata takibi icin)
                if (!string.IsNullOrEmpty(apiPath) && File.Exists(apiPath))
                {
                    _apiProcess = StartVisibleProcess(apiPath);
                    Debug.WriteLine($"API Started: {apiPath}");
                }

                // WEB BASLAT (Gorunur Mod - Console.ReadLine'in calismasi icin sart)
                if (!string.IsNullOrEmpty(webPath) && File.Exists(webPath))
                {
                    _webProcess = StartVisibleProcess(webPath);
                    Debug.WriteLine($"Web App Started: {webPath}");
                }

                // Tarayiciyi Ac
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

        public static void StopProcesses()
        {
            KillProcess(_apiProcess);
            KillProcess(_webProcess);
            KillZombieProcesses();
        }

        private static void KillZombieProcesses()
        {
            KillNamedProcess(API_PROCESS_NAME);
            KillNamedProcess(WEB_PROCESS_NAME);
        }

        private static void KillNamedProcess(string name)
        {
            foreach (var proc in Process.GetProcessesByName(name))
            {
                try { proc.Kill(); } catch { }
            }
        }

        private static void OpenDefaultBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch { }
        }

        // KRİTİK DÜZELTME: StartVisibleProcess (Gizli moddan cikildi)
        private static Process StartVisibleProcess(string filePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                // 1. UseShellExecute=true -> Yeni CMD penceresi acar, Console.ReadLine() calisir.
                // 2. WindowStyle=Normal -> Pencere gorunur, hata varsa okunabilir.
                UseShellExecute = true,
                CreateNoWindow = false, 
                WindowStyle = ProcessWindowStyle.Normal,
                WorkingDirectory = Path.GetDirectoryName(filePath) // appsettings.json icin sart
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
                    process.WaitForExit(1000);
                }
            }
            catch { }
        }

        public static string? GetApiPath(string baseDir) => FindExeRecursive(baseDir, "KamatekCrm.API", "KamatekCrm.API.exe");
        public static string? GetWebPath(string baseDir) => FindExeRecursive(baseDir, "KamatekCrm.Web", "KamatekCrm.Web.exe");
        
        // Parameterless overloads for App.xaml.cs
        public static string? GetApiPath() => GetApiPath(AppDomain.CurrentDomain.BaseDirectory);
        public static string? GetWebPath() => GetWebPath(AppDomain.CurrentDomain.BaseDirectory);

        private static string? FindExeRecursive(string baseDir, string projectName, string exeName)
        {
            string releasePath = Path.Combine(baseDir, projectName.Replace("KamatekCrm.", ""), exeName);
            if (File.Exists(releasePath)) return releasePath;

            DirectoryInfo? dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                string debugPath = Path.Combine(dir.FullName, projectName, "bin", "Debug", "net9.0", exeName);
                if (File.Exists(debugPath)) return debugPath;

                string releaseModePath = Path.Combine(dir.FullName, projectName, "bin", "Release", "net9.0", exeName);
                if (File.Exists(releaseModePath)) return releaseModePath;

                if (File.Exists(Path.Combine(dir.FullName, "KamatekCrm.sln"))) break;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
