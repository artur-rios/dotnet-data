using ArturRios.Data.MongoDb;
using MongoDB.Driver;

namespace ArturRios.Data.Tests.MongoDb;

[Trait("Category", "Functional")]
public class MongoContextTests
{
    private static MongoContext NewContext()
    {
        var database = new MongoClient("mongodb://localhost:27017").GetDatabase("testdb");
        return new MongoContext(database);
    }

    [Fact]
    public void GivenADocumentType_WhenGettingItsCollection_ThenTheConventionNameIsUsed()
    {
        var context = NewContext();
        var collection = context.GetCollection<Thing>();
        Assert.Equal("Thing", collection.CollectionNamespace.CollectionName);
    }

    [Fact]
    public void GivenANewContext_WhenInspectingTheSession_ThenItIsNullAndCanBeSet()
    {
        var context = NewContext();
        Assert.Null(context.Session);
    }

    private sealed class Thing : Document
    {
    }
}
