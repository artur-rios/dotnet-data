using ArturRios.Output;

namespace ArturRios.Data.Relational.Core.Transactions;

/// <summary>
///     Coordinates a set of repository operations within a single database transaction.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    ///     Runs <paramref name="work" /> in a transaction, committing when it returns and rolling back when it throws.
    ///     A repository call inside it reports a failed write on its envelope and does not throw, so the work must
    ///     throw on a failed envelope for the transaction to roll back.
    /// </summary>
    ProcessOutput ExecuteInTransaction(Action work);

    /// <summary>Runs <paramref name="work" /> in a transaction, returning its result on success.</summary>
    DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work);

    /// <summary>Begins a transaction for manual commit/rollback control.</summary>
    IDbTransactionHandle BeginTransaction();
}
