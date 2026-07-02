using System;
using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.Repositories;
using ArturRios.Data.Tests.TestSupport;
using ArturRios.Data.Transactions;

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
    public async Task ExecuteInTransactionAsync_WithResult_CommitsAndReturnsData()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var uow = new EfUnitOfWork(context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            var created = await repo.CreateAsync(new TestEntity { Name = "a" });
            return created.Data;
        });

        Assert.True(result.Success);
        Assert.True(result.Data > 0);
        Assert.Single(repo.GetAll().Data!);
    }
}
