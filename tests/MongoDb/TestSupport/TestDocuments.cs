using ArturRios.Data.MongoDb;

namespace ArturRios.Data.Tests.MongoDb.TestSupport;

public class TestDoc : Document
{
    public string Name { get; set; } = string.Empty;
}

public class VersionedTestDoc : VersionedDocument
{
    public string Name { get; set; } = string.Empty;
}
