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
    public async ValueTask InitializeAsync() => await fixture.CreateTableAsync("TestItems", "Category", "Sku");
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private DynamoRepository<TestItem> NewRepo() => new(fixture.CreateContext());

    [Fact]
    public async Task GivenItemsUnderOnePartitionKey_WhenQueryingByIt_ThenThoseItemsComeBack()
    {
        var repo = NewRepo();
        await repo.SaveAsync(new TestItem { Category = "q", Sku = "a", Name = "A" }, TestContext.Current.CancellationToken);
        await repo.SaveAsync(new TestItem { Category = "q", Sku = "b", Name = "B" }, TestContext.Current.CancellationToken);
        await repo.SaveAsync(new TestItem { Category = "other", Sku = "c", Name = "C" }, TestContext.Current.CancellationToken);

        var result = await repo.QueryAsync("q", TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count());
    }

    [Fact]
    public async Task GivenASortKeyCondition_WhenQuerying_ThenOnlyMatchingItemsComeBack()
    {
        var repo = NewRepo();
        await repo.SaveAsync(new TestItem { Category = "q2", Sku = "a", Name = "A" }, TestContext.Current.CancellationToken);
        await repo.SaveAsync(new TestItem { Category = "q2", Sku = "z", Name = "Z" }, TestContext.Current.CancellationToken);

        var result = await repo.QueryAsync("q2", QueryOperator.BeginsWith, ["a"], TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("A", result.Data!.Single().Name);
    }

    [Fact]
    public async Task GivenAScanCondition_WhenScanning_ThenOnlyMatchingItemsComeBack()
    {
        var repo = NewRepo();
        await repo.SaveAsync(new TestItem { Category = "s", Sku = "a", Name = "keep" }, TestContext.Current.CancellationToken);
        await repo.SaveAsync(new TestItem { Category = "s", Sku = "b", Name = "drop" }, TestContext.Current.CancellationToken);

        var result = await repo.ScanAsync([new ScanCondition("Name", ScanOperator.Equal, "keep")], TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }
}
