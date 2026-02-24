using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KamatekCrm.Helpers
{
    public static class ProcessManager
    {
        // Bind URLs (Listen on all interfaces)
        public const string API_BIND_URL = "http://0.0.0.0:5050";
        public const string WEB_BIND_URL = "http://0.0.0.0:7000";

        // Localhost URLs (For opening browser on the server machine)
        public const string API_LOCAL_URL = "http://localhost:5050";
        public const string WEB_LOCAL_URL = "http://localhost:7000";

        public static void StartServices()
        {
            try
            {
                KillZombieProcesses();

                // 1. API Sunucusunu Başlat (Port 5050) — Veritabanı, JWT, SLA hepsi burada
                string? apiExe = FindExeRecursive("KamatekCrm.API.exe");
                if (!string.IsNullOrEmpty(apiExe))
                {
                    Serilog.Log.Information($"Starting API Server: {apiExe} (Port 5050)");
                    StartVisibleProcess(apiExe, $"--urls \"{API_BIND_URL}\" --environment Development");
                }
                else
                {
                    Serilog.Log.Error("[ProcessManager] API exe not found! Backend (Port 5050) will not start.");
                    System.Windows.MessageBox.Show("API Sunucusu başlatılamadı (KamatekCrm.API.exe bulunamadı).\nVeritabanı işlemleri çalışmayacak.", 
                        "Kritik Hata", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }

                // 2. Web Arayüzünü Başlat (Port 7000) — Teknisyen paneli
                string? webExe = FindExeRecursive("KamatekCrm.Web.exe");
                if (!string.IsNullOrEmpty(webExe))
                {
                    Serilog.Log.Information($"Starting Web App: {webExe} (Port 7000)");
                    StartVisibleProcess(webExe, $"--urls \"{WEB_BIND_URL}\" --environment Development");
                }
                else
                {
                    Serilog.Log.Error("[ProcessManager] WEB exe not found! Web Interface (Port 7000) will not start.");
                    System.Windows.MessageBox.Show("Web Arayüzü başlatılamadı (KamatekCrm.Web.exe bulunamadı).", 
                        "Hata", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }

                // Open Browser after delay (Use Localhost URL for the server user)
                Task.Delay(3000).ContinueWith(_ => OpenBrowser(WEB_LOCAL_URL));
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error starting background services");
            }
        }

        public static void StopServices()
        {
            KillZombieProcesses();
        }

        private static void KillZombieProcesses()
        {
            foreach (var process in Process.GetProcessesByName("KamatekCrm.API"))
            {
                try { process.Kill(); } catch { }
            }
            foreach (var process in Process.GetProcessesByName("KamatekCrm.Web"))
            {
                try { process.Kill(); } catch { }
            }
        }

        private static void StartVisibleProcess(string exePath, string arguments = "")
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    UseShellExecute = true, // [CRITICAL] Mandatory for visible console
                    WindowStyle = ProcessWindowStyle.Normal, // [CRITICAL] Mandatory for debugging
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                };
                
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start process {exePath}: {ex.Message}");
            }
        }

        private static string? FindExeRecursive(string exeName)
        {
            var currentProcessPath = Process.GetCurrentProcess().MainModule?.FileName;
            var currentDir = Path.GetDirectoryName(currentProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;

            // Strategy: Look up the directory tree until we find the solution root (indicated by .sln)
            // Then search down specifically in bin/Debug/net9.0/ folders for the target exe
            
            var directory = new DirectoryInfo(currentDir);
            DirectoryInfo? solutionRoot = null;

            while (directory != null)
            {
                if (directory.GetFiles("*.sln").Any())
                {
                    solutionRoot = directory;
                    break;
                }
                directory = directory.Parent;
            }

            if (solutionRoot != null)
            {
                // We found solution root. Now construct likely paths.
                // Assuming standard project structure: SolutionRoot/ProjectName/bin/Debug/net9.0/ExeName
                
                string projectName = exeName.Replace(".exe", ""); // e.g. KamatekCrm.API
                string likelyPath = Path.Combine(solutionRoot.FullName, projectName, "bin", "Debug", "net9.0", exeName);

                if (File.Exists(likelyPath)) return likelyPath;

                // Fallback: Search recursively in solution root but limit depth or filter by bin
                // Search specifically for the file in AllDirectories
                try 
                {
                   var files = solutionRoot.GetFiles(exeName, SearchOption.AllDirectories);
                   // Prefer bin/Debug/net9.0
                   var match = files.FirstOrDefault(f => f.FullName.Contains("bin") && f.FullName.Contains("Debug") && f.FullName.Contains("net9.0"));
                   if (match != null) return match.FullName;
                   
                   // Fallback to any found
                   if (files.Any()) return files.First().FullName;
                }
                catch {}
            }
            else
            {
                // Fallback for when running in VS without deployed structure or weird path
                // Try relative paths from current dir
                // ../../../KamatekCrm.API/bin/Debug/net9.0/KamatekCrm.API.exe
                 string[] searchPaths = new[]
                 {
                    Path.Combine(currentDir, exeName), // Same folder
                    Path.Combine(currentDir, "..", "..", "..", exeName.Replace(".exe", ""), "bin", "Debug", "net9.0", exeName),
                    Path.Combine(currentDir, "..", "..", "..", exeName.Replace(".exe", ""), "bin", "Release", "net9.0", exeName)
                 };

                 foreach (var path in searchPaths)
                 {
                     var fullPath = Path.GetFullPath(path);
                     if (File.Exists(fullPath)) return fullPath;
                 }
            }

            return null;
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
