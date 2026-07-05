using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Transactions;

/// <summary>Coordinates document operations within a single MongoDB transaction (requires a replica set).</summary>
public interface IMongoUnitOfWork
{
    /// <summary>Runs <paramref name="work"/> in a transaction, committing on success and aborting on failure.</summary>
    ProcessOutput ExecuteInTransaction(Action work);

    /// <summary>Runs <paramref name="work"/> in a transaction, returning its result on success.</summary>
    DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work);
}
