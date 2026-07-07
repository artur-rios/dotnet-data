using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Repositories;

public class EfRepositoryAsyncTests
{
    [Fact]
    public async Task CreateAsync_PersistsAndReturnsId()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);

        var result = await repo.CreateAsync(new TestEntity { Name = "a" });

        Assert.True(result.Success);
        Assert.True(result.Data > 0);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsSuccessWithNull_WhenMissing()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);

        var result = await repo.GetByIdAsync(123);

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAll()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        await repo.CreateRangeAsync([new TestEntity { Name = "a" }, new TestEntity { Name = "b" }]);

        var result = await repo.GetAllAsync();

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count());
    }

    [Fact]
    public async Task UpdateAsync_And_DeleteAsync_Work()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var entity = new TestEntity { Name = "a" };
        await repo.CreateAsync(entity);

        entity.Name = "b";
        var updated = await repo.UpdateAsync(entity);
        Assert.True(updated.Success);

        var deleted = await repo.DeleteAsync(entity);
        Assert.True(deleted.Success);
        var after = await repo.GetByIdAsync(entity.Id);
        Assert.Null(after.Data);
    }

    [Fact]
    public async Task DeleteRangeAsync_RemovesByIds()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var a = new TestEntity { Name = "a" };
        var b = new TestEntity { Name = "b" };
        await repo.CreateRangeAsync([a, b]);

        var result = await repo.DeleteRangeAsync([a.Id, b.Id]);

        Assert.True(result.Success);
        var all = await repo.GetAllAsync();
        Assert.Empty(all.Data!);
    }
}
