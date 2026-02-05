using System;
using System.Diagnostics;
using System.IO;

namespace KamatekCrm.Helpers
{
    public static class ProcessManager
    {
        private static Process? _apiProcess;
        private static Process? _webProcess;

        public static void StartProcesses()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string apiPath = GetApiPath(baseDir);
                string webPath = GetWebPath(baseDir);

                if (!string.IsNullOrEmpty(apiPath) && File.Exists(apiPath))
                {
                    _apiProcess = StartHiddenProcess(apiPath);
                    Debug.WriteLine($"API Started: {apiPath}");
                }

                if (!string.IsNullOrEmpty(webPath) && File.Exists(webPath))
                {
                    _webProcess = StartHiddenProcess(webPath);
                    Debug.WriteLine($"Web App Started: {webPath}");
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
        }

        private static string GetApiPath(string baseDir)
        {
            // 1. Release Mode (Sibling Folder)
            string releasePath = Path.Combine(baseDir, "Api", "KamatekCrm.API.exe");
            if (File.Exists(releasePath)) return releasePath;

            // 2. Debug Mode (Solution Structure)
            string? solutionRoot = FindSolutionRoot(baseDir);
            if (!string.IsNullOrEmpty(solutionRoot))
            {
                return Path.Combine(solutionRoot, "KamatekCrm.API", "bin", "Debug", "net8.0", "KamatekCrm.API.exe");
            }

            return null;
        }

        private static string GetWebPath(string baseDir)
        {
            // 1. Release Mode (Sibling Folder)
            string releasePath = Path.Combine(baseDir, "Web", "KamatekCrm.Web.exe");
            if (File.Exists(releasePath)) return releasePath;

            // 2. Debug Mode (Solution Structure)
            string? solutionRoot = FindSolutionRoot(baseDir);
            if (!string.IsNullOrEmpty(solutionRoot))
            {
                return Path.Combine(solutionRoot, "KamatekCrm.Web", "bin", "Debug", "net8.0", "KamatekCrm.Web.exe");
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
