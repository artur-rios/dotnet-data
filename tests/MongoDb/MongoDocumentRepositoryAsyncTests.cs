using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Data.Tests.MongoDb.TestSupport;
using Xunit;

namespace ArturRios.Data.Tests.MongoDb;

[Collection(MongoTestCollection.Name)]
public class MongoDocumentRepositoryAsyncTests(MongoReplicaSetFixture fixture)
{
    private MongoDocumentRepository<TestDoc> NewRepo() => new(fixture.NewContext());

    [Fact]
    public async Task CreateAsync_And_GetByIdAsync()
    {
        var repo = NewRepo();
        var doc = new TestDoc { Name = "a" };
        var create = await repo.CreateAsync(doc);
        Assert.True(create.Success);

        var found = await repo.GetByIdAsync(create.Data!);
        Assert.True(found.Success);
        Assert.Equal("a", found.Data!.Name);

        var missing = await repo.GetByIdAsync("507f1f77bcf86cd799439011");
        Assert.True(missing.Success);
        Assert.Null(missing.Data);
    }

    [Fact]
    public async Task GetAllAsync_FindAsync_And_Ranges()
    {
        var repo = NewRepo();
        await repo.CreateRangeAsync([new TestDoc { Name = "keep" }, new TestDoc { Name = "drop" }]);

        Assert.Equal(2, (await repo.GetAllAsync()).Data!.Count());
        Assert.Single((await repo.FindAsync(d => d.Name == "keep")).Data!);
    }

    [Fact]
    public async Task UpdateAsync_And_DeleteAsync()
    {
        var repo = NewRepo();
        var doc = new TestDoc { Name = "a" };
        await repo.CreateAsync(doc);

        doc.Name = "b";
        Assert.True((await repo.UpdateAsync(doc)).Success);

        Assert.True((await repo.DeleteAsync(doc)).Success);
        Assert.Null((await repo.GetByIdAsync(doc.Id)).Data);
    }

    [Fact]
    public async Task DeleteRangeAsync_RemovesByIds()
    {
        var repo = NewRepo();
        var a = new TestDoc { Name = "a" };
        var b = new TestDoc { Name = "b" };
        await repo.CreateRangeAsync([a, b]);

        Assert.True((await repo.DeleteRangeAsync([a.Id, b.Id])).Success);
        Assert.Empty((await repo.GetAllAsync()).Data!);
    }
}
