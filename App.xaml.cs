using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using KamatekCrm.Data;
using KamatekCrm.Services;
using KamatekCrm.Helpers;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

namespace KamatekCrm
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// WPF Launcher - API ve Web sunucularını yönetir
    /// </summary>
    public partial class App : Application
    {
        private Process? _apiProcess;
        private Process? _webProcess;
        private const string WEB_URL = "http://localhost:7001";

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 1. TEMİZLİK: Zombie süreçleri agresif şekilde öldür
                KillZombieProcesses();
                await Task.Delay(500);

                // 2. QuestPDF Community License Configuration
                QuestPDF.Settings.License = LicenseType.Community;

                // 3. API ve Web Sunucularını Başlat (Gizli Mod)
                StartBackgroundProcesses();

                // 4. Veritabanını başlat
                InitializeDatabase();

                // 5. Varsayılan admin kullanıcısı oluştur
                AuthService.CreateDefaultUser();

                // 6. SLA Otomasyon Servisini Başlat (Tamamen arka plan)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var slaService = new SlaService();
                        await slaService.CheckAndGenerateJobsAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"SLA Service Background Error: {ex.Message}");
                    }
                });

                // 7. MainWindow'u oluştur ve göster
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;

                // 8. Login ekranını aktif et
                NavigationService.Instance.NavigateToLogin();

                mainWindow.Show();

                // 9. TARAYICI: 3 saniye bekle ve siteyi aç
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    OpenDefaultBrowser(WEB_URL);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Uygulama başlatılırken hata oluştu:\n\n{ex.Message}\n\nDetay: {ex.InnerException?.Message}",
                    "Başlatma Hatası",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            try
            {
                // Uygulama kapanırken otomatik yedek al
                var backupService = new BackupService();
                backupService.BackupDatabase();

                // Sunucu süreçlerini kapat
                KillProcess(_apiProcess);
                KillProcess(_webProcess);

                // Ekstra güvenlik: isimle de öldür
                KillZombieProcesses();

                Debug.WriteLine("Auto backup completed on exit.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Auto backup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// API ve Web sunucularını gizli modda başlatır.
        /// </summary>
        private void StartBackgroundProcesses()
        {
            try
            {
                string? apiPath = ProcessManager.GetApiPath();
                string? webPath = ProcessManager.GetWebPath();

                if (!string.IsNullOrEmpty(apiPath) && File.Exists(apiPath))
                {
                    _apiProcess = StartHiddenProcess(apiPath);
                    Debug.WriteLine($"API Started: {apiPath}");
                }
                else
                {
                    Debug.WriteLine($"API not found at expected path");
                }

                if (!string.IsNullOrEmpty(webPath) && File.Exists(webPath))
                {
                    _webProcess = StartHiddenProcess(webPath);
                    Debug.WriteLine($"Web App Started: {webPath}");
                }
                else
                {
                    Debug.WriteLine($"Web not found at expected path");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartBackgroundProcesses Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Zombie süreçleri agresif şekilde öldürür.
        /// </summary>
        private static void KillZombieProcesses()
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName("KamatekCrm.API"))
                {
                    try { proc.Kill(); proc.WaitForExit(2000); }
                    catch { /* Ignore */ }
                    finally { proc.Dispose(); }
                }

                foreach (var proc in Process.GetProcessesByName("KamatekCrm.Web"))
                {
                    try { proc.Kill(); proc.WaitForExit(2000); }
                    catch { /* Ignore */ }
                    finally { proc.Dispose(); }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"KillZombieProcesses Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gizli modda process başlatır.
        /// </summary>
        private static Process StartHiddenProcess(string filePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(filePath)
            };

            return Process.Start(startInfo)!;
        }

        /// <summary>
        /// Belirtilen process'i öldürür.
        /// </summary>
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

        /// <summary>
        /// Varsayılan tarayıcıda URL açar.
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

        /// <summary>
        /// Veritabanını başlat ve migration'ları uygula
        /// </summary>
        private static void InitializeDatabase()
        {
            using var context = new AppDbContext();
            try
            {
                context.Database.Migrate();
                DbSeeder.SeedDemoData(context);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veritabanı güncellenirken hata oluştu: {ex.Message}", "Veritabanı Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
