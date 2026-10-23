using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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

        private static readonly ConcurrentBag<Process> _trackedProcesses = new();

        public static void TrackProcess(Process process)
        {
            if (process != null && !process.HasExited)
            {
                _trackedProcesses.Add(process);
            }
        }

        public static void StartServices(CancellationToken cancellationToken = default)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested) return;

                KillZombieProcesses();

                // 1. API Sunucusunu Başlat (Port 5050) — Veritabanı, JWT, SLA hepsi burada
                string? apiExe = FindExeRecursive("KamatekCrm.API.exe");
                if (!string.IsNullOrEmpty(apiExe))
                {
                    Serilog.Log.Information($"Starting API Server: {apiExe} (Port 5050)");
                    StartVisibleProcess(apiExe, new[] { "--urls", API_BIND_URL, "--environment", "Development" });
                }
                else
                {
                    Serilog.Log.Error("[ProcessManager] API exe not found! Backend (Port 5050) will not start.");
                    System.Windows.MessageBox.Show("API Sunucusu başlatılamadı (KamatekCrm.API.exe bulunamadı).\nVeritabanı işlemleri çalışmayacak.", 
                        "Kritik Hata", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    StopServices();
                    return;
                }

                // 2. Web Arayüzünü Başlat (Port 7000) — Teknisyen paneli
                string? webExe = FindExeRecursive("KamatekCrm.Web.exe");
                if (!string.IsNullOrEmpty(webExe))
                {
                    Serilog.Log.Information($"Starting Web App: {webExe} (Port 7000)");
                    StartVisibleProcess(webExe, new[] { "--urls", WEB_BIND_URL, "--environment", "Development" });
                }
                else
                {
                    Serilog.Log.Error("[ProcessManager] WEB exe not found! Web Interface (Port 7000) will not start.");
                    System.Windows.MessageBox.Show("Web Arayüzü başlatılamadı (KamatekCrm.Web.exe bulunamadı).", 
                        "Hata", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }

                // Open Browser after delay (Use Localhost URL for the server user)
                Task.Delay(3000, cancellationToken).ContinueWith(t =>
                {
                    if (!t.IsCanceled && !cancellationToken.IsCancellationRequested)
                    {
                        OpenBrowser(WEB_LOCAL_URL);
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Serilog.Log.Information("ProcessManager.StartServices cancelled.");
                StopServices();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error starting background services");
            }
        }

        public static void StopServices(CancellationToken cancellationToken = default)
        {
            try
            {
                while (_trackedProcesses.TryTake(out var process))
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                            process.Dispose();
                        }
                    }
                    catch
                    {
                        // Ignore already terminated processes
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Error while stopping tracked processes");
            }

            KillZombieProcesses();
        }

        public static void KillZombieProcesses()
        {
            string[] processNames = { "KamatekCrm.API", "KamatekCrm.Web", "pg_dump", "pg_restore" };
            foreach (var name in processNames)
            {
                try
                {
                    foreach (var process in Process.GetProcessesByName(name))
                    {
                        try
                        {
                            process.Kill(entireProcessTree: true);
                            process.Dispose();
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private static void StartVisibleProcess(string exePath, IEnumerable<string>? arguments = null)
        {
            try
            {
                var fullExePath = Path.GetFullPath(exePath);
                var psi = new ProcessStartInfo
                {
                    FileName = fullExePath,
                    UseShellExecute = true, // Mandatory for visible console
                    WindowStyle = ProcessWindowStyle.Normal,
                    WorkingDirectory = Path.GetDirectoryName(fullExePath)
                };

                if (arguments != null)
                {
                    foreach (var arg in arguments)
                    {
                        psi.ArgumentList.Add(arg);
                    }
                }
                
                var process = Process.Start(psi);
                if (process != null)
                {
                    TrackProcess(process);
                }
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
                string projectName = exeName.Replace(".exe", "");
                string likelyPath = Path.Combine(solutionRoot.FullName, projectName, "bin", "Debug", "net9.0", exeName);

                if (File.Exists(likelyPath)) return likelyPath;

                try 
                {
                   var files = solutionRoot.GetFiles(exeName, SearchOption.AllDirectories);
                   var match = files.FirstOrDefault(f => f.FullName.Contains("bin") && f.FullName.Contains("Debug") && f.FullName.Contains("net9.0"));
                   if (match != null) return match.FullName;
                   
                   if (files.Any()) return files.First().FullName;
                }
                catch {}
            }
            else
            {
                 string[] searchPaths = new[]
                 {
                    Path.Combine(currentDir, exeName),
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
