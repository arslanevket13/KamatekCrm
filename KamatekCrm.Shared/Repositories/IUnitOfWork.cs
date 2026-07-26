using System;
using System.Threading;
using System.Threading.Tasks;

namespace KamatekCrm.Shared.Repositories
{
    /// <summary>
    /// Unit of Work arayüzü - Tüm veritabanı işlemlerini tek bir transaction altında yönetir.
    /// EF Core tiplerini sızdırmayan temiz mimari arayüzü.
    /// </summary>
    public interface IUnitOfWork : IDisposable, IAsyncDisposable
    {
        IRepository<TEntity> Repository<TEntity>() where TEntity : class;

        void BeginTransaction();
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);

        int SaveChanges();
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        void Commit();
        Task CommitAsync(CancellationToken cancellationToken = default);

        void Rollback();
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
