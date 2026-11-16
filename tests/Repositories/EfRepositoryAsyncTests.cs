using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Repositories;

[Trait("Category", "Functional")]
public class EfRepositoryAsyncTests
{
    [Fact]
    public async Task GivenANewEntity_WhenCreatedAsynchronously_ThenItPersistsAndItsIdIsReturned()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);

        var result = await repo.CreateAsync(new TestEntity { Name = "a" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(result.Data > 0);
    }

    [Fact]
    public async Task GivenNoMatchingEntity_WhenFetchingByIdAsynchronously_ThenASuccessfulNullComesBack()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);

        var result = await repo.GetByIdAsync(123, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GivenSeveralEntities_WhenFetchingAllAsynchronously_ThenEveryOneComesBack()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        await repo.CreateRangeAsync([new TestEntity { Name = "a" }, new TestEntity { Name = "b" }], TestContext.Current.CancellationToken);

        var result = await repo.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count());
    }

    [Fact]
    public async Task GivenAnEntity_WhenUpdatedAndDeletedAsynchronously_ThenBothTakeEffect()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var entity = new TestEntity { Name = "a" };
        await repo.CreateAsync(entity, TestContext.Current.CancellationToken);

        entity.Name = "b";
        var updated = await repo.UpdateAsync(entity, TestContext.Current.CancellationToken);
        Assert.True(updated.Success);

        var deleted = await repo.DeleteAsync(entity, TestContext.Current.CancellationToken);
        Assert.True(deleted.Success);
        var after = await repo.GetByIdAsync(entity.Id, TestContext.Current.CancellationToken);
        Assert.Null(after.Data);
    }

    [Fact]
    public async Task GivenSeveralEntities_WhenDeletingARangeOfIdsAsynchronously_ThenOnlyThoseAreRemovedAndOnlyTheirIdsComeBack()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var a = new TestEntity { Name = "a" };
        var b = new TestEntity { Name = "b" };
        var c = new TestEntity { Name = "c" };
        await repo.CreateRangeAsync([a, b, c], TestContext.Current.CancellationToken);

        var result = await repo.DeleteRangeAsync([a.Id, c.Id, 999], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal([a.Id, c.Id], result.Data!.Order());
        Assert.Equal("b", Assert.Single((await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!).Name);
    }

    [Fact]
    public async Task GivenSeveralExistingEntities_WhenUpdatingTheRangeAsynchronously_ThenEveryChangePersists()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var a = new TestEntity { Name = "a" };
        var b = new TestEntity { Name = "b" };
        await repo.CreateRangeAsync([a, b], TestContext.Current.CancellationToken);
        a.Name = "a2";
        b.Name = "b2";

        var result = await repo.UpdateRangeAsync([a, b], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal([a, b], result.Data!);
        context.ChangeTracker.Clear();
        Assert.Equal(["a2", "b2"], (await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!.Select(e => e.Name).Order());
    }

    [Fact]
    public async Task GivenAnEntityThatDoesNotExist_WhenUpdatingAsynchronously_ThenAConcurrencyErrorComesBackAndTheChangeIsDiscarded()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);

        var result = await repo.UpdateAsync(new TestEntity { Id = 999, Name = "ghost" }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal([RelationalErrors.ConcurrencyMessage], result.Errors);
        Assert.Empty(context.ChangeTracker.Entries());
    }
}
