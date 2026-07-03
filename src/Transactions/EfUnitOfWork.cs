using ArturRios.Data.Core.Configuration;
using ArturRios.Output;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArturRios.Data.Core.Transactions;

/// <summary>
/// Entity Framework Core implementation of <see cref="IUnitOfWork"/> and <see cref="IAsyncUnitOfWork"/>.
/// Repository saves issued within the delegate flush but do not commit until the transaction commits.
/// </summary>
/// <param name="context">The application's <see cref="BaseDbContext"/>.</param>
public class EfUnitOfWork(BaseDbContext context) : IUnitOfWork, IAsyncUnitOfWork
{
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
        catch (Exception ex)
        {
            tx.Rollback();
            return ProcessOutput.New.WithError(ex.GetBaseException().Message);
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
        catch (Exception ex)
        {
            tx.Rollback();
            return DataOutput<TResult>.New.WithError(ex.GetBaseException().Message);
        }
    }

    /// <inheritdoc />
    public IDbTransactionHandle BeginTransaction() =>
        new EfTransactionHandle(context.Database.BeginTransaction());

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
            await tx.RollbackAsync(ct);
            return ProcessOutput.New.WithError(ex.GetBaseException().Message);
        }
    }

    /// <inheritdoc />
    public async Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken ct = default)
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
            await tx.RollbackAsync(ct);
            return DataOutput<TResult>.New.WithError(ex.GetBaseException().Message);
        }
    }

    /// <inheritdoc />
    public async Task<IDbTransactionHandle> BeginTransactionAsync(CancellationToken ct = default) =>
        new EfTransactionHandle(await context.Database.BeginTransactionAsync(ct));

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
