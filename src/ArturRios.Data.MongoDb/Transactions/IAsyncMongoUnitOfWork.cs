using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Transactions;

/// <summary>Asynchronously coordinates document operations within a single MongoDB transaction (requires a replica set).</summary>
public interface IAsyncMongoUnitOfWork
{
    /// <summary>Runs <paramref name="work" /> in a transaction, committing on success and aborting on failure.</summary>
    Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default);

    /// <summary>Runs <paramref name="work" /> in a transaction, returning its result on success.</summary>
    Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work,
        CancellationToken ct = default);
}
