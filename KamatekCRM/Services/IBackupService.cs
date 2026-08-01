namespace KamatekCrm.Services
{
    /// <summary>
    /// Veritabanı yedekleme ve geri yükleme servisi interface
    /// </summary>
    public interface IBackupService
    {
        string DefaultBackupDirectory { get; }
        string BackupFilePattern { get; }

        /// <summary>
        /// Veritabanını yedekle
        /// </summary>
        /// <returns>Yedek dosya yolu</returns>
        string BackupDatabase(string? destinationDirectory = null, string? label = null);

        BackupValidationResult ValidateBackup(string backupPath);

        /// <summary>
        /// Veritabanını yedekten geri yükle
        /// </summary>
        /// <param name="backupZipPath">Yedek dosya yolu</param>
        string RestoreDatabase(string backupPath);
    }
}
