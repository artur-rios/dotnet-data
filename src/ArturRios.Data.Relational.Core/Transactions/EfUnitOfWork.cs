using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Output;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArturRios.Data.Relational.Core.Transactions;

/// <summary>
///     Entity Framework Core implementation of <see cref="IUnitOfWork" /> and <see cref="IAsyncUnitOfWork" />.
///     Repository saves issued within the delegate flush but do not commit until the transaction commits.
///     Every failure - including one to begin the transaction - is returned as an error envelope; on rollback
///     the context's change tracker is cleared, since none of the transaction's writes were kept.
/// </summary>
/// <param name="context">The application's <see cref="BaseDbContext" />.</param>
public class EfUnitOfWork(BaseDbContext context) : IUnitOfWork, IAsyncUnitOfWork
{
    /// <inheritdoc />
    public Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            await work().ConfigureAwait(false);
            return ProcessOutput.New;
        }, error => ProcessOutput.New.WithError(error), ct);

    /// <inheritdoc />
    public Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work,
        CancellationToken ct = default) =>
        RunAsync(async () => DataOutput<TResult>.New.WithData(await work().ConfigureAwait(false)),
            error => DataOutput<TResult>.New.WithError(error), ct);

    /// <inheritdoc />
    public async Task<IDbTransactionHandle> BeginTransactionAsync(CancellationToken ct = default) =>
        new EfTransactionHandle(await context.Database.BeginTransactionAsync(ct).ConfigureAwait(false));

    /// <inheritdoc />
    public ProcessOutput ExecuteInTransaction(Action work) =>
        Run(() =>
        {
            work();
            return ProcessOutput.New;
        }, error => ProcessOutput.New.WithError(error));

    /// <inheritdoc />
    public DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work) =>
        Run(() => DataOutput<TResult>.New.WithData(work()), error => DataOutput<TResult>.New.WithError(error));

    // Every step that can fail - beginning the transaction (no connection, or one already open on the
    // context), the work and the commit - runs inside the guard, so a failure is enveloped like any
    // other instead of escaping.
    private async Task<TOutput> RunAsync<TOutput>(Func<Task<TOutput>> work, Func<string, TOutput> fail,
        CancellationToken ct)
    {
        IDbContextTransaction? tx = null;
        try
        {
            tx = await context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            var output = await work().ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);

            return output;
        }
        catch (Exception ex)
        {
            if (tx is not null)
            {
                await RollbackQuietlyAsync(tx).ConfigureAwait(false);
            }

            if (ex is OperationCanceledException)
            {
                throw;
            }

            return fail(RelationalErrors.Describe(ex));
        }
        finally
        {
            if (tx is not null)
            {
                await tx.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private TOutput Run<TOutput>(Func<TOutput> work, Func<string, TOutput> fail)
    {
        IDbContextTransaction? tx = null;
        try
        {
            tx = context.Database.BeginTransaction();
            var output = work();
            tx.Commit();

            return output;
        }
        catch (Exception ex)
        {
            if (tx is not null)
            {
                RollbackQuietly(tx);
            }

            if (ex is OperationCanceledException)
            {
                throw;
            }

            return fail(RelationalErrors.Describe(ex));
        }
        finally
        {
            tx?.Dispose();
        }
    }

    // Rollback must never mask the failure that triggered it: it runs untied to the caller's
    // token (which may already be canceled, making Rollback throw before it rolls anything back)
    // and swallows its own errors. Disposing the transaction rolls back whatever is left.
    // The change tracker is cleared afterwards: it still holds the transaction's flushed writes as
    // saved (and its failed ones as pending), none of which the database kept.
    private async Task RollbackQuietlyAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Already rolled back, or the connection is gone. Dispose completes the cleanup.
        }

        context.ChangeTracker.Clear();
    }

    private void RollbackQuietly(IDbContextTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch
        {
            // Already rolled back, or the connection is gone. Dispose completes the cleanup.
        }

        context.ChangeTracker.Clear();
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
