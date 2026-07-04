using ArturRios.Output;
using MongoDB.Driver;

namespace ArturRios.Data.MongoDb.Transactions;

/// <summary>
/// MongoDB implementation of the unit of work. Opens a client session, sets it as the context's
/// ambient session so repository operations enlist, and commits/aborts the transaction.
/// </summary>
/// <param name="client">The Mongo client.</param>
/// <param name="context">The Mongo context whose ambient session is managed.</param>
public class MongoUnitOfWork(IMongoClient client, MongoContext context) : IMongoUnitOfWork, IAsyncMongoUnitOfWork
{
    /// <inheritdoc />
    public ProcessOutput ExecuteInTransaction(Action work)
    {
        using var session = client.StartSession();
        context.Session = session;
        session.StartTransaction();
        try
        {
            work();
            session.CommitTransaction();
            return ProcessOutput.New;
        }
        catch (Exception ex)
        {
            session.AbortTransaction();
            return ProcessOutput.New.WithError(ex.GetBaseException().Message);
        }
        finally
        {
            context.Session = null;
        }
    }

    /// <inheritdoc />
    public DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work)
    {
        using var session = client.StartSession();
        context.Session = session;
        session.StartTransaction();
        try
        {
            var result = work();
            session.CommitTransaction();
            return DataOutput<TResult>.New.WithData(result);
        }
        catch (Exception ex)
        {
            session.AbortTransaction();
            return DataOutput<TResult>.New.WithError(ex.GetBaseException().Message);
        }
        finally
        {
            context.Session = null;
        }
    }

    /// <inheritdoc />
    public async Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)
    {
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        context.Session = session;
        session.StartTransaction();
        try
        {
            await work();
            await session.CommitTransactionAsync(ct);
            return ProcessOutput.New;
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync(ct);
            return ProcessOutput.New.WithError(ex.GetBaseException().Message);
        }
        finally
        {
            context.Session = null;
        }
    }

    /// <inheritdoc />
    public async Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken ct = default)
    {
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        context.Session = session;
        session.StartTransaction();
        try
        {
            var result = await work();
            await session.CommitTransactionAsync(ct);
            return DataOutput<TResult>.New.WithData(result);
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync(ct);
            return DataOutput<TResult>.New.WithError(ex.GetBaseException().Message);
        }
        finally
        {
            context.Session = null;
        }
    }
}
