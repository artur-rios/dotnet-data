using Amazon.DynamoDBv2.DataModel;

namespace ArturRios.Data.Tests.DynamoDb.TestSupport;

[DynamoDBTable("TestItems")]
public class TestItem
{
    [DynamoDBHashKey] public string Category { get; set; } = string.Empty;
    [DynamoDBRangeKey] public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

[DynamoDBTable("VersionedTestItems")]
public class VersionedTestItem
{
    [DynamoDBHashKey] public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    [DynamoDBVersion] public int? Version { get; set; }
}
