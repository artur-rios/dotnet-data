using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Data.Relational.Core.Transactions;
using ArturRios.Data.Tests.TestSupport;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Transactions;

[Trait("Category", "Functional")]
public class EfUnitOfWorkTests
{
    [Fact]
    public void GivenWorkThatSucceeds_WhenExecutedInATransaction_ThenItCommits()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);

        var result = uow.ExecuteInTransaction(() =>
        {
            repo.Create(new TestEntity { Name = "a" });
            repo.Create(new TestEntity { Name = "b" });
        });

        Assert.True(result.Success);
        Assert.Equal(2, repo.GetAll().Data!.Count());
    }

    [Fact]
    public void GivenWorkThatThrows_WhenExecutedInATransaction_ThenItRollsBack()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);

        var result = uow.ExecuteInTransaction(() =>
        {
            repo.Create(new TestEntity { Name = "a" });
            throw new InvalidOperationException("boom");
        });

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(repo.GetAll().Data!);
    }

    [Fact]
    public void GivenAUniqueViolationInsideATransaction_WhenItIsClassified_ThenNoConstraintTextReachesTheCaller()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<UniqueTestEntity, long>(context);
        var uow = new EfUnitOfWork(context);
        repo.Create(new UniqueTestEntity { Email = "a@b.com" });

        // The work delegate saves directly, so the provider exception reaches the unit of work.
        var result = uow.ExecuteInTransaction(() =>
        {
            context.UniqueItems.Add(new UniqueTestEntity { Email = "a@b.com" });
            context.SaveChanges();
        });

        Assert.False(result.Success);
        Assert.Equal(["Conflict: a record with the same unique value already exists."], result.Errors);
    }

    // A repository reports a failed write on its envelope rather than throwing, and the unit of work rolls back
    // only when the work throws: the documented examples check each write's envelope and throw on a failure.
    [Fact]
    public async Task GivenAFailedRepositoryWriteWhoseEnvelopeIsIgnored_WhenExecutedInATransaction_ThenTheEarlierWriteCommits()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<UniqueTestEntity, long>(context);
        var uow = new EfUnitOfWork(context);
        await repo.CreateAsync(new UniqueTestEntity { Email = "taken@b.com" }, TestContext.Current.CancellationToken);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new UniqueTestEntity { Email = "new@b.com" }, TestContext.Current.CancellationToken);
            await repo.CreateAsync(new UniqueTestEntity { Email = "taken@b.com" }, TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Contains(context.UniqueItems, item => item.Email == "new@b.com");
    }

    [Fact]
    public async Task GivenAFailedRepositoryWriteTheWorkThrowsOn_WhenExecutedInATransaction_ThenNothingCommits()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<UniqueTestEntity, long>(context);
        var uow = new EfUnitOfWork(context);
        await repo.CreateAsync(new UniqueTestEntity { Email = "taken@b.com" }, TestContext.Current.CancellationToken);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            var first = await repo.CreateAsync(new UniqueTestEntity { Email = "new@b.com" }, TestContext.Current.CancellationToken);
            var second = await repo.CreateAsync(new UniqueTestEntity { Email = "taken@b.com" }, TestContext.Current.CancellationToken);

            if (!first.Success || !second.Success)
            {
                throw new InvalidOperationException(string.Join(", ", first.Errors.Concat(second.Errors)));
            }

            return first.Data;
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.DoesNotContain(context.UniqueItems, item => item.Email == "new@b.com");
        Assert.Single(context.UniqueItems);
    }

    [Fact]
    public async Task GivenAFailedRepositoryWrite_WhenTheTransactionIsLeftUncommitted_ThenNothingCommitsAndItsEnvelopeIsKept()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<UniqueTestEntity, long>(context);
        var uow = new EfUnitOfWork(context);
        await repo.CreateAsync(new UniqueTestEntity { Email = "taken@b.com" }, TestContext.Current.CancellationToken);

        DataOutput<long> result;
        await using (var transaction = await uow.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            var first = await repo.CreateAsync(new UniqueTestEntity { Email = "new@b.com" }, TestContext.Current.CancellationToken);
            var second = await repo.CreateAsync(new UniqueTestEntity { Email = "taken@b.com" }, TestContext.Current.CancellationToken);

            if (first.Success && second.Success)
            {
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            result = second.Success ? first : second;
        }

        context.ChangeTracker.Clear();
        Assert.False(result.Success);
        Assert.Equal(["Conflict: a record with the same unique value already exists."], result.Errors);
        Assert.DoesNotContain(context.UniqueItems, item => item.Email == "new@b.com");
    }

    [Fact]
    public async Task GivenACancelledToken_WhenExecutingInATransactionAsynchronously_ThenCancellationPropagatesInsteadOfBeingEnveloped()
    {
        await using var context = SqliteTestContextFactory.Create();
        var uow = new EfUnitOfWork(context);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            uow.ExecuteInTransactionAsync(() =>
            {
                cts.Cancel();
                cts.Token.ThrowIfCancellationRequested();

                return Task.CompletedTask;
            }, cts.Token));
    }

    [Fact]
    public async Task GivenAnAlreadyCancelledToken_WhenWorkThatIgnoresItRunsInATransaction_ThenTheWorkNeverRunsAndNothingPersists()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);
        var workRan = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            uow.ExecuteInTransactionAsync(async () =>
            {
                workRan = true;
                await repo.CreateAsync(new TestEntity { Name = "doomed" }, CancellationToken.None);
            }, new CancellationToken(true)));

        Assert.False(workRan);
        Assert.Null(context.Database.CurrentTransaction);
        Assert.Empty((await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!);
    }

    [Fact]
    public async Task GivenATokenCancelledDuringWorkThatIgnoresIt_WhenTheTransactionEnds_ThenItRollsBackAndNothingPersists()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            uow.ExecuteInTransactionAsync(async () =>
            {
                await repo.CreateAsync(new TestEntity { Name = "doomed" }, CancellationToken.None);
                await cts.CancelAsync();
            }, cts.Token));

        Assert.Null(context.Database.CurrentTransaction);
        Assert.Empty((await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!);
    }

    [Fact]
    public async Task GivenWorkThatReturnsAResult_WhenExecutedInATransactionAsynchronously_ThenItCommitsAndTheResultComesBack()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            var created = await repo.CreateAsync(new TestEntity { Name = "a" });
            return created.Data;
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(result.Data > 0);
        Assert.Single((await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!);
    }

    [Fact]
    public async Task GivenWorkThatSucceeds_WhenExecutedInATransactionAsynchronously_ThenItCommits()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new TestEntity { Name = "a" }, TestContext.Current.CancellationToken);
            await repo.CreateAsync(new TestEntity { Name = "b" }, TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Null(context.Database.CurrentTransaction);
        Assert.Equal(2, (await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!.Count());
    }

    [Fact]
    public async Task GivenWorkThatThrows_WhenExecutedInATransactionAsynchronously_ThenItRollsBackAndTheTrackerIsCleared()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);

        var result = await uow.ExecuteInTransactionAsync<long>(async () =>
        {
            await repo.CreateAsync(new TestEntity { Name = "a" }, TestContext.Current.CancellationToken);
            throw new InvalidOperationException("boom");
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal([RelationalErrors.GenericMessage], result.Errors);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Empty((await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!);
    }

    [Fact]
    public void GivenWorkThatReturnsAResult_WhenExecutedInATransaction_ThenItCommitsAndTheResultComesBack()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);

        var result = uow.ExecuteInTransaction(() => repo.Create(new TestEntity { Name = "a" }).Data);

        Assert.True(result.Success);
        Assert.Equal(Assert.Single(repo.GetAll().Data!).Id, result.Data);
    }

    [Fact]
    public void GivenWorkThatIsCancelled_WhenExecutedInATransaction_ThenCancellationPropagatesAndNothingPersists()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);

        Assert.ThrowsAny<OperationCanceledException>(() => uow.ExecuteInTransaction(() =>
        {
            repo.Create(new TestEntity { Name = "a" });
            throw new OperationCanceledException();
        }));

        Assert.Null(context.Database.CurrentTransaction);
        Assert.Empty(repo.GetAll().Data!);
    }

    [Fact]
    public void GivenWorkThatEndsTheTransactionItselfAndThenThrows_WhenExecutedInATransaction_ThenTheFailedRollbackIsSwallowed()
    {
        using var context = SqliteTestContextFactory.Create();
        var uow = new EfUnitOfWork(context);

        var result = uow.ExecuteInTransaction(() =>
        {
            context.Database.CurrentTransaction!.Commit();
            throw new InvalidOperationException("boom");
        });

        Assert.False(result.Success);
        Assert.Equal([RelationalErrors.GenericMessage], result.Errors);
    }

    [Fact]
    public async Task GivenWorkThatEndsTheTransactionItselfAndThenThrows_WhenExecutedInATransactionAsynchronously_ThenTheFailedRollbackIsSwallowed()
    {
        await using var context = SqliteTestContextFactory.Create();
        var uow = new EfUnitOfWork(context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            await context.Database.CurrentTransaction!.CommitAsync(TestContext.Current.CancellationToken);
            throw new InvalidOperationException("boom");
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal([RelationalErrors.GenericMessage], result.Errors);
    }

    [Fact]
    public void GivenAManualTransaction_WhenCommitted_ThenItsWritesPersist()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);

        using (var tx = uow.BeginTransaction())
        {
            repo.Create(new TestEntity { Name = "a" });
            tx.Commit();
        }

        context.ChangeTracker.Clear();
        Assert.Single(repo.GetAll().Data!);
    }

    [Fact]
    public void GivenAManualTransaction_WhenRolledBack_ThenItsWritesAreGone()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);

        using (var tx = uow.BeginTransaction())
        {
            repo.Create(new TestEntity { Name = "a" });
            tx.Rollback();
        }

        context.ChangeTracker.Clear();
        Assert.Empty(repo.GetAll().Data!);
    }

    [Fact]
    public async Task GivenAManualTransactionAsynchronously_WhenCommitted_ThenItsWritesPersist()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);

        await using (var tx = await uow.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await repo.CreateAsync(new TestEntity { Name = "a" }, TestContext.Current.CancellationToken);
            await tx.CommitAsync(TestContext.Current.CancellationToken);
        }

        context.ChangeTracker.Clear();
        Assert.Single((await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!);
    }

    [Fact]
    public async Task GivenAManualTransactionAsynchronously_WhenRolledBack_ThenItsWritesAreGone()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);

        await using (var tx = await uow.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await repo.CreateAsync(new TestEntity { Name = "a" }, TestContext.Current.CancellationToken);
            await tx.RollbackAsync(TestContext.Current.CancellationToken);
        }

        context.ChangeTracker.Clear();
        Assert.Empty((await repo.GetAllAsync(TestContext.Current.CancellationToken)).Data!);
    }

    [Fact]
    public void GivenAManualTransactionThatIsNeverCompleted_WhenDisposed_ThenItsWritesAreGone()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);
        var uow = new EfUnitOfWork(context);

        using (uow.BeginTransaction())
        {
            repo.Create(new TestEntity { Name = "a" });
        }

        context.ChangeTracker.Clear();
        Assert.Empty(repo.GetAll().Data!);
    }
}
