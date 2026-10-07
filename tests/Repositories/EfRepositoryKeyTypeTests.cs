using System;
using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Tests.Repositories;

[Trait("Category", "Functional")]
public class EfRepositoryKeyTypeTests
{
    [Fact]
    public void GivenAGuidKeyedEntity_WhenCreated_ThenAGeneratedGuidIdIsReturned()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<GuidKeyedTestEntity, Guid>(context);

        var result = repo.Create(new GuidKeyedTestEntity { Name = "a" });

        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.Data);
    }

    [Fact]
    public void GivenAGuidKeyedEntity_WhenFetchingById_ThenItComesBack()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<GuidKeyedTestEntity, Guid>(context);
        var id = repo.Create(new GuidKeyedTestEntity { Name = "a" }).Data;
        context.ChangeTracker.Clear();

        var result = repo.GetById(id);

        Assert.True(result.Success);
        Assert.Equal("a", result.Data!.Name);
    }

    [Fact]
    public async Task GivenAGuidKeyedEntity_WhenFetchingByIdAsync_ThenItComesBack()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<GuidKeyedTestEntity, Guid>(context);
        var id = (await repo.CreateAsync(new GuidKeyedTestEntity { Name = "a" })).Data;
        context.ChangeTracker.Clear();

        var result = await repo.GetByIdAsync(id);

        Assert.True(result.Success);
        Assert.Equal("a", result.Data!.Name);
    }

    [Fact]
    public void GivenNoMatchingGuid_WhenFetchingById_ThenASuccessfulNullComesBack()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<GuidKeyedTestEntity, Guid>(context);

        var result = repo.GetById(Guid.NewGuid());

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public void GivenGuidKeyedEntities_WhenDeletingARangeById_ThenOnlyThoseAreRemoved()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<GuidKeyedTestEntity, Guid>(context);
        var ids = repo.CreateRange([new GuidKeyedTestEntity { Name = "keep" }, new GuidKeyedTestEntity { Name = "drop" }])
            .Data!.ToList();

        var result = repo.DeleteRange([ids[1]]);

        Assert.True(result.Success);
        Assert.Equal([ids[1]], result.Data!);
        Assert.Equal("keep", Assert.Single(repo.GetAll().Data!).Name);
    }

    [Fact]
    public void GivenAStringKeyedEntity_WhenCreatedAndFetched_ThenTheAssignedIdRoundTrips()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<StringKeyedTestEntity, string>(context);

        var created = repo.Create(new StringKeyedTestEntity { Id = "sku-001", Name = "a" });
        context.ChangeTracker.Clear();
        var fetched = repo.GetById("sku-001");

        Assert.Equal("sku-001", created.Data);
        Assert.Equal("a", fetched.Data!.Name);
    }

    [Fact]
    public async Task GivenAStringKeyedEntity_WhenDeletedAsync_ThenItsIdIsReturnedAndItIsGone()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<StringKeyedTestEntity, string>(context);
        var entity = new StringKeyedTestEntity { Id = "sku-002", Name = "a" };
        await repo.CreateAsync(entity);

        var result = await repo.DeleteAsync(entity);

        Assert.Equal("sku-002", result.Data);
        Assert.Null((await repo.GetByIdAsync("sku-002")).Data);
    }

    [Fact]
    public void GivenAGuidKeyedVersionedEntity_WhenUpdated_ThenItsConcurrencyStampIsRegenerated()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<VersionedGuidKeyedTestEntity, Guid>(context);
        var entity = new VersionedGuidKeyedTestEntity { Name = "v1" };
        repo.Create(entity);
        var original = entity.ConcurrencyStamp;

        entity.Name = "v2";
        var result = repo.Update(entity);

        Assert.True(result.Success);
        Assert.NotEqual(original, entity.ConcurrencyStamp);
    }

    [Fact]
    public void GivenAStaleStampOnAGuidKeyedVersionedEntity_WhenUpdating_ThenAConcurrencyErrorIsReturned()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<VersionedGuidKeyedTestEntity, Guid>(context);
        var entity = new VersionedGuidKeyedTestEntity { Name = "v1" };
        repo.Create(entity);
        var staleStamp = entity.ConcurrencyStamp;
        context.Entry(entity).State = EntityState.Detached;

        var fresh = repo.GetById(entity.Id).Data!;
        fresh.Name = "v2";
        Assert.True(repo.Update(fresh).Success);
        context.Entry(fresh).State = EntityState.Detached;

        entity.ConcurrencyStamp = staleStamp;
        entity.Name = "late";
        var result = repo.Update(entity);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Concurrency conflict"));
    }
}
