using System;
using System.Threading.Tasks;
using KamatekCrm.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace KamatekCrm.Repositories
{
    /// <summary>
    /// Unit of Work arayüzü - Tüm veritabanı işlemlerini tek bir transaction altında yönetir.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Mevcut DbContext örneği
        /// </summary>
        AppDbContext Context { get; }

        /// <summary>
        /// Yeni bir veritabanı transaction'ı başlatır
        /// </summary>
        IDbContextTransaction BeginTransaction();

        /// <summary>
        /// Değişiklikleri veritabanına kaydeder (Senkron)
        /// </summary>
        int SaveChanges();

        /// <summary>
        /// Değişiklikleri veritabanına kaydeder (Asenkron)
        /// </summary>
        Task<int> SaveChangesAsync();

        /// <summary>
        /// Transaction'ı onaylar (commit)
        /// </summary>
        void Commit();

        /// <summary>
        /// Transaction'ı geri alır (rollback)
        /// </summary>
        void Rollback();
    }
}
