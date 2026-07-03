using ArturRios.Output;

namespace ArturRios.Data.Core.Transactions;

/// <summary>
/// Coordinates a set of repository operations within a single database transaction.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Runs <paramref name="work"/> in a transaction, committing on success and rolling back on failure.</summary>
    ProcessOutput ExecuteInTransaction(Action work);

    /// <summary>Runs <paramref name="work"/> in a transaction, returning its result on success.</summary>
    DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work);

    /// <summary>Begins a transaction for manual commit/rollback control.</summary>
    IDbTransactionHandle BeginTransaction();
}
