using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Data.Sqlite;
using KamatekCrm.Data;

namespace KamatekCrm.Services
{
    public class BackupService
    {
        private const string DbFileName = "KamatekCrm.db";
        
        public string BackupDatabase()
        {
            try
            {
                // 1. DB Yolu (Absolute Path kullan)
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DbFileName);
                if (!File.Exists(dbPath))
                    throw new FileNotFoundException("Veritabanı dosyası bulunamadı!", dbPath);

                // 2. Hedef Klasör: Belgelerim / KamatekBackups
                var docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var backupFolder = Path.Combine(docPath, "KamatekBackups");
                
                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }

                // 3. Dosya İsimleri
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
                var tempDbPath = Path.Combine(Path.GetTempPath(), $"{DbFileName}.bak");
                var zipPath = Path.Combine(backupFolder, $"KamatekBackup_{timestamp}.zip");

                // 4. Veritabanını Güvenli Kopyala (Online Backup)
                // 4. Veritabanını Güvenli Kopyala (Online Backup)
                var connectionString = $"Data Source={dbPath}";
                var tempConnectionString = $"Data Source={tempDbPath}";

                // STEP 1: Backup inside using block to ensure disposal
                using (var source = new SqliteConnection(connectionString))
                using (var destination = new SqliteConnection(tempConnectionString))
                {
                    source.Open();
                    destination.Open();
                    
                    // SQLite Backup API
                    source.BackupDatabase(destination);
                }

                // STEP 2: CLEAR POOLS to release the .bak file handle
                // This is critical because SQLite connection pooling might keep the file open
                SqliteConnection.ClearAllPools();

                // STEP 3: Now it is safe to ZIP the tempBakPath
                // 5. Zip Sıkıştırma
                if (File.Exists(zipPath)) File.Delete(zipPath);

                using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(tempDbPath, DbFileName);
                }

                // 6. Geçici dosyayı temizle
                if (File.Exists(tempDbPath)) File.Delete(tempDbPath);

                return zipPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Yedekleme hatası: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Yedekten geri yükleme - ZIP dosyasından veritabanını geri yükler
        /// </summary>
        public void RestoreDatabase(string backupZipPath)
        {
            try
            {
                if (!File.Exists(backupZipPath))
                    throw new FileNotFoundException("Yedek dosyası bulunamadı!", backupZipPath);

                // 1. Aktif DB yolu
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DbFileName);

                // 2. Geçici klasör oluştur
                var tempExtractPath = Path.Combine(Path.GetTempPath(), "KamatekRestore_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempExtractPath);

                try
                {
                    // 3. ZIP dosyasını aç ve içeriği çıkar
                    ZipFile.ExtractToDirectory(backupZipPath, tempExtractPath);

                    // 4. BULLETPROOF FIX: Tüm alt klasörlerde ara ve büyük/küçük harf farkını yoksay
                    var allExtractedFiles = Directory.GetFiles(tempExtractPath, "*.*", SearchOption.AllDirectories);
                    var dbFileToRestore = allExtractedFiles.FirstOrDefault(f => 
                        f.EndsWith(".db", StringComparison.OrdinalIgnoreCase) || 
                        f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetFileName(f).Equals("KamatekCrm.db", StringComparison.OrdinalIgnoreCase));
                    
                    if (string.IsNullOrEmpty(dbFileToRestore))
                        throw new FileNotFoundException($"Yedek içinde veritabanı dosyası bulunamadı! Taranan dosya sayısı: {allExtractedFiles.Length}");

                    // 5. TÜM SQLite bağlantılarını zorla kapat
                    SqliteConnection.ClearAllPools();

                    // 6. Küçük bir gecikme (Dosya kilidinin serbest kalması için)
                    System.Threading.Thread.Sleep(500);

                    // 7. Aktif veritabanını üzerine yaz
                    File.Copy(dbFileToRestore, dbPath, overwrite: true);
                }
                finally
                {
                    // 8. Geçici klasörü temizle
                    if (Directory.Exists(tempExtractPath))
                    {
                        try { Directory.Delete(tempExtractPath, recursive: true); }
                        catch { /* Cleanup failure is not critical */ }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Geri yükleme hatası: {ex.Message}", ex);
            }
        }
    }
}
