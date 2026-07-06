using System;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using ArturRios.Data.DynamoDb.Repositories;
using ArturRios.Data.Tests.DynamoDb.TestSupport;
using Xunit;

namespace ArturRios.Data.Tests.DynamoDb;

[DynamoDBTable("UnmappedItems")]
public class UnmappedItem
{
    [DynamoDBHashKey] public string Id { get; set; } = string.Empty;
}

[Collection(DynamoTestCollection.Name)]
public class DynamoRepositoryTests(DynamoLocalFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.CreateTableAsync("TestItems", "Category", "Sku");
        await fixture.CreateTableAsync("VersionedTestItems", "Id");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private DynamoRepository<TestItem> NewRepo() => new(fixture.CreateContext());
    private DynamoRepository<VersionedTestItem> NewVersionedRepo() => new(fixture.CreateContext());

    [Fact]
    public async Task Save_And_Load_RoundTrips_AndNullWhenMissing()
    {
        var repo = NewRepo();
        var item = new TestItem { Category = "books", Sku = "b1", Name = "A" };
        Assert.True((await repo.SaveAsync(item)).Success);

        var found = await repo.LoadAsync("books", "b1");
        Assert.True(found.Success);
        Assert.Equal("A", found.Data!.Name);

        var missing = await repo.LoadAsync("books", "nope");
        Assert.True(missing.Success);
        Assert.Null(missing.Data);
    }

    [Fact]
    public async Task Delete_RemovesItem_AndIsIdempotent()
    {
        var repo = NewRepo();
        var item = new TestItem { Category = "books", Sku = "d1", Name = "A" };
        await repo.SaveAsync(item);

        Assert.True((await repo.DeleteAsync(item)).Success);
        Assert.Null((await repo.LoadAsync("books", "d1")).Data);
        Assert.True((await repo.DeleteAsync(item)).Success); // deleting again is not an error
    }

    [Fact]
    public async Task VersionedSave_WithStaleVersion_ReturnsConcurrencyError()
    {
        var repo = NewVersionedRepo();
        var item = new VersionedTestItem { Id = Guid.NewGuid().ToString(), Name = "A" };
        await repo.SaveAsync(item);          // first save: item.Version is now set (SDK-managed)

        // Load a fresh copy and update it — this advances the stored version.
        var fresh = (await repo.LoadAsync(item.Id)).Data!;
        fresh.Name = "updated";
        Assert.True((await repo.SaveAsync(fresh)).Success);

        // The original 'item' still holds the pre-update version -> stale.
        item.Name = "late";
        var conflict = await repo.SaveAsync(item);

        Assert.False(conflict.Success);
        Assert.Contains(conflict.Errors, e => e.Contains("Concurrency conflict"));
    }

    [Fact]
    public async Task Save_OnMissingTable_ReturnsErrorEnvelope_DoesNotThrow()
    {
        // A repository for a type whose table was never created.
        var repo = new DynamoRepository<UnmappedItem>(fixture.CreateContext());
        var result = await repo.SaveAsync(new UnmappedItem { Id = "x" });
        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }
}
