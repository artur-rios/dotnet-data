using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Tests.Repositories;

/// <summary>
///     Writes through a context that already tracks the entity: the usual request flow reads the row (which
///     tracks it), then updates or deletes an instance mapped from the request, or the tracked one itself.
/// </summary>
[Trait("Category", "Functional")]
public class EfRepositoryTrackedEntityTests
{
    private static async Task<TEntity?> StoredAsync<TEntity>(TestDbContext context, long id)
        where TEntity : class, ArturRios.Data.Relational.Core.Entities.IVersionedEntity =>
        await context.Set<TEntity>().AsNoTracking()
            .FirstOrDefaultAsync(e => EF.Property<long>(e, "Id") == id, TestContext.Current.CancellationToken);

    [Fact]
    public async Task GivenATrackedRow_WhenUpdatingANewInstanceWithItsKey_ThenTheUpdateIsSaved()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var id = (await repo.CreateAsync(new TestEntity { Name = "original" }, TestContext.Current.CancellationToken)).Data;
        context.ChangeTracker.Clear();
        _ = await repo.GetByIdAsync(id, TestContext.Current.CancellationToken);

        var result = await repo.UpdateAsync(new TestEntity { Id = id, Name = "from the request" },
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join(", ", result.Errors));
        var stored = await context.Items.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("from the request", stored.Name);
    }

    [Fact]
    public void GivenATrackedRow_WhenUpdatingANewInstanceWithItsKeySynchronously_ThenTheUpdateIsSaved()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var id = repo.Create(new TestEntity { Name = "original" }).Data;
        context.ChangeTracker.Clear();
        _ = repo.GetById(id);

        var result = repo.Update(new TestEntity { Id = id, Name = "from the request" });

        Assert.True(result.Success, string.Join(", ", result.Errors));
        Assert.Equal("from the request", context.Items.AsNoTracking().Single().Name);
    }

    [Fact]
    public async Task GivenTrackedRows_WhenUpdatingNewInstancesWithTheirKeys_ThenEveryUpdateIsSaved()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var ids = (await repo.CreateRangeAsync([new TestEntity { Name = "a" }, new TestEntity { Name = "b" }],
            TestContext.Current.CancellationToken)).Data!.ToArray();
        context.ChangeTracker.Clear();
        _ = await repo.GetAllAsync(TestContext.Current.CancellationToken);

        var result = await repo.UpdateRangeAsync(ids.Select(id => new TestEntity { Id = id, Name = $"new {id}" }),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join(", ", result.Errors));
        Assert.Equal(ids.Select(id => $"new {id}"),
            context.Items.AsNoTracking().OrderBy(e => e.Id).Select(e => e.Name).ToArray());
    }

    [Fact]
    public async Task GivenATrackedRow_WhenDeletingANewInstanceWithItsKey_ThenTheRowIsDeleted()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var id = (await repo.CreateAsync(new TestEntity { Name = "original" }, TestContext.Current.CancellationToken)).Data;
        context.ChangeTracker.Clear();
        _ = await repo.GetByIdAsync(id, TestContext.Current.CancellationToken);

        var result = await repo.DeleteAsync(new TestEntity { Id = id }, TestContext.Current.CancellationToken);

        Assert.True(result.Success, string.Join(", ", result.Errors));
        Assert.False(await context.Items.AsNoTracking().AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GivenATrackedVersionedRow_WhenUpdatingANewInstanceWithTheCurrentStamp_ThenTheReturnedStampIsTheStoredOne()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<VersionedTestEntity, long>(context);
        var created = new VersionedTestEntity { Name = "original" };
        await repo.CreateAsync(created, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var loaded = (await repo.GetByIdAsync(created.Id, TestContext.Current.CancellationToken)).Data!;

        var first = await repo.UpdateAsync(
            new VersionedTestEntity { Id = loaded.Id, Name = "first", ConcurrencyStamp = loaded.ConcurrencyStamp },
            TestContext.Current.CancellationToken);
        Assert.True(first.Success, string.Join(", ", first.Errors));
        Assert.Equal((await StoredAsync<VersionedTestEntity>(context, loaded.Id))!.ConcurrencyStamp,
            first.Data!.ConcurrencyStamp);

        first.Data.Name = "second";
        var second = await repo.UpdateAsync(first.Data, TestContext.Current.CancellationToken);

        Assert.True(second.Success, string.Join(", ", second.Errors));
        Assert.Equal("second", (await StoredAsync<VersionedTestEntity>(context, loaded.Id))!.Name);
    }

    [Fact]
    public async Task GivenATrackedVersionedRow_WhenUpdatingANewInstanceWithAStaleStamp_ThenAConcurrencyErrorIsReturned()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<VersionedTestEntity, long>(context);
        var created = new VersionedTestEntity { Name = "original" };
        await repo.CreateAsync(created, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        _ = await repo.GetByIdAsync(created.Id, TestContext.Current.CancellationToken);

        var result = await repo.UpdateAsync(
            new VersionedTestEntity { Id = created.Id, Name = "late write", ConcurrencyStamp = Guid.NewGuid() },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Concurrency conflict"));
        Assert.Equal("original", (await StoredAsync<VersionedTestEntity>(context, created.Id))!.Name);
    }

    [Fact]
    public async Task GivenTheTrackedVersionedInstance_WhenItsStampIsSetToAStaleOne_ThenTheUpdateIsAConcurrencyError()
    {
        // The stamp a client sent back with its edit, set on the instance the context loaded: the check must be
        // against it, or the edit overwrites a change the client never saw.
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<VersionedTestEntity, long>(context);
        var created = new VersionedTestEntity { Name = "original" };
        await repo.CreateAsync(created, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var tracked = (await repo.GetByIdAsync(created.Id, TestContext.Current.CancellationToken)).Data!;

        tracked.ConcurrencyStamp = Guid.NewGuid();
        tracked.Name = "late write";
        var result = await repo.UpdateAsync(tracked, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Concurrency conflict"));
        Assert.Equal("original", (await StoredAsync<VersionedTestEntity>(context, created.Id))!.Name);
    }

    [Fact]
    public async Task GivenATrackedVersionedRow_WhenDeletingANewInstanceWithAStaleStamp_ThenAConcurrencyErrorIsReturned()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<VersionedTestEntity, long>(context);
        var created = new VersionedTestEntity { Name = "original" };
        await repo.CreateAsync(created, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        _ = await repo.GetByIdAsync(created.Id, TestContext.Current.CancellationToken);

        var result = await repo.DeleteAsync(
            new VersionedTestEntity { Id = created.Id, ConcurrencyStamp = Guid.NewGuid() },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Concurrency conflict"));
        Assert.NotNull(await StoredAsync<VersionedTestEntity>(context, created.Id));
    }
}
