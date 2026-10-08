using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Data.MongoDb.Transactions;
using ArturRios.Data.Tests.MongoDb.TestSupport;

namespace ArturRios.Data.Tests.MongoDb;

[Trait("Category", "Functional")]
public class MongoUnitOfWorkStandaloneTests(MongoStandaloneFixture fixture) : IClassFixture<MongoStandaloneFixture>
{
    [Fact]
    public async Task GivenAStandaloneServer_WhenExecutingInATransactionAsync_ThenAnErrorEnvelopeIsReturnedAndTheContextStaysUsable()
    {
        var context = fixture.NewContext(out var client);
        var repo = new MongoDocumentRepository<TestDoc>(context);
        var uow = new MongoUnitOfWork(client, context);

        var result = await uow.ExecuteInTransactionAsync(async () => await repo.CreateAsync(new TestDoc { Name = "a" }));

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.GenericMessage], result.Errors);
        Assert.Null(context.Session);

        var created = await repo.CreateAsync(new TestDoc { Name = "after" });
        Assert.True(created.Success);
        Assert.Equal(["after"], (await repo.GetAllAsync()).Data!.Select(d => d.Name));
    }

    [Fact]
    public void GivenAStandaloneServer_WhenExecutingInATransaction_ThenAnErrorEnvelopeIsReturnedAndTheContextStaysUsable()
    {
        var context = fixture.NewContext(out var client);
        var repo = new MongoDocumentRepository<TestDoc>(context);
        var uow = new MongoUnitOfWork(client, context);

        var result = uow.ExecuteInTransaction(() => repo.Create(new TestDoc { Name = "a" }).Data);

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.GenericMessage], result.Errors);
        Assert.Null(context.Session);

        var created = repo.Create(new TestDoc { Name = "after" });
        Assert.True(created.Success);
        Assert.Equal(["after"], repo.GetAll().Data!.Select(d => d.Name));
    }
}
