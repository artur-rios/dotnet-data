using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Output;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArturRios.Data.Relational.Core.Transactions;

/// <summary>
///     Entity Framework Core implementation of <see cref="IUnitOfWork" /> and <see cref="IAsyncUnitOfWork" />.
///     Repository saves issued within the delegate flush but do not commit until the transaction commits.
/// </summary>
/// <param name="context">The application's <see cref="BaseDbContext" />.</param>
public class EfUnitOfWork(BaseDbContext context) : IUnitOfWork, IAsyncUnitOfWork
{
    /// <inheritdoc />
    public async Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)
    {
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        try
        {
            await work();
            await tx.CommitAsync(ct);
            return ProcessOutput.New;
        }
        catch (Exception ex)
        {
            await RollbackQuietlyAsync(tx);

            if (ex is OperationCanceledException)
            {
                throw;
            }

            return ProcessOutput.New.WithError(RelationalErrors.Describe(ex));
        }
    }

    /// <inheritdoc />
    public async Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work,
        CancellationToken ct = default)
    {
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await work();
            await tx.CommitAsync(ct);

            return DataOutput<TResult>.New.WithData(result);
        }
        catch (Exception ex)
        {
            await RollbackQuietlyAsync(tx);

            if (ex is OperationCanceledException)
            {
                throw;
            }

            return DataOutput<TResult>.New.WithError(RelationalErrors.Describe(ex));
        }
    }

    /// <inheritdoc />
    public async Task<IDbTransactionHandle> BeginTransactionAsync(CancellationToken ct = default) =>
        new EfTransactionHandle(await context.Database.BeginTransactionAsync(ct));

    /// <inheritdoc />
    public ProcessOutput ExecuteInTransaction(Action work)
    {
        using var tx = context.Database.BeginTransaction();
        try
        {
            work();
            tx.Commit();

            return ProcessOutput.New;
        }
        catch (OperationCanceledException)
        {
            RollbackQuietly(tx);

            throw;
        }
        catch (Exception ex)
        {
            RollbackQuietly(tx);

            return ProcessOutput.New.WithError(RelationalErrors.Describe(ex));
        }
    }

    /// <inheritdoc />
    public DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work)
    {
        using var tx = context.Database.BeginTransaction();
        try
        {
            var result = work();
            tx.Commit();

            return DataOutput<TResult>.New.WithData(result);
        }
        catch (OperationCanceledException)
        {
            RollbackQuietly(tx);

            throw;
        }
        catch (Exception ex)
        {
            RollbackQuietly(tx);

            return DataOutput<TResult>.New.WithError(RelationalErrors.Describe(ex));
        }
    }

    // Rollback must never mask the failure that triggered it: it runs untied to the caller's
    // token (which may already be canceled, making Rollback throw before it rolls anything back)
    // and swallows its own errors. Disposing the transaction rolls back whatever is left.
    private static async Task RollbackQuietlyAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch
        {
            // Already rolled back, or the connection is gone. Dispose completes the cleanup.
        }
    }

    private static void RollbackQuietly(IDbContextTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch
        {
            // Already rolled back, or the connection is gone. Dispose completes the cleanup.
        }
    }

    /// <inheritdoc />
    public IDbTransactionHandle BeginTransaction() =>
        new EfTransactionHandle(context.Database.BeginTransaction());

    private sealed class EfTransactionHandle(IDbContextTransaction transaction) : IDbTransactionHandle
    {
        public void Commit() => transaction.Commit();
        public void Rollback() => transaction.Rollback();
        public Task CommitAsync(CancellationToken ct = default) => transaction.CommitAsync(ct);
        public Task RollbackAsync(CancellationToken ct = default) => transaction.RollbackAsync(ct);
        public void Dispose() => transaction.Dispose();
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
