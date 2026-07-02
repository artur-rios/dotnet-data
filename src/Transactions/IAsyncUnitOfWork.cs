using ArturRios.Output;

namespace ArturRios.Data.Transactions;

/// <summary>
/// Asynchronously coordinates repository operations within a single database transaction.
/// </summary>
public interface IAsyncUnitOfWork
{
    /// <summary>Runs <paramref name="work"/> in a transaction, committing on success and rolling back on failure.</summary>
    Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default);

    /// <summary>Runs <paramref name="work"/> in a transaction, returning its result on success.</summary>
    Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken ct = default);

    /// <summary>Begins a transaction for manual commit/rollback control.</summary>
    Task<IDbTransactionHandle> BeginTransactionAsync(CancellationToken ct = default);
}
