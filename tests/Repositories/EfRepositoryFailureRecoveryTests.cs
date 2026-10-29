using System;
using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Data.Relational.Core.Transactions;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Tests.Repositories;

/// <summary>
///     A failed write comes back as an error envelope, so the caller keeps using the same scoped
///     context. These tests pin that the failure does not poison it: the next write must not replay the
///     rejected change, and a versioned entity must keep the stamp the database still holds.
/// </summary>
[Trait("Category", "Functional")]
public class EfRepositoryFailureRecoveryTests
{
    [Fact]
    public void GivenACreateThatViolatedAUniqueIndex_WhenCreatingAnotherEntity_ThenTheSecondCreateSucceeds()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<UniqueTestEntity, long>(context);
        repo.Create(new UniqueTestEntity { Email = "a@b.com" });
        Assert.False(repo.Create(new UniqueTestEntity { Email = "a@b.com" }).Success);

        var result = repo.Create(new UniqueTestEntity { Email = "c@d.com" });

        Assert.True(result.Success);
        Assert.Equal(2, repo.GetAll().Data!.Count());
    }

    [Fact]
    public async Task GivenACreateThatViolatedAUniqueIndexAsynchronously_WhenCreatingAnotherEntity_ThenTheSecondCreateSucceeds()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<UniqueTestEntity, long>(context);
        await repo.CreateAsync(new UniqueTestEntity { Email = "a@b.com" });
        Assert.False((await repo.CreateAsync(new UniqueTestEntity { Email = "a@b.com" })).Success);

        var result = await repo.CreateAsync(new UniqueTestEntity { Email = "c@d.com" });

        Assert.True(result.Success);
        Assert.Equal(2, (await repo.GetAllAsync()).Data!.Count());
    }

    [Fact]
    public void GivenADeleteOfAMissingEntityFailed_WhenCreatingAnotherEntity_ThenTheCreateSucceeds()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        Assert.False(repo.Delete(new TestEntity { Id = 999, Name = "ghost" }).Success);

        var result = repo.Create(new TestEntity { Name = "a" });

        Assert.True(result.Success);
    }

    [Fact]
    public void GivenAStaleUpdateFailed_WhenCreatingAnotherEntity_ThenTheCreateSucceeds()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<VersionedTestEntity, long>(context);
        var entity = new VersionedTestEntity { Name = "a" };
        repo.Create(entity);
        context.ChangeTracker.Clear();
        context.Database.ExecuteSqlRaw("UPDATE VersionedItems SET ConcurrencyStamp = {0}", Guid.NewGuid());
        var stale = new VersionedTestEntity
        {
            Id = entity.Id,
            Name = "stale",
            ConcurrencyStamp = entity.ConcurrencyStamp
        };
        Assert.False(repo.Update(stale).Success);

        var result = repo.Create(new VersionedTestEntity { Name = "b" });

        Assert.True(result.Success);
    }

    [Fact]
    public void GivenAnUpdateOfAVersionedEntityFailed_WhenTheSameInstanceIsRetried_ThenItKeepsTheStampTheDatabaseHolds()
    {
        using var context = SqliteTestContextFactory.Create();
        var versioned = new EfRepository<VersionedTestEntity, long>(context);
        var unique = new EfRepository<UniqueTestEntity, long>(context);
        var entity = new VersionedTestEntity { Name = "a" };
        versioned.Create(entity);
        var storedStamp = entity.ConcurrencyStamp;
        unique.Create(new UniqueTestEntity { Email = "a@b.com" });

        // The versioned change rides along with a save that fails for an unrelated reason.
        entity.Name = "b";
        Assert.False(unique.Create(new UniqueTestEntity { Email = "a@b.com" }).Success);

        Assert.Equal(storedStamp, entity.ConcurrencyStamp);
        Assert.True(versioned.Update(entity).Success);
    }

    [Fact]
    public void GivenAnOpenManualTransaction_WhenExecutingInATransaction_ThenAnErrorEnvelopeComesBack()
    {
        using var context = SqliteTestContextFactory.Create();
        var uow = new EfUnitOfWork(context);
        using var manual = uow.BeginTransaction();

        var result = uow.ExecuteInTransaction(() => { });

        Assert.False(result.Success);
        Assert.Equal([RelationalErrors.GenericMessage], result.Errors);
    }

    [Fact]
    public async Task GivenAnOpenManualTransaction_WhenExecutingInATransactionAsynchronously_ThenAnErrorEnvelopeComesBack()
    {
        await using var context = SqliteTestContextFactory.Create();
        var uow = new EfUnitOfWork(context);
        await using var manual = await uow.BeginTransactionAsync();

        var result = await uow.ExecuteInTransactionAsync(() => Task.CompletedTask);
        var typed = await uow.ExecuteInTransactionAsync(() => Task.FromResult(1));
        var typedSync = uow.ExecuteInTransaction(() => 1);

        Assert.False(result.Success);
        Assert.False(typed.Success);
        Assert.False(typedSync.Success);
    }

    [Fact]
    public void GivenATransactionRolledBack_WhenTheRolledBackEntityIsFetched_ThenItIsNotFound()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);
        var entity = new TestEntity { Name = "a" };

        var result = uow.ExecuteInTransaction(() =>
        {
            repo.Create(entity);
            throw new InvalidOperationException("boom");
        });

        Assert.False(result.Success);
        Assert.Null(repo.GetById(entity.Id).Data);
        Assert.Empty(context.ChangeTracker.Entries());
    }
}
