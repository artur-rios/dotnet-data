using ArturRios.Data.MongoDb;
using ArturRios.Data.Tests.TestSupport;
using MongoDB.Driver;

namespace ArturRios.Data.Tests.MongoDb;

[Trait("Category", "Unit")]
public class MongoContextTests
{
    [Fact]
    public void GivenADocumentType_WhenGettingItsCollection_ThenTheConventionNameIsUsed()
    {
        string? requested = null;
        var database = InterfaceStub.For<IMongoDatabase>((method, args) =>
        {
            if (method.Name == nameof(IMongoDatabase.GetCollection))
            {
                requested = (string)args![0]!;
            }

            return (false, null);
        });

        new MongoContext(database).GetCollection<Thing>();

        Assert.Equal("Thing", requested);
    }

    [Fact]
    public void GivenANewContext_WhenInspectingTheSession_ThenItIsNullAndCanBeSet()
    {
        var context = new MongoContext(InterfaceStub.For<IMongoDatabase>((_, _) => (false, null)));
        Assert.Null(context.Session);

        var session = InterfaceStub.For<IClientSessionHandle>((_, _) => (false, null));
        context.Session = session;

        Assert.Same(session, context.Session);
    }

    private sealed class Thing : Document
    {
    }
}
