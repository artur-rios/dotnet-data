using Xunit;

namespace ArturRios.Data.Tests.MongoDb.TestSupport;

/// <summary>xUnit collection so all Mongo integration tests share one replica-set fixture.</summary>
[CollectionDefinition(Name)]
public sealed class MongoTestCollection : ICollectionFixture<MongoReplicaSetFixture>
{
    public const string Name = "mongo";
}
