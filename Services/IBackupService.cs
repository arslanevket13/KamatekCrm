namespace KamatekCrm.Services
{
    /// <summary>
    /// Veritabanı yedekleme ve geri yükleme servisi interface
    /// </summary>
    public interface IBackupService
    {
        /// <summary>
        /// Veritabanını yedekle
        /// </summary>
        /// <returns>Yedek dosya yolu</returns>
        string BackupDatabase();

        /// <summary>
        /// Veritabanını yedekten geri yükle
        /// </summary>
        /// <param name="backupZipPath">Yedek dosya yolu</param>
        void RestoreDatabase(string backupZipPath);
    }
}
