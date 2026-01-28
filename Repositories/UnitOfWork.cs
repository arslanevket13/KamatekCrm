using System;
using KamatekCrm.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KamatekCrm.Repositories
{
    /// <summary>
    /// Unit of Work implementasyonu - AppDbContext sarmalayıcısı
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private IDbContextTransaction? _currentTransaction;
        private bool _disposed;

        public UnitOfWork()
        {
            _context = new AppDbContext();
        }

        public UnitOfWork(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public AppDbContext Context => _context;

        public IDbContextTransaction BeginTransaction()
        {
            if (_currentTransaction != null)
            {
                throw new InvalidOperationException("Zaten aktif bir transaction var. Önce mevcut transaction'ı tamamlayın.");
            }

            _currentTransaction = _context.Database.BeginTransaction();
            return _currentTransaction;
        }

        public int SaveChanges()
        {
            return _context.SaveChanges();
        }

        /// <summary>
        /// Eşzamanlılık hatası kontrolü ile kaydetme
        /// </summary>
        /// <returns>Başarılı ise true, eşzamanlılık hatası varsa false</returns>
        public (bool Success, string? ErrorMessage) SaveChangesWithConcurrencyHandling()
        {
            try
            {
                _context.SaveChanges();
                return (true, null);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Eşzamanlılık hatası: Kayıt başka bir kullanıcı tarafından değiştirildi
                foreach (var entry in ex.Entries)
                {
                    var databaseValues = entry.GetDatabaseValues();
                    if (databaseValues == null)
                    {
                        return (false, "Kayıt başka bir kullanıcı tarafından silindi.");
                    }
                    
                    // Veritabanındaki güncel değerleri al
                    entry.OriginalValues.SetValues(databaseValues);
                }
                return (false, "Kayıt başka bir kullanıcı tarafından değiştirildi. Lütfen sayfayı yenileyip tekrar deneyin.");
            }
        }

        public void Commit()
        {
            if (_currentTransaction == null)
            {
                throw new InvalidOperationException("Commit yapılacak aktif bir transaction yok.");
            }

            try
            {
                _context.SaveChanges();
                _currentTransaction.Commit();
            }
            finally
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }

        public void Rollback()
        {
            if (_currentTransaction == null)
            {
                return; // Sessizce çık, rollback yapmaya gerek yok
            }

            try
            {
                _currentTransaction.Rollback();
            }
            finally
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _currentTransaction?.Dispose();
                    _context.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
