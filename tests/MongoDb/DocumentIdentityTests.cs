using ArturRios.Data.MongoDb;
using ArturRios.Data.MongoDb.Configuration;
using MongoDB.Bson.Serialization.Attributes;

namespace ArturRios.Data.Tests.MongoDb;

[Trait("Category", "Unit")]
public class DocumentIdentityTests
{
    [Fact]
    public void GivenTheDocumentId_WhenInspected_ThenItCarriesTheBsonIdAttribute()
    {
        var prop = typeof(Document).GetProperty(nameof(Document.Id))!;
        Assert.NotEmpty(prop.GetCustomAttributes(typeof(BsonIdAttribute), true));
    }

    [Fact]
    public void GivenANewDocument_WhenInspected_ThenItsIdDefaultsToAnEmptyString() => Assert.Equal(string.Empty, new Sample().Id);

    [Fact]
    public void GivenTheVersionedDocumentType_WhenInspected_ThenItDerivesFromDocumentAndCarriesAVersion()
    {
        Assert.True(typeof(Document).IsAssignableFrom(typeof(VersionedDocument)));
        Assert.Equal(0L, new VersionedSample().Version);
    }

    [Fact]
    public void GivenAnObjectInitializer_WhenSettingTheMongoOptions_ThenTheConnectionAndDatabaseAreCarried()
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
