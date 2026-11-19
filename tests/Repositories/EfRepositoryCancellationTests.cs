using System;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArturRios.Data.Tests.Repositories;

/// <summary>
///     Cancellation is not a data-access failure: it propagates instead of coming back as an error
///     envelope, and a cancelled write still leaves no pending change behind for the next save to replay.
/// </summary>
[Trait("Category", "Functional")]
public class EfRepositoryCancellationTests
{
    [Fact]
    public async Task GivenACancelledToken_WhenCreatingAsynchronously_ThenCancellationPropagatesAndThePendingChangeIsDiscarded()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repo.CreateAsync(new TestEntity { Name = "a" }, new CancellationToken(true)));

        Assert.Empty(context.ChangeTracker.Entries());
        Assert.True(repo.Create(new TestEntity { Name = "b" }).Success);
        Assert.Equal("b", Assert.Single(repo.GetAll().Data!).Name);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenReadingAsynchronously_ThenCancellationPropagates()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity, long>(context);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.GetAllAsync(new CancellationToken(true)));
    }

    [Fact]
    public void GivenASaveThatIsCancelled_WhenCreating_ThenCancellationPropagatesAndThePendingChangeIsDiscarded()
    {
        var cancelling = new CancellingSaveInterceptor();
        using var context = SqliteTestContextFactory.Create(cancelling);
        var repo = new EfRepository<TestEntity, long>(context);

        Assert.ThrowsAny<OperationCanceledException>(() => repo.Create(new TestEntity { Name = "a" }));

        Assert.Empty(context.ChangeTracker.Entries());
        cancelling.Enabled = false;
        Assert.True(repo.Create(new TestEntity { Name = "b" }).Success);
        Assert.Equal("b", Assert.Single(repo.GetAll().Data!).Name);
    }

    [Fact]
    public void GivenAReadThatIsCancelled_WhenFetchingAll_ThenCancellationPropagates()
    {
        using var context = SqliteTestContextFactory.Create(new CancellingCommandInterceptor());
        var repo = new EfRepository<TestEntity, long>(context);

        Assert.ThrowsAny<OperationCanceledException>(() => repo.GetAll());
    }

    // A synchronous save has no token, but an interceptor or a provider can still cancel it.
    private sealed class CancellingSaveInterceptor : SaveChangesInterceptor
    {
        public bool Enabled { get; set; } = true;

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
            InterceptionResult<int> result) =>
            Enabled ? throw new OperationCanceledException("cancelled by the test") : result;
    }

    private sealed class CancellingCommandInterceptor : DbCommandInterceptor
    {
        public override InterceptionResult<System.Data.Common.DbDataReader> ReaderExecuting(
            System.Data.Common.DbCommand command, CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result) =>
            throw new OperationCanceledException("cancelled by the test");
    }
}
