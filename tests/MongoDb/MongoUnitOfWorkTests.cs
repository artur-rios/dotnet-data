using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Data.MongoDb.Transactions;
using ArturRios.Data.Tests.MongoDb.TestSupport;

namespace ArturRios.Data.Tests.MongoDb;

[Collection(MongoTestCollection.Name)]
[Trait("Category", "Functional")]
public class MongoUnitOfWorkTests(MongoReplicaSetFixture fixture)
{
    [Fact]
    public async Task GivenSeveralWritesInATransaction_WhenCommitting_ThenAllOfThemPersist()
    {
        var context = fixture.NewContext(out var client);
        var repo = new MongoDocumentRepository<TestDoc>(context);
        var uow = new MongoUnitOfWork(client, context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new TestDoc { Name = "a" });
            await repo.CreateAsync(new TestDoc { Name = "b" });
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, repo.GetAll().Data!.Count());
    }

    [Fact]
    public async Task GivenAnExceptionInsideATransaction_WhenItRollsBack_ThenNothingPersists()
    {
        var context = fixture.NewContext(out var client);
        var repo = new MongoDocumentRepository<TestDoc>(context);
        var uow = new MongoUnitOfWork(client, context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new TestDoc { Name = "doomed" });
            throw new InvalidOperationException("force rollback");
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Empty(repo.GetAll().Data!);
    }

    [Fact]
    public async Task GivenANestedTransaction_WhenItCompletes_ThenTheOuterAmbientSessionIsRestored()
    {
        var context = fixture.NewContext(out var client);
        var uow = new MongoUnitOfWork(client, context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            var outerSession = context.Session;
            Assert.NotNull(outerSession);

            // An inner unit of work must not leave the context session-less for the outer one.
            await new MongoUnitOfWork(client, context).ExecuteInTransactionAsync(() => Task.CompletedTask);

            Assert.Same(outerSession, context.Session);
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Null(context.Session);
    }

    [Fact]
    public async Task GivenAnExceptionInsideATransaction_WhenItRollsBack_ThenNoDriverTextReachesTheCaller()
    {
        var context = fixture.NewContext(out var client);
        var uow = new MongoUnitOfWork(client, context);

        var result = await uow.ExecuteInTransactionAsync(() =>
            throw new InvalidOperationException("connection to cluster0.internal:27017 refused"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(["A data-access error occurred."], result.Errors);
    }

    [Fact]
    public async Task GivenAnUncommittedWrite_WhenReadingInsideTheSameTransaction_ThenTheWriteIsVisible()
    {
        var context = fixture.NewContext(out var client);
        var repo = new MongoDocumentRepository<TestDoc>(context);
        var uow = new MongoUnitOfWork(client, context);

        var seen = await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new TestDoc { Name = "inside" });
            return (await repo.GetAllAsync()).Data!.Count();
        }, TestContext.Current.CancellationToken);

        Assert.True(seen.Success);
        Assert.Equal(1, seen.Data);
    }

    [Fact]
    public void GivenSeveralWritesInASynchronousTransaction_WhenCommitting_ThenAllOfThemPersist()
    {
        var context = fixture.NewContext(out var client);
        var repo = new MongoDocumentRepository<TestDoc>(context);
        var uow = new MongoUnitOfWork(client, context);

        var result = uow.ExecuteInTransaction(() =>
        {
            repo.Create(new TestDoc { Name = "a" });
            repo.Create(new TestDoc { Name = "b" });
        });

        Assert.True(result.Success);
        Assert.Null(context.Session);
        Assert.Equal(["a", "b"], repo.GetAll().Data!.Select(d => d.Name).Order());
    }

    [Fact]
    public void GivenAnExceptionInsideASynchronousTransaction_WhenItRollsBack_ThenNoWriteOfItPersists()
    {
        var context = fixture.NewContext(out var client);
        var plain = new MongoDocumentRepository<TestDoc>(context);
        var versioned = new MongoDocumentRepository<VersionedTestDoc>(context);
        var kept = new TestDoc { Name = "kept" };
        var removed = new TestDoc { Name = "removed" };
        plain.CreateRange([kept, removed]);
        var doc = new VersionedTestDoc { Name = "v1" };
        versioned.Create(doc);
        var uow = new MongoUnitOfWork(client, context);

        // Every session-aware write path enlists: a rollback undoes each of them.
        var result = uow.ExecuteInTransaction<int>(() =>
        {
            plain.Create(new TestDoc { Name = "created" });
            plain.CreateRange([new TestDoc { Name = "range1" }, new TestDoc { Name = "range2" }]);
            plain.DeleteRange([removed.Id]);
            plain.Delete(kept);
            versioned.Update(new VersionedTestDoc { Id = doc.Id, Name = "v2", Version = doc.Version });
            Assert.Empty(plain.Find(d => d.Name == "kept").Data!);
            throw new InvalidOperationException("force rollback");
        });

        Assert.False(result.Success);
        Assert.Null(context.Session);
        Assert.Equal(["kept", "removed"], plain.GetAll().Data!.Select(d => d.Name).Order());
        Assert.Equal("v1", versioned.GetById(doc.Id).Data!.Name);
    }

    [Fact]
    public void GivenSessionAwareWritesInASynchronousTransaction_WhenCommitting_ThenEachOfThemTakesEffect()
    {
        var context = fixture.NewContext(out var client);
        var plain = new MongoDocumentRepository<TestDoc>(context);
        var versioned = new MongoDocumentRepository<VersionedTestDoc>(context);
        var removed = new TestDoc { Name = "removed" };
        var deleted = new TestDoc { Name = "deleted" };
        plain.CreateRange([removed, deleted]);
        var doc = new VersionedTestDoc { Name = "v1" };
        versioned.Create(doc);
        var uow = new MongoUnitOfWork(client, context);

        var result = uow.ExecuteInTransaction(() =>
        {
            plain.CreateRange([new TestDoc { Name = "range" }]);
            plain.DeleteRange([removed.Id]);
            plain.Delete(deleted);
            doc.Name = "v2";
            return versioned.Update(doc).Success;
        });

        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Equal(["range"], plain.GetAll().Data!.Select(d => d.Name));
        Assert.Equal("v2", versioned.GetById(doc.Id).Data!.Name);
    }

    [Fact]
    public async Task GivenAnExceptionInsideATransaction_WhenItRollsBack_ThenNoSessionAwareWriteOfItPersists()
    {
        var context = fixture.NewContext(out var client);
        var plain = new MongoDocumentRepository<TestDoc>(context);
        var versioned = new MongoDocumentRepository<VersionedTestDoc>(context);
        var kept = new TestDoc { Name = "kept" };
        var removed = new TestDoc { Name = "removed" };
        await plain.CreateRangeAsync([kept, removed], TestContext.Current.CancellationToken);
        var doc = new VersionedTestDoc { Name = "v1" };
        await versioned.CreateAsync(doc, TestContext.Current.CancellationToken);
        var plainDoc = new TestDoc { Name = "plain" };
        await plain.CreateAsync(plainDoc, TestContext.Current.CancellationToken);
        var uow = new MongoUnitOfWork(client, context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            var ct = TestContext.Current.CancellationToken;
            await plain.CreateRangeAsync([new TestDoc { Name = "range" }], ct);
            await plain.DeleteRangeAsync([removed.Id], ct);
            await plain.DeleteAsync(kept, ct);
            await versioned.UpdateAsync(new VersionedTestDoc { Id = doc.Id, Name = "v2", Version = doc.Version }, ct);
            plainDoc.Name = "plain2";
            await plain.UpdateAsync(plainDoc, ct);
            throw new InvalidOperationException("force rollback");
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(["kept", "plain", "removed"],
            (await plain.GetAllAsync(TestContext.Current.CancellationToken)).Data!.Select(d => d.Name).Order());
        Assert.Equal("v1", (await versioned.GetByIdAsync(doc.Id, TestContext.Current.CancellationToken)).Data!.Name);
    }

    [Fact]
    public async Task GivenSessionAwareWritesInATransaction_WhenCommitting_ThenEachOfThemTakesEffect()
    {
        var context = fixture.NewContext(out var client);
        var plain = new MongoDocumentRepository<TestDoc>(context);
        var versioned = new MongoDocumentRepository<VersionedTestDoc>(context);
        var removed = new TestDoc { Name = "removed" };
        var deleted = new TestDoc { Name = "deleted" };
        await plain.CreateRangeAsync([removed, deleted], TestContext.Current.CancellationToken);
        var doc = new VersionedTestDoc { Name = "v1" };
        await versioned.CreateAsync(doc, TestContext.Current.CancellationToken);
        var uow = new MongoUnitOfWork(client, context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            var ct = TestContext.Current.CancellationToken;
            await plain.CreateRangeAsync([new TestDoc { Name = "range" }], ct);
            await plain.DeleteRangeAsync([removed.Id], ct);
            await plain.DeleteAsync(deleted, ct);
            doc.Name = "v2";
            await versioned.UpdateAsync(doc, ct);
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(["range"],
            (await plain.GetAllAsync(TestContext.Current.CancellationToken)).Data!.Select(d => d.Name));
        Assert.Equal("v2", (await versioned.GetByIdAsync(doc.Id, TestContext.Current.CancellationToken)).Data!.Name);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenAWriteInATransactionHonoursIt_ThenCancellationPropagatesAndNothingPersists()
    {
        var context = fixture.NewContext(out var client);
        var repo = new MongoDocumentRepository<TestDoc>(context);
        var uow = new MongoUnitOfWork(client, context);
        var cancelled = new CancellationToken(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            uow.ExecuteInTransactionAsync(() => repo.CreateAsync(new TestDoc { Name = "doomed" }, cancelled), cancelled));

        Assert.Null(context.Session);
        Assert.Empty((await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!);
    }

    [Fact]
    public void GivenWorkThatIsCancelled_WhenExecutingInASynchronousTransaction_ThenCancellationPropagatesAndNothingPersists()
    {
        var context = fixture.NewContext(out var client);
        var repo = new MongoDocumentRepository<TestDoc>(context);
        var uow = new MongoUnitOfWork(client, context);

        Assert.ThrowsAny<OperationCanceledException>(() => uow.ExecuteInTransaction(() =>
        {
            repo.Create(new TestDoc { Name = "doomed" });
            throw new OperationCanceledException();
        }));

        Assert.Null(context.Session);
        Assert.Empty(repo.GetAll().Data!);
    }
}
