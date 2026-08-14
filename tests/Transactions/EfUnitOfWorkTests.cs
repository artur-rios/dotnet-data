using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Data.Relational.Core.Transactions;
using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Transactions;

public class EfUnitOfWorkTests
{
    [Fact]
    public void ExecuteInTransaction_CommitsOnSuccess()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
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
    public void ExecuteInTransaction_RollsBackOnException()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
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
    public void ExecuteInTransaction_OnUniqueViolation_ClassifiesWithoutLeakingConstraintText()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<UniqueTestEntity>(context);
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

    [Fact]
    public async Task ExecuteInTransactionAsync_OnCancellation_PropagatesInsteadOfEnveloping()
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
    public async Task ExecuteInTransactionAsync_WithResult_CommitsAndReturnsData()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var uow = new EfUnitOfWork(context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            var created = await repo.CreateAsync(new TestEntity { Name = "a" });
            return created.Data;
        });

        Assert.True(result.Success);
        Assert.True(result.Data > 0);
        Assert.Single((await repo.GetAllAsync()).Data!);
    }
}
