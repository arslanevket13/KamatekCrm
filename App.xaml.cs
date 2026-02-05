using System;
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
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 1. QuestPDF Community License Configuration
                QuestPDF.Settings.License = LicenseType.Community;

                // 1.1 Start Background Processes (API & Web)
                ProcessManager.StartProcesses();

                // 2. Veritabanını başlat
                InitializeDatabase();

                // 3. Varsayılan admin kullanıcısı oluştur
                AuthService.CreateDefaultUser();

                // 4. SLA Otomasyon Servisini Başlat (TAM İZOLE ARKA PLAN)
                // UI Thread'i bloklamaması için tamamen ayrı bir Task içinde çalıştırıyoruz.
                Task.Run(async () => 
                {
                    try 
                    {
                        // Servis kendi içinde DbContext yönetiyor, burada sadece çağırıyoruz.
                        // Arka planda sessizce çalışır.
                        var slaService = new SlaService();
                        await slaService.CheckAndGenerateJobsAsync();
                    }
                    catch (Exception ex)
                    {
                        // Arka plan hatası UI'ı çökertmesin
                        System.Diagnostics.Debug.WriteLine($"SLA Service Background Error: {ex.Message}");
                    }
                });

                // DEBUG: Veritabanı yolunu göster
                string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KamatekCrm.db");
                // MessageBox.Show($"Sistem Başlatıldı.\nDB Yolu: {dbPath}\n\nLütfen 'admin' / '123' ile giriş yapın.", "Sistem Durumu", MessageBoxButton.OK, MessageBoxImage.Information);

                // 4. MainWindow'u oluştur ve göster
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;

                // 5. Login ekranını aktif et
                NavigationService.Instance.NavigateToLogin();
                // NavigationService.Instance.NavigateToMainContent(); // Giriş ekranını atlamak için bunu kullan

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
                
                // Stop background processes
                ProcessManager.StopProcesses();
                
                // Loglama veya debug eklenebilir
                System.Diagnostics.Debug.WriteLine("Auto backup completed on exit.");
            }
            catch (Exception ex)
            {
                // Kapanışta hata olursa kullanıcıyı rahatsız etmeyelim, ama loglayalım
                System.Diagnostics.Debug.WriteLine($"Auto backup failed: {ex.Message}");
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
                context.Database.Migrate(); // Migration kullan (EnsureCreated yerine)
                
                // Demo verilerini yükle (Seeder)
                DbSeeder.SeedDemoData(context);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veritabanı güncellenirken hata oluştu: {ex.Message}", "Veritabanı Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
