using System;
using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.Dapper;
using ArturRios.Data.Relational.Core.Repositories;
using ArturRios.Data.Relational.Core.Transactions;
using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Dapper;

public class DapperTransactionSharingTests
{
    [Fact]
    public async Task DapperRead_SeesUncommittedEfWrite_WithinUnitOfWorkTransaction()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var uow = new EfUnitOfWork(context);
        var dapper = new DapperSqlQuery(context);

        var seen = await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new TestEntity { Name = "inside-tx" });
            var rows = await dapper.QueryAsync<ItemRow>("SELECT Id, Name FROM Items");
            return rows.Data!.Count();
        });

        Assert.True(seen.Success);
        Assert.Equal(1, seen.Data); // Dapper saw the uncommitted EF insert via the shared connection+transaction
    }

    [Fact]
    public async Task Rollback_LeavesNothingVisibleToDapperAfterwards()
    {
        await using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var uow = new EfUnitOfWork(context);
        var dapper = new DapperSqlQuery(context);

        await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new TestEntity { Name = "doomed" });
            throw new InvalidOperationException("force rollback");
        });

        var after = await dapper.QueryAsync<ItemRow>("SELECT Id, Name FROM Items");
        Assert.True(after.Success);
        Assert.Empty(after.Data!);
    }

    private sealed record ItemRow(long Id, string Name);
}
