using ArturRios.Data.MongoDb;
using MongoDB.Driver;

namespace ArturRios.Data.Tests.MongoDb;

public class MongoContextTests
{
    private sealed class Thing : Document { }

    private static MongoContext NewContext()
    {
        var database = new MongoClient("mongodb://localhost:27017").GetDatabase("testdb");
        return new MongoContext(database);
    }

    [Fact]
    public void GetCollection_UsesConventionName()
    {
        var context = NewContext();
        var collection = context.GetCollection<Thing>();
        Assert.Equal("Thing", collection.CollectionNamespace.CollectionName);
    }

    [Fact]
    public void Session_IsNullByDefault_AndSettable()
    {
        var context = NewContext();
        Assert.Null(context.Session);
    }
}
