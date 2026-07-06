using Xunit;

namespace ArturRios.Data.Tests.DynamoDb.TestSupport;

/// <summary>xUnit collection so all DynamoDB integration tests share one DynamoDB Local instance.</summary>
[CollectionDefinition(Name)]
public sealed class DynamoTestCollection : ICollectionFixture<DynamoLocalFixture>
{
    public const string Name = "dynamo";
}
