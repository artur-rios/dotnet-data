using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Data.Tests.MongoDb.TestSupport;
using MongoDB.Driver;

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

        var result = await repo.UpdateAsync(new TestDoc { Id = MissingId, Name = "ghost" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.ConcurrencyMessage], result.Errors);
        Assert.Empty((await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!);
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
        await repo.CreateAsync(existing, TestContext.Current.CancellationToken);

        var result = await repo.UpdateRangeAsync([existing, new TestDoc { Id = MissingId, Name = "ghost" }], TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.ConcurrencyMessage], result.Errors);
    }

    [Fact]
    public async Task GivenAStaleVersionedDocument_WhenDeleted_ThenAConcurrencyErrorComesBackAndTheDocumentSurvives()
    {
        var context = fixture.NewContext();
        var repo = new MongoDocumentRepository<VersionedTestDoc>(context);
        var original = new VersionedTestDoc { Name = "a" };
        await repo.CreateAsync(original, TestContext.Current.CancellationToken);

        var fresh = (await repo.GetByIdAsync(original.Id, TestContext.Current.CancellationToken)).Data!;
        fresh.Name = "b";
        Assert.True((await repo.UpdateAsync(fresh, TestContext.Current.CancellationToken)).Success);

        var result = await repo.DeleteAsync(original, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.ConcurrencyMessage], result.Errors);
        Assert.NotNull((await repo.GetByIdAsync(original.Id, TestContext.Current.CancellationToken)).Data);
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
        await repo.CreateAsync(doc, TestContext.Current.CancellationToken);
        doc.Name = "b";
        await repo.UpdateAsync(doc, TestContext.Current.CancellationToken);

        var result = await repo.DeleteAsync(doc, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Null((await repo.GetByIdAsync(doc.Id, TestContext.Current.CancellationToken)).Data);
    }

    [Fact]
    public async Task GivenSomeIdsThatDoNotExist_WhenDeletingARange_ThenOnlyTheDeletedIdsComeBack()
    {
        var repo = NewRepo();
        var a = new TestDoc { Name = "a" };
        await repo.CreateAsync(a, TestContext.Current.CancellationToken);

        var result = await repo.DeleteRangeAsync([a.Id, MissingId], TestContext.Current.CancellationToken);

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

        var result = await repo.CreateRangeAsync([], TestContext.Current.CancellationToken);

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

        var result = await repo.GetByIdAsync("not-an-object-id", TestContext.Current.CancellationToken);

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

        var result = await repo.DeleteRangeAsync([MissingId, "not-an-object-id"], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GivenADocumentThatExists_WhenUpdatedWithoutChanges_ThenItSucceeds()
    {
        var repo = NewRepo();
        var doc = new TestDoc { Name = "a" };
        await repo.CreateAsync(doc, TestContext.Current.CancellationToken);

        var result = await repo.UpdateAsync(doc, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Single((await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!, d => d.Name == "a");
    }

    [Fact]
    public void GivenOnlyIdsThatAreNotObjectIds_WhenDeletingARangeSynchronously_ThenASuccessfulEmptyResultComesBack()
    {
        var repo = NewRepo();
        repo.Create(new TestDoc { Name = "a" });

        var result = repo.DeleteRange(["not-an-object-id", ""]);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
        Assert.Single(repo.GetAll().Data!);
    }

    [Fact]
    public async Task GivenOnlyIdsThatAreNotObjectIds_WhenDeletingARange_ThenASuccessfulEmptyResultComesBack()
    {
        var repo = NewRepo();
        await repo.CreateAsync(new TestDoc { Name = "a" }, TestContext.Current.CancellationToken);

        var result = await repo.DeleteRangeAsync(["not-an-object-id"], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
        Assert.Single((await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!);
    }

    [Fact]
    public void GivenSeveralExistingDocuments_WhenUpdatingTheRangeSynchronously_ThenEveryChangePersists()
    {
        var repo = NewRepo();
        var a = new TestDoc { Name = "a" };
        var b = new TestDoc { Name = "b" };
        repo.CreateRange([a, b]);
        a.Name = "a2";
        b.Name = "b2";

        var result = repo.UpdateRange([a, b]);

        Assert.True(result.Success);
        Assert.Equal([a, b], result.Data!);
        Assert.Equal(["a2", "b2"], repo.GetAll().Data!.Select(d => d.Name).Order());
    }

    [Fact]
    public async Task GivenSeveralExistingDocuments_WhenUpdatingTheRange_ThenEveryChangePersists()
    {
        var repo = NewRepo();
        var a = new TestDoc { Name = "a" };
        var b = new TestDoc { Name = "b" };
        await repo.CreateRangeAsync([a, b], TestContext.Current.CancellationToken);
        a.Name = "a2";
        b.Name = "b2";

        var result = await repo.UpdateRangeAsync([a, b], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal([a, b], result.Data!);
        Assert.Equal(["a2", "b2"], (await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!.Select(d => d.Name).Order());
    }

    [Fact]
    public void GivenOneOfSeveralDocumentsDoesNotExist_WhenUpdatingTheRangeSynchronously_ThenAConcurrencyErrorComesBack()
    {
        var repo = NewRepo();
        var existing = new TestDoc { Name = "a" };
        repo.Create(existing);

        var result = repo.UpdateRange([existing, new TestDoc { Id = MissingId, Name = "ghost" }]);

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.ConcurrencyMessage], result.Errors);
    }

    [Fact]
    public async Task GivenAStaleVersionedDocument_WhenUpdated_ThenAConcurrencyErrorComesBackAndItsVersionIsRestored()
    {
        var repo = new MongoDocumentRepository<VersionedTestDoc>(fixture.NewContext());
        var doc = new VersionedTestDoc { Name = "a" };
        await repo.CreateAsync(doc, TestContext.Current.CancellationToken);
        var fresh = (await repo.GetByIdAsync(doc.Id, TestContext.Current.CancellationToken)).Data!;
        Assert.True((await repo.UpdateAsync(fresh, TestContext.Current.CancellationToken)).Success);
        var heldVersion = doc.Version;

        var result = await repo.UpdateAsync(doc, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.ConcurrencyMessage], result.Errors);
        Assert.Equal(heldVersion, doc.Version);
    }

    [Fact]
    public void GivenAVersionedUpdateRejectedByAUniqueIndex_WhenUpdatingSynchronously_ThenAUniqueConflictComesBackAndItsVersionIsRestored()
    {
        var context = fixture.NewContext();
        UniqueNames(context);
        var repo = new MongoDocumentRepository<VersionedTestDoc>(context);
        repo.Create(new VersionedTestDoc { Name = "taken" });
        var doc = new VersionedTestDoc { Name = "free" };
        repo.Create(doc);
        var heldVersion = doc.Version;

        doc.Name = "taken";
        var result = repo.Update(doc);

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.UniqueViolationMessage], result.Errors);
        Assert.Equal(heldVersion, doc.Version);
        doc.Name = "renamed";
        Assert.True(repo.Update(doc).Success);
    }

    [Fact]
    public async Task GivenAVersionedUpdateRejectedByAUniqueIndex_WhenUpdating_ThenAUniqueConflictComesBackAndItsVersionIsRestored()
    {
        var context = fixture.NewContext();
        UniqueNames(context);
        var repo = new MongoDocumentRepository<VersionedTestDoc>(context);
        await repo.CreateAsync(new VersionedTestDoc { Name = "taken" }, TestContext.Current.CancellationToken);
        var doc = new VersionedTestDoc { Name = "free" };
        await repo.CreateAsync(doc, TestContext.Current.CancellationToken);
        var heldVersion = doc.Version;

        doc.Name = "taken";
        var result = await repo.UpdateAsync(doc, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.UniqueViolationMessage], result.Errors);
        Assert.Equal(heldVersion, doc.Version);
        doc.Name = "renamed";
        Assert.True((await repo.UpdateAsync(doc, TestContext.Current.CancellationToken)).Success);
    }

    [Fact]
    public async Task GivenARangeThatRepeatsAnId_WhenCreatingTheRange_ThenAUniqueConflictComesBack()
    {
        var repo = NewRepo();
        var id = MissingId;

        var result = await repo.CreateRangeAsync([new TestDoc { Id = id, Name = "a" }, new TestDoc { Id = id, Name = "b" }], TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal([MongoErrors.UniqueViolationMessage], result.Errors);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenReading_ThenCancellationPropagatesInsteadOfBeingEnveloped()
    {
        var repo = NewRepo();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.GetAllAsync(new CancellationToken(true)));
    }

    private static void UniqueNames(ArturRios.Data.MongoDb.MongoContext context) =>
        context.GetCollection<VersionedTestDoc>().Indexes.CreateOne(new CreateIndexModel<VersionedTestDoc>(
            Builders<VersionedTestDoc>.IndexKeys.Ascending(d => d.Name), new CreateIndexOptions { Unique = true }),
            cancellationToken: TestContext.Current.CancellationToken);
}
