using System;
using System.Threading.Tasks;
using ArturRios.Data.MongoDb;
using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Data.MongoDb.Transactions;
using ArturRios.Data.Tests.MongoDb.TestSupport;
using ArturRios.Output;
using MongoDB.Driver;

namespace ArturRios.Data.Tests.MongoDb;

[Trait("Category", "Unit")]
public class MongoUnitOfWorkSessionTests
{
    public static TheoryData<string> Overloads => ["Async", "AsyncWithResult", "Sync", "SyncWithResult"];

    [Theory]
    [MemberData(nameof(Overloads))]
    public async Task GivenATransactionThatCannotStart_WhenExecuting_ThenAnErrorEnvelopeIsReturnedAndTheContextIsClean(
        string overload)
    {
        var session = new SessionThatCannotStartATransaction();
        var context = NewContext();
        var uow = new MongoUnitOfWork(ClientReturning(session.Handle), context);
        var workRan = false;

        var result = await Execute(uow, overload, () => workRan = true);

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.GenericMessage], result.Errors);
        Assert.False(workRan);
        Assert.Null(context.Session);
        Assert.True(session.Disposed);
    }

    [Theory]
    [MemberData(nameof(Overloads))]
    public async Task GivenAnAmbientSession_WhenANestedTransactionCannotStart_ThenTheAmbientSessionIsRestored(
        string overload)
    {
        var context = NewContext();
        var ambient = new SessionThatCannotStartATransaction().Handle;
        context.Session = ambient;
        var uow = new MongoUnitOfWork(ClientReturning(new SessionThatCannotStartATransaction().Handle), context);

        var result = await Execute(uow, overload, () => { });

        Assert.False(result.Success);
        Assert.Same(ambient, context.Session);
    }

    private static async Task<ProcessOutput> Execute(MongoUnitOfWork uow, string overload, Action work) =>
        overload switch
        {
            "Async" => await uow.ExecuteInTransactionAsync(() =>
            {
                work();
                return Task.CompletedTask;
            }),
            "AsyncWithResult" => await uow.ExecuteInTransactionAsync(() =>
            {
                work();
                return Task.FromResult(1);
            }),
            "Sync" => uow.ExecuteInTransaction(work),
            "SyncWithResult" => uow.ExecuteInTransaction(() =>
            {
                work();
                return 1;
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(overload))
        };

    private static MongoContext NewContext() => new(InterfaceStub.For<IMongoDatabase>((_, _) => (false, null)));

    private static IMongoClient ClientReturning(IClientSessionHandle session) =>
        InterfaceStub.For<IMongoClient>((method, _) => method.Name switch
        {
            nameof(IMongoClient.StartSession) => (true, session),
            nameof(IMongoClient.StartSessionAsync) => (true, Task.FromResult(session)),
            _ => (false, null)
        });

    // A session on a server without transaction support: StartTransaction throws, as the driver does
    // once it knows the server is a standalone mongod.
    private sealed class SessionThatCannotStartATransaction
    {
        public SessionThatCannotStartATransaction() =>
            Handle = InterfaceStub.For<IClientSessionHandle>((method, _) =>
            {
                switch (method.Name)
                {
                    case nameof(IClientSessionHandle.StartTransaction):
                        throw new NotSupportedException("Standalone servers do not support transactions.");
                    case nameof(IDisposable.Dispose):
                        Disposed = true;
                        return (true, null);
                    case nameof(IClientSessionHandle.AbortTransaction):
                    case nameof(IClientSessionHandle.AbortTransactionAsync):
                        throw new InvalidOperationException("No transaction started.");
                    default:
                        return (false, null);
                }
            });

        public IClientSessionHandle Handle { get; }

        public bool Disposed { get; private set; }
    }
}
