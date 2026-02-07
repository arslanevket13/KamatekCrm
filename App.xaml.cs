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
        // Proses yönetimi KamatekCrm.Helpers.ProcessManager tarafından yapılır


        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 3. API ve Web Sunucularını Başlat (Görünür Mod - ProcessManager)
                ProcessManager.StartServices();

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
                ProcessManager.StopServices();

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
        // Helper methods StopServices, StartBackgroundProcesses, KillZombieProcesses, StartHiddenProcess, KillProcess, OpenDefaultBrowser removed as they are now in ProcessManager


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
