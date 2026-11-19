using ArturRios.Output;

namespace ArturRios.Data.Relational.Core.Transactions;

/// <summary>
///     Asynchronously coordinates repository operations within a single database transaction.
/// </summary>
public interface IAsyncUnitOfWork
{
    /// <summary>
    ///     Runs <paramref name="work" /> in a transaction, committing when it returns and rolling back when it throws.
    ///     A repository call inside it reports a failed write on its envelope and does not throw, so the work must
    ///     throw on a failed envelope for the transaction to roll back.
    /// </summary>
    Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default);

    /// <summary>Runs <paramref name="work" /> in a transaction, returning its result on success.</summary>
    Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work,
        CancellationToken ct = default);

    /// <summary>Begins a transaction for manual commit/rollback control.</summary>
    Task<IDbTransactionHandle> BeginTransactionAsync(CancellationToken ct = default);
}
