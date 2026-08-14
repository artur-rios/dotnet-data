using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Output;
using MongoDB.Driver;

namespace ArturRios.Data.MongoDb.Transactions;

/// <summary>
///     MongoDB implementation of the unit of work. Opens a client session, sets it as the context's
///     ambient session so repository operations enlist, and commits/aborts the transaction.
/// </summary>
/// <param name="client">The Mongo client.</param>
/// <param name="context">The Mongo context whose ambient session is managed.</param>
public class MongoUnitOfWork(IMongoClient client, MongoContext context) : IMongoUnitOfWork, IAsyncMongoUnitOfWork
{
    /// <inheritdoc />
    public async Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)
    {
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        var previousSession = context.Session;
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
            await AbortQuietlyAsync(session);

            if (ex is OperationCanceledException)
            {
                throw;
            }

            return ProcessOutput.New.WithError(MongoErrors.Describe(ex));
        }
        finally
        {
            context.Session = previousSession;
        }
    }

    /// <inheritdoc />
    public async Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work,
        CancellationToken ct = default)
    {
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        var previousSession = context.Session;
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
            await AbortQuietlyAsync(session);

            if (ex is OperationCanceledException)
            {
                throw;
            }

            return DataOutput<TResult>.New.WithError(MongoErrors.Describe(ex));
        }
        finally
        {
            context.Session = previousSession;
        }
    }

    /// <inheritdoc />
    public ProcessOutput ExecuteInTransaction(Action work)
    {
        using var session = client.StartSession();
        var previousSession = context.Session;
        context.Session = session;
        session.StartTransaction();
        try
        {
            work();
            session.CommitTransaction();
            return ProcessOutput.New;
        }
        catch (OperationCanceledException)
        {
            AbortQuietly(session);

            throw;
        }
        catch (Exception ex)
        {
            AbortQuietly(session);

            return ProcessOutput.New.WithError(MongoErrors.Describe(ex));
        }
        finally
        {
            context.Session = previousSession;
        }
    }

    /// <inheritdoc />
    public DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work)
    {
        using var session = client.StartSession();
        var previousSession = context.Session;
        context.Session = session;
        session.StartTransaction();
        try
        {
            var result = work();
            session.CommitTransaction();
            return DataOutput<TResult>.New.WithData(result);
        }
        catch (OperationCanceledException)
        {
            AbortQuietly(session);

            throw;
        }
        catch (Exception ex)
        {
            AbortQuietly(session);

            return DataOutput<TResult>.New.WithError(MongoErrors.Describe(ex));
        }
        finally
        {
            context.Session = previousSession;
        }
    }

    // Abort must never mask the failure that triggered it: the server aborts the transaction itself
    // on a write conflict, so an explicit abort can fail on a transaction that is already gone. It
    // also runs untied to the caller's token, which may already be canceled.
    private static async Task AbortQuietlyAsync(IClientSessionHandle session)
    {
        try
        {
            await session.AbortTransactionAsync(CancellationToken.None);
        }
        catch
        {
            // Already aborted, or the session is gone. Disposing the session completes the cleanup.
        }
    }

    private static void AbortQuietly(IClientSessionHandle session)
    {
        try
        {
            session.AbortTransaction();
        }
        catch
        {
            // Already aborted, or the session is gone. Disposing the session completes the cleanup.
        }
    }
}
