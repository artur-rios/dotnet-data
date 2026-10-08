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
    public ProcessOutput ExecuteInTransaction(Action work) =>
        Run(() =>
        {
            work();
            return ProcessOutput.New;
        }, error => ProcessOutput.New.WithError(error));

    /// <inheritdoc />
    public DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work) =>
        Run(() => DataOutput<TResult>.New.WithData(work()), error => DataOutput<TResult>.New.WithError(error));

    // Every step that can fail — opening the session, starting the transaction (which throws on a standalone
    // server), the work and the commit — runs inside the guard, so a failure is enveloped like any other and
    // the finally always restores the caller's ambient session before the session is disposed. The session is
    // made ambient only once its transaction has started.
    private async Task<TOutput> RunAsync<TOutput>(Func<Task<TOutput>> work, Func<string, TOutput> fail,
        CancellationToken ct)
    {
        var previousSession = context.Session;
        IClientSessionHandle? session = null;
        try
        {
            session = await client.StartSessionAsync(cancellationToken: ct).ConfigureAwait(false);
            session.StartTransaction();
            context.Session = session;
            var output = await work().ConfigureAwait(false);
            await session.CommitTransactionAsync(ct).ConfigureAwait(false);
            return output;
        }
        catch (Exception ex)
        {
            if (session is not null)
            {
                await AbortQuietlyAsync(session).ConfigureAwait(false);
            }

            if (ex is OperationCanceledException)
            {
                throw;
            }

            return fail(MongoErrors.Describe(ex));
        }
        finally
        {
            context.Session = previousSession;
            session?.Dispose();
        }
    }

    private TOutput Run<TOutput>(Func<TOutput> work, Func<string, TOutput> fail)
    {
        var previousSession = context.Session;
        IClientSessionHandle? session = null;
        try
        {
            session = client.StartSession();
            session.StartTransaction();
            context.Session = session;
            var output = work();
            session.CommitTransaction();
            return output;
        }
        catch (Exception ex)
        {
            if (session is not null)
            {
                AbortQuietly(session);
            }

            if (ex is OperationCanceledException)
            {
                throw;
            }

            return fail(MongoErrors.Describe(ex));
        }
        finally
        {
            context.Session = previousSession;
            session?.Dispose();
        }
    }

    // Abort must never mask the failure that triggered it: the server aborts the transaction itself
    // on a write conflict, so an explicit abort can fail on a transaction that is already gone. It
    // also runs untied to the caller's token, which may already be canceled.
    private static async Task AbortQuietlyAsync(IClientSessionHandle session)
    {
        try
        {
            await session.AbortTransactionAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Never started, already aborted, or the session is gone. Disposing the session completes the cleanup.
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
            // Never started, already aborted, or the session is gone. Disposing the session completes the cleanup.
        }
    }
}
