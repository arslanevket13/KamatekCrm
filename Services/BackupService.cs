using System;
using System.IO;

namespace KamatekCrm.Services
{
    public class BackupService
    {
        public string BackupDatabase()
        {
            throw new NotSupportedException("Yedekleme işlemi PostgreSQL için henüz aktif değil. Lütfen pgAdmin veya pg_dump kullanın.");
        }

        public void RestoreDatabase(string backupZipPath)
        {
             throw new NotSupportedException("Geri yükleme işlemi PostgreSQL için henüz aktif değil. Lütfen pgAdmin veya psql kullanın.");
        }
    }
}
