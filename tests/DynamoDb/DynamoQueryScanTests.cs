using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using ArturRios.Data.DynamoDb.Repositories;
using ArturRios.Data.Tests.DynamoDb.TestSupport;

namespace ArturRios.Data.Tests.DynamoDb;

[Collection(DynamoTestCollection.Name)]
[Trait("Category", "Functional")]
public class DynamoQueryScanTests(DynamoLocalFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.CreateTableAsync("TestItems", "Category", "Sku");
    public Task DisposeAsync() => Task.CompletedTask;

    private DynamoRepository<TestItem> NewRepo() => new(fixture.CreateContext());

    [Fact]
    public async Task GivenItemsUnderOnePartitionKey_WhenQueryingByIt_ThenThoseItemsComeBack()
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
    public async Task GivenASortKeyCondition_WhenQuerying_ThenOnlyMatchingItemsComeBack()
    {
        var repo = NewRepo();
        await repo.SaveAsync(new TestItem { Category = "q2", Sku = "a", Name = "A" });
        await repo.SaveAsync(new TestItem { Category = "q2", Sku = "z", Name = "Z" });

        var result = await repo.QueryAsync("q2", QueryOperator.BeginsWith, ["a"]);
        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("A", result.Data!.Single().Name);
    }

    [Fact]
    public async Task GivenAScanCondition_WhenScanning_ThenOnlyMatchingItemsComeBack()
    {
        var repo = NewRepo();
        await repo.SaveAsync(new TestItem { Category = "s", Sku = "a", Name = "keep" });
        await repo.SaveAsync(new TestItem { Category = "s", Sku = "b", Name = "drop" });

        var result = await repo.ScanAsync([new ScanCondition("Name", ScanOperator.Equal, "keep")]);
        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }
}
