using System.Collections.Generic;
using System.Linq;
using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Data.Tests.MongoDb.TestSupport;
using Xunit;

namespace ArturRios.Data.Tests.MongoDb;

[Collection(MongoTestCollection.Name)]
public class MongoDocumentRepositoryTests(MongoReplicaSetFixture fixture)
{
    private MongoDocumentRepository<TestDoc> NewRepo() => new(fixture.NewContext());
    private MongoDocumentRepository<VersionedTestDoc> NewVersionedRepo() => new(fixture.NewContext());

    [Fact]
    public void Create_AssignsAndReturnsId()
    {
        var repo = NewRepo();
        var result = repo.Create(new TestDoc { Name = "a" });
        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Data));
    }

    [Fact]
    public void GetById_RoundTrips_AndNullWhenMissing()
    {
        var repo = NewRepo();
        var doc = new TestDoc { Name = "a" };
        var id = repo.Create(doc).Data!;

        var found = repo.GetById(id);
        Assert.True(found.Success);
        Assert.Equal("a", found.Data!.Name);

        var missing = repo.GetById("507f1f77bcf86cd799439011");
        Assert.True(missing.Success);
        Assert.Null(missing.Data);
    }

    [Fact]
    public void GetAll_And_Find_And_Query()
    {
        var repo = NewRepo();
        repo.CreateRange([new TestDoc { Name = "keep" }, new TestDoc { Name = "drop" }]);

        Assert.Equal(2, repo.GetAll().Data!.Count());
        Assert.Single(repo.Find(d => d.Name == "keep").Data!);
        Assert.Single(repo.Query().Where(d => d.Name == "drop").ToList());
    }

    [Fact]
    public void Update_And_Delete()
    {
        var repo = NewRepo();
        var doc = new TestDoc { Name = "a" };
        repo.Create(doc);

        doc.Name = "b";
        Assert.True(repo.Update(doc).Success);
        Assert.Equal("b", repo.GetById(doc.Id).Data!.Name);

        Assert.True(repo.Delete(doc).Success);
        Assert.Null(repo.GetById(doc.Id).Data);
    }

    [Fact]
    public void DeleteRange_RemovesByIds()
    {
        var repo = NewRepo();
        var a = new TestDoc { Name = "a" };
        var b = new TestDoc { Name = "b" };
        repo.CreateRange([a, b]);

        var result = repo.DeleteRange([a.Id, b.Id]);
        Assert.True(result.Success);
        Assert.Empty(repo.GetAll().Data!);
    }

    [Fact]
    public void Create_DuplicateId_ReturnsErrorEnvelope_DoesNotThrow()
    {
        var repo = NewRepo();
        var first = new TestDoc { Id = "507f1f77bcf86cd799439099", Name = "a" };
        Assert.True(repo.Create(first).Success);

        var duplicate = new TestDoc { Id = "507f1f77bcf86cd799439099", Name = "b" };
        var result = repo.Create(duplicate); // duplicate _id -> write error, enveloped

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void VersionedUpdate_WithStaleVersion_ReturnsConcurrencyError()
    {
        var repo = NewVersionedRepo();
        var doc = new VersionedTestDoc { Name = "a" };
        repo.Create(doc);

        // Load a second copy and update it (advances stored Version).
        var fresh = repo.GetById(doc.Id).Data!;
        fresh.Name = "updated";
        Assert.True(repo.Update(fresh).Success);

        // The original 'doc' still holds the old Version -> stale.
        doc.Name = "late";
        var versionBeforeStaleUpdate = doc.Version;
        var stale = repo.Update(doc);
        Assert.False(stale.Success);
        Assert.Contains(stale.Errors, e => e.Contains("Concurrency conflict"));
        Assert.Equal(versionBeforeStaleUpdate, doc.Version); // in-memory version rolled back, not left bumped
    }
}
