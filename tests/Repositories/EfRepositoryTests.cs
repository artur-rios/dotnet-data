using System.Linq;
using ArturRios.Data.Relational.Core.Entities;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Tests.Repositories;

[Trait("Category", "Functional")]
public class EfRepositoryTests
{
    [Fact]
    public void GivenAnUnmappedEntity_WhenFetchingAll_ThenAnErrorEnvelopeComesBackWithoutThrowing()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<UnmappedEntity, long>(context);

        var result = repo.GetAll();

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void GivenANewEntity_WhenCreated_ThenItPersistsAndItsIdIsReturned()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);

        var result = repo.Create(new TestEntity { Name = "a" });

        Assert.True(result.Success);
        Assert.True(result.Data > 0);
    }

    [Fact]
    public void GivenAnExistingEntity_WhenFetchingById_ThenItComesBack()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var id = repo.Create(new TestEntity { Name = "a" }).Data;

        var result = repo.GetById(id);

        Assert.True(result.Success);
        Assert.Equal("a", result.Data!.Name);
    }

    [Fact]
    public void GivenNoMatchingEntity_WhenFetchingById_ThenASuccessfulNullComesBack()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);

        var result = repo.GetById(999);

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public void GivenSeveralEntities_WhenFetchingAll_ThenEveryOneComesBack()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        repo.CreateRange([new TestEntity { Name = "a" }, new TestEntity { Name = "b" }]);

        var result = repo.GetAll();

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count());
    }

    [Fact]
    public void GivenAnExistingEntity_WhenUpdated_ThenTheChangePersists()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var entity = new TestEntity { Name = "a" };
        repo.Create(entity);

        entity.Name = "b";
        var result = repo.Update(entity);

        Assert.True(result.Success);
        Assert.Equal("b", repo.GetById(entity.Id).Data!.Name);
    }

    [Fact]
    public void GivenAnExistingEntity_WhenDeleted_ThenItIsRemoved()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var entity = new TestEntity { Name = "a" };
        repo.Create(entity);

        var result = repo.Delete(entity);

        Assert.True(result.Success);
        Assert.Null(repo.GetById(entity.Id).Data);
    }

    [Fact]
    public void GivenSeveralEntities_WhenDeletingARangeOfIds_ThenOnlyThoseAreRemovedAndOnlyTheirIdsComeBack()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var a = new TestEntity { Name = "a" };
        var b = new TestEntity { Name = "b" };
        var c = new TestEntity { Name = "c" };
        repo.CreateRange([a, b, c]);

        var result = repo.DeleteRange([a.Id, c.Id, 999]);

        Assert.True(result.Success);
        Assert.Equal([a.Id, c.Id], result.Data!.Order());
        Assert.Equal("b", Assert.Single(repo.GetAll().Data!).Name);
    }

    [Fact]
    public void GivenSeveralExistingEntities_WhenUpdatingTheRange_ThenEveryChangePersists()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var a = new TestEntity { Name = "a" };
        var b = new TestEntity { Name = "b" };
        repo.CreateRange([a, b]);
        a.Name = "a2";
        b.Name = "b2";

        var result = repo.UpdateRange([a, b]);

        Assert.True(result.Success);
        Assert.Equal([a, b], result.Data!);
        context.ChangeTracker.Clear();
        Assert.Equal(["a2", "b2"], repo.GetAll().Data!.Select(e => e.Name).Order());
    }

    [Fact]
    public void GivenARangeWithAnEntityThatDoesNotExist_WhenUpdatingTheRange_ThenAConcurrencyErrorComesBackAndNothingChanges()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var a = new TestEntity { Name = "a" };
        repo.Create(a);
        a.Name = "a2";

        var result = repo.UpdateRange([a, new TestEntity { Id = 999, Name = "ghost" }]);

        Assert.False(result.Success);
        Assert.Equal([RelationalErrors.ConcurrencyMessage], result.Errors);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
        context.ChangeTracker.Clear();
        Assert.Equal("a", Assert.Single(repo.GetAll().Data!).Name);
    }

    [Fact]
    public void GivenTheQueryMethod_WhenComposingLinqOverIt_ThenTheCompositionIsHonoured()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        repo.CreateRange([new TestEntity { Name = "keep" }, new TestEntity { Name = "drop" }]);

        var kept = repo.Query().Where(e => e.Name == "keep").ToList();

        Assert.Single(kept);
    }

    [Fact]
    public void GivenADuplicateUniqueValue_WhenCreating_ThenAConflictComesBackWithoutTheConstraintText()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<UniqueTestEntity, long>(context);
        Assert.True(repo.Create(new UniqueTestEntity { Email = "a@b.com" }).Success);

        var result = repo.Create(new UniqueTestEntity { Email = "a@b.com" });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("same unique value already exists"));
        Assert.DoesNotContain(result.Errors, e =>
            e.Contains("IX_UniqueItems_Email") || e.Contains("Email") || e.Contains("a@b.com"));
    }

    [Fact]
    public void GivenAnUnmappedEntity_WhenFetchingAll_ThenNoProviderTextReachesTheCaller()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<UnmappedEntity, long>(context);

        var result = repo.GetAll();

        Assert.False(result.Success);
        Assert.All(result.Errors, e => Assert.DoesNotContain("UnmappedEntity", e));
        Assert.All(result.Errors, e => Assert.DoesNotContain("SQLite", e));
    }

    private sealed class UnmappedEntity : Entity<long>;
}
