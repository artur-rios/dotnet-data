using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using ArturRios.Data.DynamoDb.Repositories;
using ArturRios.Data.Tests.DynamoDb.TestSupport;
using Xunit;

namespace ArturRios.Data.Tests.DynamoDb;

[Collection(DynamoTestCollection.Name)]
public class DynamoQueryScanTests(DynamoLocalFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.CreateTableAsync("TestItems", "Category", "Sku");
    public Task DisposeAsync() => Task.CompletedTask;

    private DynamoRepository<TestItem> NewRepo() => new(fixture.CreateContext());

    [Fact]
    public async Task Query_ByPartitionKey_ReturnsItems()
    {
        var repo = NewRepo();
        await repo.SaveAsync(new TestItem { Category = "q", Sku = "a", Name = "A" });
        await repo.SaveAsync(new TestItem { Category = "q", Sku = "b", Name = "B" });
        await repo.SaveAsync(new TestItem { Category = "other", Sku = "c", Name = "C" });

        var result = await repo.QueryAsync("q");
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count());
    }

    [Fact]
    public async Task Query_WithSortCondition_Filters()
    {
        var repo = NewRepo();
        await repo.SaveAsync(new TestItem { Category = "q2", Sku = "a", Name = "A" });
        await repo.SaveAsync(new TestItem { Category = "q2", Sku = "z", Name = "Z" });

        var result = await repo.QueryAsync("q2", QueryOperator.BeginsWith, new object[] { "a" });
        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("A", result.Data!.Single().Name);
    }

    [Fact]
    public async Task Scan_WithCondition_ReturnsMatches()
    {
        var repo = NewRepo();
        await repo.SaveAsync(new TestItem { Category = "s", Sku = "a", Name = "keep" });
        await repo.SaveAsync(new TestItem { Category = "s", Sku = "b", Name = "drop" });

        var result = await repo.ScanAsync(new[] { new ScanCondition("Name", ScanOperator.Equal, "keep") });
        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }
}
