using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.DynamoDb.Repositories;
using ArturRios.Data.Tests.DynamoDb.TestSupport;

namespace ArturRios.Data.Tests.DynamoDb;

[Collection(DynamoTestCollection.Name)]
[Trait("Category", "Functional")]
public class DynamoBatchTests(DynamoLocalFixture fixture) : IAsyncLifetime
{
    // Hash-only table for batch-get by hash key.
    public async ValueTask InitializeAsync() => await fixture.CreateTableAsync("VersionedTestItems", "Id");
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private DynamoRepository<VersionedTestItem> NewRepo() => new(fixture.CreateContext());

    [Fact]
    public async Task GivenABatchOfItems_WhenSavingLoadingAndDeletingThemTogether_ThenEveryItemIsHandled()
    {
        var repo = NewRepo();
        var a = new VersionedTestItem { Id = "a", Name = "A" };
        var b = new VersionedTestItem { Id = "b", Name = "B" };

        Assert.True((await repo.SaveManyAsync([a, b], TestContext.Current.CancellationToken)).Success);

        var loaded = await repo.LoadManyAsync(["a", "b"], TestContext.Current.CancellationToken);
        Assert.True(loaded.Success);
        Assert.Equal(2, loaded.Data!.Count());

        Assert.True((await repo.DeleteManyAsync([a, b], TestContext.Current.CancellationToken)).Success);
        Assert.Empty((await repo.LoadManyAsync(["a", "b"], TestContext.Current.CancellationToken)).Data!);
    }
}
