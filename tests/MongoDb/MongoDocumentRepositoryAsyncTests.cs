using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Data.Tests.MongoDb.TestSupport;

namespace ArturRios.Data.Tests.MongoDb;

[Collection(MongoTestCollection.Name)]
[Trait("Category", "Functional")]
public class MongoDocumentRepositoryAsyncTests(MongoReplicaSetFixture fixture)
{
    private MongoDocumentRepository<TestDoc> NewRepo() => new(fixture.NewContext());

    [Fact]
    public async Task GivenADocument_WhenCreatedAndFetchedByIdAsynchronously_ThenItRoundTrips()
    {
        var repo = NewRepo();
        var doc = new TestDoc { Name = "a" };
        var create = await repo.CreateAsync(doc, TestContext.Current.CancellationToken);
        Assert.True(create.Success);

        var found = await repo.GetByIdAsync(create.Data!, TestContext.Current.CancellationToken);
        Assert.True(found.Success);
        Assert.Equal("a", found.Data!.Name);

        var missing = await repo.GetByIdAsync("507f1f77bcf86cd799439011", TestContext.Current.CancellationToken);
        Assert.True(missing.Success);
        Assert.Null(missing.Data);
    }

    [Fact]
    public async Task GivenSeveralDocuments_WhenReadingThemAsynchronouslyEveryWay_ThenEachReadAgrees()
    {
        var repo = NewRepo();
        await repo.CreateRangeAsync([new TestDoc { Name = "keep" }, new TestDoc { Name = "drop" }], TestContext.Current.CancellationToken);

        Assert.Equal(2, (await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!.Count());
        Assert.Single((await repo.FindAsync(d => d.Name == "keep", TestContext.Current.CancellationToken)).Data!);
    }

    [Fact]
    public async Task GivenADocument_WhenUpdatedAndDeletedAsynchronously_ThenBothTakeEffect()
    {
        var repo = NewRepo();
        var doc = new TestDoc { Name = "a" };
        await repo.CreateAsync(doc, TestContext.Current.CancellationToken);

        doc.Name = "b";
        Assert.True((await repo.UpdateAsync(doc, TestContext.Current.CancellationToken)).Success);

        Assert.True((await repo.DeleteAsync(doc, TestContext.Current.CancellationToken)).Success);
        Assert.Null((await repo.GetByIdAsync(doc.Id, TestContext.Current.CancellationToken)).Data);
    }

    [Fact]
    public async Task GivenSeveralDocuments_WhenDeletingARangeOfIdsAsynchronously_ThenOnlyThoseAreRemoved()
    {
        var repo = NewRepo();
        var a = new TestDoc { Name = "a" };
        var b = new TestDoc { Name = "b" };
        await repo.CreateRangeAsync([a, b], TestContext.Current.CancellationToken);

        Assert.True((await repo.DeleteRangeAsync([a.Id, b.Id], TestContext.Current.CancellationToken)).Success);
        Assert.Empty((await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!);
    }
}
