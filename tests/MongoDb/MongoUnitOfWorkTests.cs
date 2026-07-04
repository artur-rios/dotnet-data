using System;
using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.MongoDb;
using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Data.MongoDb.Transactions;
using ArturRios.Data.Tests.MongoDb.TestSupport;
using MongoDB.Driver;
using Xunit;

namespace ArturRios.Data.Tests.MongoDb;

[Collection(MongoTestCollection.Name)]
public class MongoUnitOfWorkTests(MongoReplicaSetFixture fixture)
{
    [Fact]
    public async Task Commit_PersistsAllWrites()
    {
        var context = fixture.NewContext(out var client);
        var repo = new MongoDocumentRepository<TestDoc>(context);
        var uow = new MongoUnitOfWork(client, context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new TestDoc { Name = "a" });
            await repo.CreateAsync(new TestDoc { Name = "b" });
        });

        Assert.True(result.Success);
        Assert.Equal(2, repo.GetAll().Data!.Count());
    }

    [Fact]
    public async Task Rollback_OnException_PersistsNothing()
    {
        var context = fixture.NewContext(out var client);
        var repo = new MongoDocumentRepository<TestDoc>(context);
        var uow = new MongoUnitOfWork(client, context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new TestDoc { Name = "doomed" });
            throw new InvalidOperationException("force rollback");
        });

        Assert.False(result.Success);
        Assert.Empty(repo.GetAll().Data!);
    }

    [Fact]
    public async Task ReadInsideTransaction_SeesUncommittedWrite()
    {
        var context = fixture.NewContext(out var client);
        var repo = new MongoDocumentRepository<TestDoc>(context);
        var uow = new MongoUnitOfWork(client, context);

        var seen = await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new TestDoc { Name = "inside" });
            return (await repo.GetAllAsync()).Data!.Count();
        });

        Assert.True(seen.Success);
        Assert.Equal(1, seen.Data);
    }
}
