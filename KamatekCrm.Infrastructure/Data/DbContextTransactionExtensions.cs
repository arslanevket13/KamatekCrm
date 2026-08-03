using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KamatekCrm.Infrastructure.Data
{
    public static class DbContextTransactionExtensions
    {
        public static async Task<TResult> ExecuteInTransactionAsync<TResult>(
            this AppDbContext context,
            Func<IDbContextTransaction?, Task<TResult>> action,
            IsolationLevel isolationLevel = IsolationLevel.Serializable,
            CancellationToken cancellationToken = default)
        {
            var strategy = context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = context.Database.IsRelational()
                    ? await context.Database.BeginTransactionAsync(isolationLevel, cancellationToken)
                    : null;
                return await action(transaction);
            });
        }
    }
}
