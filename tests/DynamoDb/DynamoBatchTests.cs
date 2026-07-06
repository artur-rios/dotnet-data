using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.DynamoDb.Repositories;
using ArturRios.Data.Tests.DynamoDb.TestSupport;

namespace ArturRios.Data.Tests.DynamoDb;

[Collection(DynamoTestCollection.Name)]
public class DynamoBatchTests(DynamoLocalFixture fixture) : IAsyncLifetime
{
    // Hash-only table for batch-get by hash key.
    public Task InitializeAsync() => fixture.CreateTableAsync("VersionedTestItems", "Id");
    public Task DisposeAsync() => Task.CompletedTask;

    private DynamoRepository<VersionedTestItem> NewRepo() => new(fixture.CreateContext());

    [Fact]
    public async Task SaveMany_LoadMany_DeleteMany()
    {
        var repo = NewRepo();
        var a = new VersionedTestItem { Id = "a", Name = "A" };
        var b = new VersionedTestItem { Id = "b", Name = "B" };

        Assert.True((await repo.SaveManyAsync([a, b])).Success);

        var loaded = await repo.LoadManyAsync(["a", "b"]);
        Assert.True(loaded.Success);
        Assert.Equal(2, loaded.Data!.Count());

        Assert.True((await repo.DeleteManyAsync([a, b])).Success);
        Assert.Empty((await repo.LoadManyAsync(["a", "b"])).Data!);
    }
}
