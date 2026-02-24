using System;
using System.Threading;
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
        /// Yeni bir veritabanı transaction'ı başlatır (Senkron)
        /// </summary>
        IDbContextTransaction BeginTransaction();

        /// <summary>
        /// Yeni bir veritabanı transaction'ı başlatır (Asenkron)
        /// </summary>
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Değişiklikleri veritabanına kaydeder (Senkron)
        /// </summary>
        int SaveChanges();

        /// <summary>
        /// Değişiklikleri veritabanına kaydeder (Asenkron)
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Transaction'ı onaylar (commit) (Senkron)
        /// </summary>
        void Commit();

        /// <summary>
        /// Transaction'ı onaylar (commit) (Asenkron)
        /// </summary>
        Task CommitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Transaction'ı geri alır (rollback) (Senkron)
        /// </summary>
        void Rollback();

        /// <summary>
        /// Transaction'ı geri alır (rollback) (Asenkron)
        /// </summary>
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
