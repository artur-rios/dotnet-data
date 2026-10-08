using System.Threading.Tasks;
using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Data.Tests.MongoDb.TestSupport;

namespace ArturRios.Data.Tests.MongoDb;

/// <summary>
///     Pins the repository semantics the Mongo provider shares with the relational one: a write that
///     targets a missing or stale document is a concurrency error, range operations report what they
///     actually did, and an id that cannot exist reads as not-found.
/// </summary>
[Collection(MongoTestCollection.Name)]
[Trait("Category", "Functional")]
public class MongoDocumentRepositorySemanticsTests(MongoReplicaSetFixture fixture)
{
    private const string MissingId = "507f1f77bcf86cd799439011";

    private MongoDocumentRepository<TestDoc> NewRepo() => new(fixture.NewContext());

    [Fact]
    public async Task GivenADocumentThatDoesNotExist_WhenUpdated_ThenAConcurrencyErrorComesBack()
    {
        var repo = NewRepo();

        var result = await repo.UpdateAsync(new TestDoc { Id = MissingId, Name = "ghost" });

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.ConcurrencyMessage], result.Errors);
        Assert.Empty((await repo.GetAllAsync()).Data!);
    }

    [Fact]
    public void GivenADocumentThatDoesNotExist_WhenUpdatedSynchronously_ThenAConcurrencyErrorComesBack()
    {
        var repo = NewRepo();

        var result = repo.Update(new TestDoc { Id = MissingId, Name = "ghost" });

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.ConcurrencyMessage], result.Errors);
    }

    [Fact]
    public async Task GivenOneOfSeveralDocumentsDoesNotExist_WhenUpdatingTheRange_ThenAConcurrencyErrorComesBack()
    {
        var repo = NewRepo();
        var existing = new TestDoc { Name = "a" };
        await repo.CreateAsync(existing);

        var result = await repo.UpdateRangeAsync([existing, new TestDoc { Id = MissingId, Name = "ghost" }]);

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.ConcurrencyMessage], result.Errors);
    }

    [Fact]
    public async Task GivenAStaleVersionedDocument_WhenDeleted_ThenAConcurrencyErrorComesBackAndTheDocumentSurvives()
    {
        var context = fixture.NewContext();
        var repo = new MongoDocumentRepository<VersionedTestDoc>(context);
        var original = new VersionedTestDoc { Name = "a" };
        await repo.CreateAsync(original);

        var fresh = (await repo.GetByIdAsync(original.Id)).Data!;
        fresh.Name = "b";
        Assert.True((await repo.UpdateAsync(fresh)).Success);

        var result = await repo.DeleteAsync(original);

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.ConcurrencyMessage], result.Errors);
        Assert.NotNull((await repo.GetByIdAsync(original.Id)).Data);
    }

    [Fact]
    public void GivenAStaleVersionedDocument_WhenDeletedSynchronously_ThenAConcurrencyErrorComesBack()
    {
        var repo = new MongoDocumentRepository<VersionedTestDoc>(fixture.NewContext());
        var original = new VersionedTestDoc { Name = "a" };
        repo.Create(original);

        var fresh = repo.GetById(original.Id).Data!;
        Assert.True(repo.Update(fresh).Success);

        var result = repo.Delete(original);

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.ConcurrencyMessage], result.Errors);
        Assert.NotNull(repo.GetById(original.Id).Data);
    }

    [Fact]
    public async Task GivenACurrentVersionedDocument_WhenDeleted_ThenItIsRemoved()
    {
        var repo = new MongoDocumentRepository<VersionedTestDoc>(fixture.NewContext());
        var doc = new VersionedTestDoc { Name = "a" };
        await repo.CreateAsync(doc);
        doc.Name = "b";
        await repo.UpdateAsync(doc);

        var result = await repo.DeleteAsync(doc);

        Assert.True(result.Success);
        Assert.Null((await repo.GetByIdAsync(doc.Id)).Data);
    }

    [Fact]
    public async Task GivenSomeIdsThatDoNotExist_WhenDeletingARange_ThenOnlyTheDeletedIdsComeBack()
    {
        var repo = NewRepo();
        var a = new TestDoc { Name = "a" };
        await repo.CreateAsync(a);

        var result = await repo.DeleteRangeAsync([a.Id, MissingId]);

        Assert.True(result.Success);
        Assert.Equal([a.Id], result.Data!);
    }

    [Fact]
    public void GivenSomeIdsThatDoNotExist_WhenDeletingARangeSynchronously_ThenOnlyTheDeletedIdsComeBack()
    {
        var repo = NewRepo();
        var a = new TestDoc { Name = "a" };
        repo.Create(a);

        var result = repo.DeleteRange([a.Id, MissingId, "not-an-object-id"]);

        Assert.True(result.Success);
        Assert.Equal([a.Id], result.Data!);
    }

    [Fact]
    public async Task GivenNoDocuments_WhenCreatingAnEmptyRange_ThenASuccessfulEmptyResultComesBack()
    {
        var repo = NewRepo();

        var result = await repo.CreateRangeAsync([]);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public void GivenNoDocuments_WhenCreatingAnEmptyRangeSynchronously_ThenASuccessfulEmptyResultComesBack()
    {
        var repo = NewRepo();

        var result = repo.CreateRange([]);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GivenAnIdThatIsNotAnObjectId_WhenFetchingById_ThenASuccessfulNullComesBack()
    {
        var repo = NewRepo();

        var result = await repo.GetByIdAsync("not-an-object-id");

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public void GivenAnIdThatIsNotAnObjectId_WhenFetchingByIdSynchronously_ThenASuccessfulNullComesBack()
    {
        var repo = NewRepo();

        var result = repo.GetById("not-an-object-id");

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GivenOnlyIdsThatDoNotExist_WhenDeletingARange_ThenASuccessfulEmptyResultComesBack()
    {
        var repo = NewRepo();

        var result = await repo.DeleteRangeAsync([MissingId, "not-an-object-id"]);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GivenADocumentThatExists_WhenUpdatedWithoutChanges_ThenItSucceeds()
    {
        var repo = NewRepo();
        var doc = new TestDoc { Name = "a" };
        await repo.CreateAsync(doc);

        var result = await repo.UpdateAsync(doc);

        Assert.True(result.Success);
        Assert.Single((await repo.GetAllAsync()).Data!, d => d.Name == "a");
    }
}
