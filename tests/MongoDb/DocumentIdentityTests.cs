using ArturRios.Data.MongoDb;
using ArturRios.Data.MongoDb.Configuration;
using MongoDB.Bson.Serialization.Attributes;

namespace ArturRios.Data.Tests.MongoDb;

public class DocumentIdentityTests
{
    [Fact]
    public void Document_Id_HasBsonIdAttribute()
    {
        var prop = typeof(Document).GetProperty(nameof(Document.Id))!;
        Assert.NotEmpty(prop.GetCustomAttributes(typeof(BsonIdAttribute), true));
    }

    [Fact]
    public void Document_Id_DefaultsToEmptyString() => Assert.Equal(string.Empty, new Sample().Id);

    [Fact]
    public void VersionedDocument_DerivesFromDocument_AndHasVersion()
    {
        Assert.True(typeof(Document).IsAssignableFrom(typeof(VersionedDocument)));
        Assert.Equal(0L, new VersionedSample().Version);
    }

    [Fact]
    public void MongoOptions_CarryConnectionAndDatabase()
    {
        var o = new MongoOptions { ConnectionString = "mongodb://localhost:27017", DatabaseName = "db" };
        Assert.Equal("mongodb://localhost:27017", o.ConnectionString);
        Assert.Equal("db", o.DatabaseName);
    }

    private sealed class Sample : Document
    {
    }

    private sealed class VersionedSample : VersionedDocument
    {
    }
}
