using System.Linq;
using ArturRios.Data.Relational.Core;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Repositories;

public class EfRepositoryTests
{
    private sealed class UnmappedEntity : Entity;

    [Fact]
    public void GetAll_OnUnmappedEntity_ReturnsErrorEnvelope_DoesNotThrow()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<UnmappedEntity>(context);

        var result = repo.GetAll();

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Create_PersistsAndReturnsId()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);

        var result = repo.Create(new TestEntity { Name = "a" });

        Assert.True(result.Success);
        Assert.True(result.Data > 0);
    }

    [Fact]
    public void GetById_ReturnsEntity_WhenExists()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var id = repo.Create(new TestEntity { Name = "a" }).Data;

        var result = repo.GetById(id);

        Assert.True(result.Success);
        Assert.Equal("a", result.Data!.Name);
    }

    [Fact]
    public void GetById_ReturnsSuccessWithNull_WhenMissing()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);

        var result = repo.GetById(999);

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public void GetAll_ReturnsAll()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        repo.CreateRange([new TestEntity { Name = "a" }, new TestEntity { Name = "b" }]);

        var result = repo.GetAll();

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count());
    }

    [Fact]
    public void Update_ChangesEntity()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var entity = new TestEntity { Name = "a" };
        repo.Create(entity);

        entity.Name = "b";
        var result = repo.Update(entity);

        Assert.True(result.Success);
        Assert.Equal("b", repo.GetById(entity.Id).Data!.Name);
    }

    [Fact]
    public void Delete_RemovesEntity()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var entity = new TestEntity { Name = "a" };
        repo.Create(entity);

        var result = repo.Delete(entity);

        Assert.True(result.Success);
        Assert.Null(repo.GetById(entity.Id).Data);
    }

    [Fact]
    public void DeleteRange_RemovesByIds()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var a = new TestEntity { Name = "a" };
        var b = new TestEntity { Name = "b" };
        repo.CreateRange([a, b]);

        var result = repo.DeleteRange([a.Id, b.Id]);

        Assert.True(result.Success);
        Assert.Empty(repo.GetAll().Data!);
    }

    [Fact]
    public void Query_ComposesLinq()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        repo.CreateRange([new TestEntity { Name = "keep" }, new TestEntity { Name = "drop" }]);

        var kept = repo.Query().Where(e => e.Name == "keep").ToList();

        Assert.Single(kept);
    }
}
